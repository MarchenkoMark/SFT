import { Injectable } from '@angular/core';
import { Store, StoreConfig } from '@datorama/akita';

import {
  RealtimeErrorMessage,
  RoomAssignmentMessage,
  RoomState,
} from '../../core/realtime/protocol.models';

export interface RoomUiState {
  room: RoomState | null;
  localPlayerId: string | null;
  hasPlayerSessionToken: boolean;
  inviteUrl: string | null;
  pendingReady: boolean;
  lastError: RealtimeErrorMessage | null;
}

const initialState: RoomUiState = {
  room: null,
  localPlayerId: null,
  hasPlayerSessionToken: false,
  inviteUrl: null,
  pendingReady: false,
  lastError: null,
};

@Injectable({ providedIn: 'root' })
@StoreConfig({ name: 'room' })
export class RoomStore extends Store<RoomUiState> {
  constructor() {
    super(initialState);
  }

  acceptAssignment(message: RoomAssignmentMessage, inviteUrl: string): void {
    this.update({
      room: message.room,
      localPlayerId: message.playerId,
      hasPlayerSessionToken: true,
      inviteUrl,
      pendingReady: false,
      lastError: null,
    });
  }

  setRoom(room: RoomState): void {
    this.update({ room, pendingReady: false, lastError: null });
  }

  setPendingReady(pendingReady: boolean): void {
    this.update({ pendingReady });
  }

  setRoomError(error: RealtimeErrorMessage): void {
    this.update({ pendingReady: false, lastError: error });
  }

  clearRoomError(): void {
    this.update({ lastError: null });
  }

  clear(): void {
    this.update(initialState);
  }
}
