import { ClockSyncService } from '../clock/clock-sync.service';
import { ConnectionStore } from '../state/connection.store';
import { NetworkDelaySimulatorService } from './network-delay-simulator.service';
import { RealtimeGatewayService } from './realtime-gateway.service';

class MockWebSocket extends EventTarget {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSING = 2;
  static readonly CLOSED = 3;
  static instances: MockWebSocket[] = [];

  readonly sent: string[] = [];
  readyState = MockWebSocket.CONNECTING;

  constructor(readonly url: string) {
    super();
    MockWebSocket.instances.push(this);
  }

  send(message: string): void {
    this.sent.push(message);
  }

  close(code = 1000, reason = ''): void {
    this.readyState = MockWebSocket.CLOSED;
    this.dispatchEvent(new CloseEvent('close', { code, reason }));
  }

  open(): void {
    this.readyState = MockWebSocket.OPEN;
    this.dispatchEvent(new Event('open'));
  }

  receive(message: string): void {
    this.dispatchEvent(new MessageEvent('message', { data: message }));
  }
}

describe('RealtimeGatewayService', () => {
  let originalWebSocket: typeof WebSocket;
  let connectionStore: ConnectionStore;
  let gateway: RealtimeGatewayService;
  let networkDelay: NetworkDelaySimulatorService;

  beforeEach(() => {
    vi.useFakeTimers();
    sessionStorage.clear();
    MockWebSocket.instances = [];
    originalWebSocket = globalThis.WebSocket;
    globalThis.WebSocket = MockWebSocket as unknown as typeof WebSocket;

    connectionStore = {
      connecting: vi.fn(),
      reconnecting: vi.fn(),
      opened: vi.fn(),
      closed: vi.fn(),
      failed: vi.fn(),
      recordDiagnostic: vi.fn(),
    } as unknown as ConnectionStore;
    networkDelay = new NetworkDelaySimulatorService();
    gateway = new RealtimeGatewayService(connectionStore, {} as ClockSyncService, networkDelay);
  });

  afterEach(() => {
    gateway.disconnect();
    globalThis.WebSocket = originalWebSocket;
    sessionStorage.clear();
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('delays outbound messages before they reach the socket', () => {
    gateway.connect();
    const socket = MockWebSocket.instances[0];
    socket.open();
    socket.sent.length = 0;
    networkDelay.update({ clientToServerMs: 100 });

    gateway.send({ type: 'createRoom' });

    expect(socket.sent).toEqual([]);

    vi.advanceTimersByTime(99);
    expect(socket.sent).toEqual([]);

    vi.advanceTimersByTime(1);
    expect(JSON.parse(socket.sent[0]) as unknown).toEqual({ type: 'createRoom' });
  });

  it('delays inbound server messages before publishing them', () => {
    const received: unknown[] = [];
    gateway.messages$.subscribe((message) => received.push(message));
    networkDelay.update({ serverToClientMs: 75 });
    gateway.connect();
    const socket = MockWebSocket.instances[0];
    socket.open();

    socket.receive(
      JSON.stringify({
        type: 'pong',
        clientTime: 10,
        serverTime: 20,
        sampleId: 'sample-1',
      }),
    );

    expect(received).toEqual([]);

    vi.advanceTimersByTime(74);
    expect(received).toEqual([]);

    vi.advanceTimersByTime(1);
    expect(received).toEqual([
      {
        type: 'pong',
        clientTime: 10,
        serverTime: 20,
        sampleId: 'sample-1',
      },
    ]);
  });

  it('reconnects and resumes the room after an unexpected socket close', () => {
    gateway.setResumeSession('ROOM1', 'token-1');
    gateway.connect();
    const firstSocket = MockWebSocket.instances[0];
    firstSocket.open();
    firstSocket.sent.length = 0;

    firstSocket.close(1001, 'Network changed.');

    expect(connectionStore.reconnecting).toHaveBeenCalledWith('ws://localhost:5012/ws');
    expect(MockWebSocket.instances).toHaveLength(1);

    vi.advanceTimersByTime(500);

    expect(MockWebSocket.instances).toHaveLength(2);
    const secondSocket = MockWebSocket.instances[1];
    secondSocket.open();

    expect(JSON.parse(secondSocket.sent[0]) as unknown).toEqual({
      type: 'resumeRoom',
      roomId: 'ROOM1',
      playerSessionToken: 'token-1',
    });
  });

  it('does not reconnect when the seat was intentionally resumed by a newer connection', () => {
    gateway.setResumeSession('ROOM1', 'token-1');
    gateway.connect();
    const socket = MockWebSocket.instances[0];
    socket.open();

    socket.close(1008, 'Seat was resumed by a newer connection.');
    vi.advanceTimersByTime(10_000);

    expect(MockWebSocket.instances).toHaveLength(1);
    expect(connectionStore.reconnecting).not.toHaveBeenCalled();
  });
});
