import { Injectable } from '@angular/core';
import { Store, StoreConfig } from '@datorama/akita';

import {
  AuthoritativeFrameMessage,
  AuthoritativeGameState,
  CorrectionMessage,
  GameFinishedMessage,
  GameStartedMessage,
  GameStartingMessage,
  TimingSettings,
} from '../../../core/realtime/protocol.models';

export type GamePhase = 'idle' | 'countdown' | 'running' | 'finished';

export interface GameResult {
  result: string;
  reason: string;
}

export interface StoredAuthoritativeFrame {
  tick: number;
  serverTime: number;
  stateHash: string;
  state: AuthoritativeGameState;
  source: AuthoritativeFrameMessage['type'] | CorrectionMessage['type'] | 'finalState';
  revision: number;
  receivedAt: number;
}

export interface GameSessionState {
  phase: GamePhase;
  roomId: string | null;
  matchId: string | null;
  localPlayerId: string | null;
  localSeat: number | null;
  startServerTime: number | null;
  seed: number | null;
  timing: TimingSettings | null;
  latestFrame: StoredAuthoritativeFrame | null;
  result: GameResult | null;
  showGameOver: boolean;
}

const initialState: GameSessionState = {
  phase: 'idle',
  roomId: null,
  matchId: null,
  localPlayerId: null,
  localSeat: null,
  startServerTime: null,
  seed: null,
  timing: null,
  latestFrame: null,
  result: null,
  showGameOver: false,
};

@Injectable({ providedIn: 'root' })
@StoreConfig({ name: 'gameSession' })
export class GameSessionStore extends Store<GameSessionState> {
  constructor() {
    super(initialState);
  }

  countdown(message: GameStartingMessage): void {
    this.update({
      phase: 'countdown',
      roomId: message.roomId,
      matchId: message.matchId,
      startServerTime: message.startServerTime,
      seed: message.seed,
      timing: message.timing,
      latestFrame: null,
      result: null,
      showGameOver: false,
    });
  }

  started(message: GameStartedMessage): void {
    this.update({
      phase: 'running',
      roomId: message.roomId,
      matchId: message.matchId,
      localPlayerId: message.playerId,
      localSeat: message.seat,
      startServerTime: message.startServerTime,
      seed: message.seed,
      timing: message.timing,
      latestFrame: null,
      result: null,
      showGameOver: false,
    });
  }

  acceptFrame(message: AuthoritativeFrameMessage | CorrectionMessage): void {
    this.update((state) => ({
      phase: 'running',
      roomId: message.roomId,
      matchId: message.matchId,
      latestFrame: {
        tick: message.tick,
        serverTime: message.serverTime,
        stateHash: message.stateHash,
        state: message.state,
        source: message.type,
        revision: (state.latestFrame?.revision ?? 0) + 1,
        receivedAt: Date.now(),
      },
    }));
  }

  finished(message: GameFinishedMessage): void {
    this.update((state) => ({
      phase: 'finished',
      roomId: message.roomId,
      matchId: message.matchId,
      latestFrame: message.finalState
        ? {
            tick: state.latestFrame?.tick ?? 0,
            serverTime: Date.now(),
            stateHash: state.latestFrame?.stateHash ?? 'final',
            state: message.finalState,
            source: 'finalState',
            revision: (state.latestFrame?.revision ?? 0) + 1,
            receivedAt: Date.now(),
          }
        : state.latestFrame,
      result: { result: message.result, reason: message.reason },
      showGameOver: true,
    }));
  }

  hideGameOver(): void {
    this.update({ showGameOver: false });
  }

  resetSession(): void {
    this.update(initialState);
  }
}
