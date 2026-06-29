import { Injectable } from '@angular/core';
import { Query } from '@datorama/akita';
import { Observable } from 'rxjs';

import { ConnectionState, ConnectionStore } from './connection.store';

@Injectable({ providedIn: 'root' })
export class ConnectionQuery extends Query<ConnectionState> {
  readonly state$: Observable<ConnectionState>;

  constructor(protected override store: ConnectionStore) {
    super(store);
    this.state$ = this.select();
  }

  get snapshot(): ConnectionState {
    return this.getValue();
  }
}
