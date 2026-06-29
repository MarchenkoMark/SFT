import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { combineLatest, map, timer } from 'rxjs';

import { ClockSyncService } from '../../core/clock/clock-sync.service';
import { RealtimeGatewayService } from '../../core/realtime/realtime-gateway.service';
import { RoomSessionStorageService } from '../../core/realtime/room-session-storage.service';
import { ConnectionQuery } from '../../core/state/connection.query';
import { GameSessionQuery } from '../game/state/game-session.query';
import { GameSessionStore } from '../game/state/game-session.store';
import { RoomQuery } from './room.query';
import { RoomStore } from './room.store';

@Injectable({ providedIn: 'root' })
export class RoomFacade {
  readonly vm$;

  constructor(
    private readonly clockSync: ClockSyncService,
    private readonly connectionQuery: ConnectionQuery,
    private readonly gateway: RealtimeGatewayService,
    private readonly gameSessionQuery: GameSessionQuery,
    private readonly gameSessionStore: GameSessionStore,
    private readonly roomQuery: RoomQuery,
    private readonly roomSessionStorage: RoomSessionStorageService,
    private readonly roomStore: RoomStore,
    private readonly router: Router,
  ) {
    this.vm$ = combineLatest([
      this.roomQuery.state$,
      this.connectionQuery.state$,
      this.gameSessionQuery.state$,
      timer(0, 250),
    ]).pipe(
      map(([roomState, connection, game]) => {
        const room = roomState.room;
        const localPlayer =
          room?.players.find((player) => player.playerId === roomState.localPlayerId) ?? null;
        const waitingForPlayers = (room?.players.length ?? 0) < 2;
        const canToggleReady =
          connection.status === 'open' &&
          !!room &&
          !waitingForPlayers &&
          !roomState.pendingReady &&
          (room.status === 'ReadyCheck' || room.status === 'PostGame');
        const countdownSeconds =
          game.phase === 'countdown' && game.startServerTime
            ? Math.max(
                0,
                Math.ceil((game.startServerTime - this.clockSync.estimatedServerNow()) / 1000),
              )
            : null;

        return {
          room,
          localPlayerId: roomState.localPlayerId,
          inviteUrl: roomState.inviteUrl,
          connectionStatus: connection.status,
          lastError: roomState.lastError?.message ?? connection.lastError,
          players: room?.players ?? [],
          roomStatus: room?.status ?? 'WaitingForPlayers',
          localPlayer,
          waitingForPlayers,
          readyDisabled: !canToggleReady,
          readyLabel: localPlayer?.isReady ? 'Unready' : 'Ready',
          countdownSeconds,
          gamePhase: game.phase,
          isGameRunning: game.phase === 'running',
          gameResult: game.result,
          showGameOver: game.showGameOver,
        };
      }),
    );
  }

  enterRoom(roomId: string): void {
    const normalizedRoomId = roomId.trim();
    if (!normalizedRoomId) {
      void this.router.navigate(['/']);
      return;
    }

    if (this.roomQuery.snapshot.room?.roomId === normalizedRoomId) {
      this.gateway.connect();
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

  toggleReady(): void {
    const room = this.roomQuery.snapshot.room;
    if (!room) {
      return;
    }

    const localPlayer = this.roomQuery.localPlayer;
    this.roomStore.setPendingReady(true);
    this.gateway.send({
      type: localPlayer?.isReady ? 'unready' : 'ready',
      roomId: room.roomId,
    });
  }

  readyForRematch(): void {
    this.gameSessionStore.hideGameOver();
    this.toggleReady();
  }

  dismissGameOver(): void {
    this.gameSessionStore.hideGameOver();
  }

  leaveRoom(): void {
    const roomId = this.roomQuery.snapshot.room?.roomId;
    if (roomId) {
      this.gateway.send({ type: 'leaveRoom', roomId });
    }

    this.roomStore.clear();
    this.gameSessionStore.resetSession();
    void this.router.navigate(['/']);
  }
}
