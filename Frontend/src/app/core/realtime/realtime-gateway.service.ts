import { Injectable, OnDestroy } from '@angular/core';
import { Subject } from 'rxjs';

import { ClockSyncService } from '../clock/clock-sync.service';
import { ConnectionStore } from '../state/connection.store';
import { NetworkDelaySimulatorService } from './network-delay-simulator.service';
import { decodeServerMessage } from './protocol-codecs';
import { ClientMessage, ServerMessage } from './protocol.models';
import { resolveRealtimeUrl } from './realtime-url';

@Injectable({ providedIn: 'root' })
export class RealtimeGatewayService implements OnDestroy {
  private socket: WebSocket | null = null;
  private readonly messagesSubject = new Subject<ServerMessage>();
  private readonly outboundQueue: ClientMessage[] = [];
  private readonly delayedTrafficTimers = new Set<number>();
  private pingTimerId: number | null = null;
  private nextOutboundReleaseAt = 0;
  private nextInboundReleaseAt = 0;

  readonly messages$ = this.messagesSubject.asObservable();

  constructor(
    private readonly connectionStore: ConnectionStore,
    private readonly clockSync: ClockSyncService,
    private readonly networkDelay: NetworkDelaySimulatorService,
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
      this.sendWithDelay(message, this.socket);
      return;
    }

    this.outboundQueue.push(message);
    this.connect();
  }

  disconnect(): void {
    this.stopPingLoop();
    this.clearDelayedTraffic();
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

    const delayMs = this.releaseDelayMs(
      this.networkDelay.serverToClientDelayMs(),
      'serverToClient',
    );
    if (delayMs === 0) {
      this.deliverServerMessage(event.data);
      return;
    }

    this.scheduleDelayedTraffic(() => this.deliverServerMessage(event.data as string), delayMs);
  }

  private deliverServerMessage(message: string): void {
    const decoded = decodeServerMessage(message);
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
    this.clearDelayedTraffic();
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
        this.sendWithDelay(message, this.socket);
      }
    }
  }

  private sendWithDelay(message: ClientMessage, socket: WebSocket): void {
    const payload = JSON.stringify(message);
    const delayMs = this.releaseDelayMs(
      this.networkDelay.clientToServerDelayMs(),
      'clientToServer',
    );

    const sendPayload = () => {
      if (this.socket === socket && socket.readyState === WebSocket.OPEN) {
        socket.send(payload);
      }
    };

    if (delayMs === 0) {
      sendPayload();
      return;
    }

    this.scheduleDelayedTraffic(sendPayload, delayMs);
  }

  private releaseDelayMs(
    sampledDelayMs: number,
    direction: 'clientToServer' | 'serverToClient',
  ): number {
    const now = Date.now();
    const releaseAt = Math.max(
      now + sampledDelayMs,
      direction === 'clientToServer' ? this.nextOutboundReleaseAt : this.nextInboundReleaseAt,
    );

    if (direction === 'clientToServer') {
      this.nextOutboundReleaseAt = releaseAt;
    } else {
      this.nextInboundReleaseAt = releaseAt;
    }

    return Math.max(0, releaseAt - now);
  }

  private scheduleDelayedTraffic(action: () => void, delayMs: number): void {
    const timerId = window.setTimeout(() => {
      this.delayedTrafficTimers.delete(timerId);
      action();
    }, delayMs);

    this.delayedTrafficTimers.add(timerId);
  }

  private clearDelayedTraffic(): void {
    for (const timerId of this.delayedTrafficTimers) {
      window.clearTimeout(timerId);
    }

    this.delayedTrafficTimers.clear();
    this.nextOutboundReleaseAt = 0;
    this.nextInboundReleaseAt = 0;
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
