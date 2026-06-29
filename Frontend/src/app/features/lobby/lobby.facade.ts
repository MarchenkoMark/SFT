import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { map } from 'rxjs';

import { RealtimeGatewayService } from '../../core/realtime/realtime-gateway.service';
import { RoomSessionStorageService } from '../../core/realtime/room-session-storage.service';
import { ConnectionQuery } from '../../core/state/connection.query';

@Injectable({ providedIn: 'root' })
export class LobbyFacade {
  readonly vm$;

  constructor(
    private readonly gateway: RealtimeGatewayService,
    private readonly connectionQuery: ConnectionQuery,
    private readonly roomSessionStorage: RoomSessionStorageService,
    private readonly router: Router,
  ) {
    this.vm$ = this.connectionQuery.state$.pipe(
      map((connection) => ({
        connectionStatus: connection.status,
        lastError: connection.lastError,
      })),
    );
  }

  createRoom(): void {
    this.gateway.send({ type: 'createRoom' });
  }

  joinRoom(roomId: string): void {
    const normalizedRoomId = roomId.trim();
    if (!normalizedRoomId) {
      return;
    }

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
      void this.router.navigate(['/join'], { queryParams: { id: normalizedRoomId } });
    }
  }
}
