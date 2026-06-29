import { Injectable } from '@angular/core';

import { PongMessage } from '../realtime/protocol.models';
import { ConnectionQuery } from '../state/connection.query';
import { ConnectionStore } from '../state/connection.store';

@Injectable({ providedIn: 'root' })
export class ClockSyncService {
  private readonly offsets: number[] = [];

  constructor(
    private readonly connectionQuery: ConnectionQuery,
    private readonly connectionStore: ConnectionStore,
  ) {}

  estimatedServerNow(): number {
    return Date.now() + this.connectionQuery.snapshot.serverTimeOffsetMs;
  }

  recordPong(message: PongMessage, receivedAt = Date.now()): void {
    const rttMs = Math.max(0, receivedAt - message.clientTime);
    const estimatedServerAtReceipt = message.serverTime + rttMs / 2;
    const offset = estimatedServerAtReceipt - receivedAt;

    this.offsets.push(offset);
    if (this.offsets.length > 8) {
      this.offsets.shift();
    }

    const averageOffset =
      this.offsets.reduce((total, sample) => total + sample, 0) / this.offsets.length;
    const jitter =
      this.offsets.length > 1
        ? Math.max(...this.offsets.map((sample) => Math.abs(sample - averageOffset)))
        : null;

    this.connectionStore.recordClockSample(averageOffset, rttMs, jitter);
  }
}
