import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { applyTransaction } from '@datorama/akita';
import { Subscription } from 'rxjs';

import { ClockSyncService } from '../clock/clock-sync.service';
import { RealtimeGatewayService } from './realtime-gateway.service';
import { RoomSessionStorageService } from './room-session-storage.service';
import { ConnectionStore } from '../state/connection.store';
import { GameSessionStore } from '../../features/game/state/game-session.store';
import { RoomStore } from '../../features/room/room.store';
import { ServerMessage } from './protocol.models';

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
        });
        void this.router.navigate(['/room', message.roomId]);
        return;

      case 'roomState':
        this.roomStore.setRoom(message.room);
        return;

      case 'gameStarting':
        applyTransaction(() => {
          this.gameSessionStore.countdown(message);
        });
        return;

      case 'gameStarted':
        this.gameSessionStore.started(message);
        return;

      case 'authoritativeFrame':
      case 'correction':
        this.gameSessionStore.acceptFrame(message);
        return;

      case 'gameFinished':
        this.gameSessionStore.finished(message);
        return;

      case 'error':
        this.connectionStore.failed(message.message);
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
}
