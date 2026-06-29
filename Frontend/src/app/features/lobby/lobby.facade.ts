import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { combineLatest, map } from 'rxjs';

import { RealtimeGatewayService } from '../../core/realtime/realtime-gateway.service';
import { RoomSessionStorageService } from '../../core/realtime/room-session-storage.service';
import { ConnectionQuery } from '../../core/state/connection.query';
import { ConnectionStore } from '../../core/state/connection.store';
import { GameSessionStore } from '../game/state/game-session.store';
import { RoomQuery } from '../room/room.query';
import { RoomStore } from '../room/room.store';

@Injectable({ providedIn: 'root' })
export class LobbyFacade {
  readonly vm$;

  constructor(
    private readonly gateway: RealtimeGatewayService,
    private readonly connectionQuery: ConnectionQuery,
    private readonly connectionStore: ConnectionStore,
    private readonly gameSessionStore: GameSessionStore,
    private readonly roomSessionStorage: RoomSessionStorageService,
    private readonly roomQuery: RoomQuery,
    private readonly roomStore: RoomStore,
    private readonly router: Router,
  ) {
    this.vm$ = combineLatest([this.connectionQuery.state$, this.roomQuery.state$]).pipe(
      map(([connection, room]) => ({
        connectionStatus: connection.status,
        lastError: room.lastError?.message ?? connection.lastError,
      })),
    );
  }

  createRoom(): void {
    this.clearErrors();
    this.gateway.send({ type: 'createRoom' });
  }

  joinRoom(roomId: string): void {
    const normalizedRoomId = roomId.trim();
    if (!normalizedRoomId) {
      return;
    }

    this.clearErrors();
    const token = this.roomSessionStorage.getToken(normalizedRoomId);
    this.gateway.send(
      token
        ? {
            type: 'resumeRoom',
            roomId: normalizedRoomId,
            playerSessionToken: token,
          }
        : { type: 'joinRoom', roomId: normalizedRoomId },
    );
  }

  openJoinRoute(roomId: string): void {
    const normalizedRoomId = roomId.trim();
    if (normalizedRoomId) {
      this.clearErrors();
      void this.router.navigate(['/join'], { queryParams: { id: normalizedRoomId } });
    }
  }

  goHome(): void {
    this.clearSessionUi();
    void this.router.navigate(['/']);
  }

  clearSessionUi(): void {
    this.roomStore.clear();
    this.gameSessionStore.resetSession();
    this.connectionStore.clearError();
  }

  private clearErrors(): void {
    this.roomStore.clearRoomError();
    this.connectionStore.clearError();
  }
}
