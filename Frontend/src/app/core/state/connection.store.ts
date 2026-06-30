import { Injectable } from '@angular/core';
import { Store, StoreConfig } from '@datorama/akita';

export type ConnectionStatus = 'idle' | 'connecting' | 'reconnecting' | 'open' | 'closed' | 'error';

export interface ConnectionState {
  status: ConnectionStatus;
  url: string | null;
  lastError: string | null;
  diagnostics: string[];
  serverTimeOffsetMs: number;
  rttMs: number | null;
  jitterMs: number | null;
}

const initialState: ConnectionState = {
  status: 'idle',
  url: null,
  lastError: null,
  diagnostics: [],
  serverTimeOffsetMs: 0,
  rttMs: null,
  jitterMs: null,
};

@Injectable({ providedIn: 'root' })
@StoreConfig({ name: 'connection' })
export class ConnectionStore extends Store<ConnectionState> {
  constructor() {
    super(initialState);
  }

  connecting(url: string): void {
    this.update({ status: 'connecting', url, lastError: null });
  }

  reconnecting(url: string): void {
    this.update({ status: 'reconnecting', url });
  }

  opened(): void {
    this.update({ status: 'open', lastError: null });
  }

  closed(): void {
    this.update({ status: 'closed' });
  }

  failed(error: string): void {
    this.update({ status: 'error', lastError: error });
  }

  clearError(): void {
    this.update({ lastError: null });
  }

  recordDiagnostic(message: string): void {
    this.update((state) => ({
      diagnostics: [message, ...state.diagnostics].slice(0, 20),
    }));
  }

  recordClockSample(serverTimeOffsetMs: number, rttMs: number, jitterMs: number | null): void {
    this.update({ serverTimeOffsetMs, rttMs, jitterMs });
  }
}
