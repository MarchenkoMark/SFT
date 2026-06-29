import { Injectable } from '@angular/core';
import { Query } from '@datorama/akita';
import { Observable } from 'rxjs';

import { RoomPlayer } from '../../core/realtime/protocol.models';
import { RoomStore, RoomUiState } from './room.store';

@Injectable({ providedIn: 'root' })
export class RoomQuery extends Query<RoomUiState> {
  readonly state$: Observable<RoomUiState>;

  constructor(protected override store: RoomStore) {
    super(store);
    this.state$ = this.select();
  }

  get snapshot(): RoomUiState {
    return this.getValue();
  }

  get localPlayer(): RoomPlayer | null {
    const state = this.getValue();
    return (
      state.room?.players.find((player) => player.playerId === state.localPlayerId) ?? null
    );
  }
}
