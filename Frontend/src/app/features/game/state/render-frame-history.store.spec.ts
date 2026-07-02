import { TestBed } from '@angular/core/testing';

import { RenderFrameHistoryStore } from './render-frame-history.store';

describe('RenderFrameHistoryStore', () => {
  let store: RenderFrameHistoryStore;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    store = TestBed.inject(RenderFrameHistoryStore);
    store.clear();
  });

  afterEach(() => {
    store.clear();
    delete window.__SFT_RENDER_HISTORY__;
  });

  it('records lightweight per-frame snake head history for the active match', () => {
    store.beginMatch({ roomId: 'room-1', matchId: 'match-1', startedAt: 1000 });
    store.recordFrame(createFrameInput({ projectedHead: { x: 0.4, y: 0 } }));
    store.recordFrame(createFrameInput({ projectedHead: { x: 0.6, y: 0 } }));

    expect(store.getValue()).toEqual(
      expect.objectContaining({
        roomId: 'room-1',
        matchId: 'match-1',
        startedAt: 1000,
        endedAt: null,
        frameCount: 2,
      }),
    );
    expect(store.entries().map((entry) => entry.snakes[0].projectedHead)).toEqual([
      { x: 0.4, y: 0 },
      { x: 0.6, y: 0 },
    ]);
    expect(store.latest(1)[0].frameIndex).toBe(1);
  });

  it('resets when a new match begins and keeps finished history until then', () => {
    store.beginMatch({ roomId: 'room-1', matchId: 'match-1', startedAt: 1000 });
    store.recordFrame(createFrameInput({ projectedHead: { x: 1, y: 0 } }));
    store.finishMatch(1600);
    store.recordFrame(createFrameInput({ projectedHead: { x: 2, y: 0 } }));

    expect(store.getValue().endedAt).toBe(1600);
    expect(store.entries()).toHaveLength(1);

    store.beginMatch({ roomId: 'room-1', matchId: 'match-2', startedAt: 2000 });

    expect(store.getValue()).toEqual(
      expect.objectContaining({
        matchId: 'match-2',
        startedAt: 2000,
        endedAt: null,
        frameCount: 0,
      }),
    );
    expect(store.entries()).toEqual([]);
  });

  it('retains only the latest 30 seconds while keeping total frame indexes', () => {
    store.beginMatch({ roomId: 'room-1', matchId: 'match-1', startedAt: 1000 });

    store.recordFrame(
      createFrameInput({ capturedAt: 9_999, projectedHead: { x: 0.2, y: 0 } }),
    );
    store.recordFrame(
      createFrameInput({ capturedAt: 10_000, projectedHead: { x: 0.4, y: 0 } }),
    );
    store.recordFrame(
      createFrameInput({ capturedAt: 40_000, projectedHead: { x: 0.6, y: 0 } }),
    );

    expect(store.getValue().frameCount).toBe(3);
    expect(store.entries().map((entry) => entry.frameIndex)).toEqual([1, 2]);
    expect(store.entries().map((entry) => entry.snakes[0].projectedHead)).toEqual([
      { x: 0.4, y: 0 },
      { x: 0.6, y: 0 },
    ]);
  });
});

function createFrameInput(input: {
  capturedAt?: number;
  projectedHead: { x: number; y: number };
}) {
  return {
    capturedAt: input.capturedAt ?? 1200,
    performanceTime: 200,
    roomId: 'room-1',
    matchId: 'match-1',
    localPlayerId: 'player-1',
    latestFrameTick: 1,
    latestFrameRevision: 2,
    latestFrameSource: 'authoritativeFrame',
    latestFrameServerTime: 1000,
    latestFrameReceivedAt: 1190,
    latestFrameStateHash: 'hash-1',
    estimatedServerNow: 1200,
    renderContinuousTick: 1.4,
    renderTick: 1,
    tileAlpha: 0.4,
    quantizedTileAlpha: 0.4,
    snakes: [
      {
        playerId: 'player-1',
        isLocal: true,
        alive: true,
        direction: 'Right' as const,
        projectedHead: input.projectedHead,
        segmentCount: 2,
        authoritativeHead: { x: 1, y: 0 },
        authoritativeDirection: 'Right' as const,
        authoritativeAlive: true,
        authoritativeBodyLength: 2,
      },
    ],
  };
}
