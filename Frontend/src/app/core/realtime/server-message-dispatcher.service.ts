import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { applyTransaction } from '@datorama/akita';
import { Subscription } from 'rxjs';

import { ClockSyncService } from '../clock/clock-sync.service';
import { RealtimeGatewayService } from './realtime-gateway.service';
import { RoomSessionStorageService } from './room-session-storage.service';
import { ConnectionStore } from '../state/connection.store';
import { GameSessionStore } from '../../features/game/state/game-session.store';
import { LocalPredictionStore } from '../../features/game/state/local-prediction.store';
import { RoomStore } from '../../features/room/room.store';
import { RoomStatus, ServerMessage } from './protocol.models';

@Injectable({ providedIn: 'root' })
export class ServerMessageDispatcherService {
  private subscription: Subscription | null = null;

  constructor(
    private readonly gateway: RealtimeGatewayService,
    private readonly router: Router,
    private readonly clockSync: ClockSyncService,
    private readonly connectionStore: ConnectionStore,
    private readonly roomSessionStorage: RoomSessionStorageService,
    private readonly roomStore: RoomStore,
    private readonly gameSessionStore: GameSessionStore,
    private readonly localPredictionStore: LocalPredictionStore,
  ) {}

  start(): void {
    if (this.subscription) {
      return;
    }

    this.subscription = this.gateway.messages$.subscribe((message) => this.handle(message));
  }

  private handle(message: ServerMessage): void {
    switch (message.type) {
      case 'roomCreated':
      case 'roomJoined':
      case 'roomResumed':
        this.roomSessionStorage.saveToken(message.roomId, message.playerSessionToken);
        applyTransaction(() => {
          this.roomStore.acceptAssignment(message, this.createInviteUrl(message.roomId));
          this.gameSessionStore.resetSession();
          this.localPredictionStore.reset();
        });
        void this.router.navigate(['/room', message.roomId]);
        return;

      case 'roomState':
        applyTransaction(() => {
          this.roomStore.setRoom(message.room);
          if (this.shouldResetCountdown(message.room.status)) {
            this.gameSessionStore.resetSession();
          }
        });
        return;

      case 'gameStarting':
        applyTransaction(() => {
          this.gameSessionStore.countdown(message);
        });
        return;

      case 'gameStarted':
        this.localPredictionStore.reset();
        this.gameSessionStore.started(message);
        return;

      case 'authoritativeFrame':
      case 'correction':
        this.gameSessionStore.acceptFrame(message);
        this.localPredictionStore.removeCoveredInputs(message.tick);
        return;

      case 'turnIntentAccepted':
        this.localPredictionStore.acceptServerIntent(message);
        return;

      case 'gameFinished':
        this.localPredictionStore.reset();
        this.gameSessionStore.finished(message);
        return;

      case 'error':
        this.roomStore.setRoomError(message);
        if (message.code === 'invalidPlayerSessionToken' && message.roomId) {
          this.roomSessionStorage.clearToken(message.roomId);
        }
        return;

      case 'pong':
        this.clockSync.recordPong(message);
        return;
    }
  }

  private createInviteUrl(roomId: string): string {
    const tree = this.router.createUrlTree(['/join'], { queryParams: { id: roomId } });
    return `${window.location.origin}${this.router.serializeUrl(tree)}`;
  }

  private shouldResetCountdown(status: RoomStatus): boolean {
    return (
      this.gameSessionStore.getValue().phase === 'countdown' &&
      (status === 'ReadyCheck' || status === 'WaitingForPlayers')
    );
  }
}
