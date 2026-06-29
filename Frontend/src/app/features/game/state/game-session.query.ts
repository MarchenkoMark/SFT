import { Injectable } from '@angular/core';
import { Query } from '@datorama/akita';
import { Observable } from 'rxjs';

import { GameSessionState, GameSessionStore } from './game-session.store';

@Injectable({ providedIn: 'root' })
export class GameSessionQuery extends Query<GameSessionState> {
  readonly state$: Observable<GameSessionState>;

  constructor(protected override store: GameSessionStore) {
    super(store);
    this.state$ = this.select();
  }

  get snapshot(): GameSessionState {
    return this.getValue();
  }
}
