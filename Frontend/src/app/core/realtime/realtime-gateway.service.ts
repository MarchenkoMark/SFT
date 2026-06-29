import { Injectable, OnDestroy } from '@angular/core';
import { Subject } from 'rxjs';

import { ClockSyncService } from '../clock/clock-sync.service';
import { ConnectionStore } from '../state/connection.store';
import { decodeServerMessage } from './protocol-codecs';
import { ClientMessage, ServerMessage } from './protocol.models';
import { resolveRealtimeUrl } from './realtime-url';

@Injectable({ providedIn: 'root' })
export class RealtimeGatewayService implements OnDestroy {
  private socket: WebSocket | null = null;
  private readonly messagesSubject = new Subject<ServerMessage>();
  private readonly outboundQueue: ClientMessage[] = [];
  private pingTimerId: number | null = null;

  readonly messages$ = this.messagesSubject.asObservable();

  constructor(
    private readonly connectionStore: ConnectionStore,
    private readonly clockSync: ClockSyncService,
  ) {}

  connect(): void {
    if (
      this.socket?.readyState === WebSocket.OPEN ||
      this.socket?.readyState === WebSocket.CONNECTING
    ) {
      return;
    }

    const url = resolveRealtimeUrl();
    this.connectionStore.connecting(url);

    this.socket = new WebSocket(url);
    this.socket.addEventListener('open', () => this.handleOpen());
    this.socket.addEventListener('message', (event) => this.handleMessage(event));
    this.socket.addEventListener('close', () => this.handleClose());
    this.socket.addEventListener('error', () => this.handleError());
  }

  send(message: ClientMessage): void {
    if (this.socket?.readyState === WebSocket.OPEN) {
      this.socket.send(JSON.stringify(message));
      return;
    }

    this.outboundQueue.push(message);
    this.connect();
  }

  disconnect(): void {
    this.stopPingLoop();
    this.outboundQueue.length = 0;
    this.socket?.close();
    this.socket = null;
    this.connectionStore.closed();
  }

  ngOnDestroy(): void {
    this.disconnect();
    this.messagesSubject.complete();
  }

  private handleOpen(): void {
    this.connectionStore.opened();
    this.flushQueue();
    this.startPingLoop();
  }

  private handleMessage(event: MessageEvent): void {
    if (typeof event.data !== 'string') {
      this.connectionStore.failed('Server sent a non-text WebSocket message.');
      return;
    }

    const decoded = decodeServerMessage(event.data);
    if (decoded.ok) {
      this.messagesSubject.next(decoded.message);
      return;
    }

    if (decoded.unknownType) {
      this.connectionStore.recordDiagnostic(decoded.reason);
      return;
    }

    this.connectionStore.failed(decoded.reason);
  }

  private handleClose(): void {
    this.stopPingLoop();
    this.socket = null;
    this.connectionStore.closed();
  }

  private handleError(): void {
    this.connectionStore.failed('WebSocket connection failed.');
  }

  private flushQueue(): void {
    while (this.outboundQueue.length > 0 && this.socket?.readyState === WebSocket.OPEN) {
      const message = this.outboundQueue.shift();
      if (message) {
        this.socket.send(JSON.stringify(message));
      }
    }
  }

  private startPingLoop(): void {
    this.stopPingLoop();
    this.sendPing();
    this.pingTimerId = window.setInterval(() => this.sendPing(), 10_000);
  }

  private stopPingLoop(): void {
    if (this.pingTimerId !== null) {
      window.clearInterval(this.pingTimerId);
      this.pingTimerId = null;
    }
  }

  private sendPing(): void {
    const clientTime = Date.now();
    this.send({
      type: 'ping',
      clientTime,
      sampleId: `${clientTime}-${Math.random().toString(16).slice(2)}`,
    });
  }
}
