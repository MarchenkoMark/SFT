import { RenderDiagnosticsClientMessage } from '../../../core/realtime/protocol.models';
import { RealtimeGatewayService } from '../../../core/realtime/realtime-gateway.service';
import {
  RenderFrameHistoryEntry,
  RenderFrameHistoryState,
} from '../state/render-frame-history.store';
import { RenderDiagnosticsReporterService } from './render-diagnostics-reporter.service';
import { RenderDiagnosticsSettingsService } from './render-diagnostics-settings.service';

describe('RenderDiagnosticsReporterService', () => {
  it('does not touch history or send when diagnostics are disabled', () => {
    const historyStore = {
      getValue: vi.fn(),
      latestWithinMs: vi.fn(),
      markSent: vi.fn(),
    };
    const gateway = { send: vi.fn() };
    const reporter = new RenderDiagnosticsReporterService(
      { enabled: false } as RenderDiagnosticsSettingsService,
      gateway as unknown as RealtimeGatewayService,
      { get: vi.fn(() => historyStore) } as never,
    );

    expect(reporter.sendPending('leaveRoom')).toBe(false);
    expect(historyStore.getValue).not.toHaveBeenCalled();
    expect(gateway.send).not.toHaveBeenCalled();
  });

  it('sends compact diagnostics for only the latest 30 seconds and marks history as sent', () => {
    const frames = [
      createFrame({ frameIndex: 1, capturedAt: 10_000, headX: 1 }),
      createFrame({ frameIndex: 2, capturedAt: 39_900, headX: 2.4 }),
      createFrame({ frameIndex: 3, capturedAt: 40_000, headX: 2.6 }),
    ];
    const state: RenderFrameHistoryState = {
      roomId: 'ROOM1',
      matchId: 'match-1',
      startedAt: 1_000,
      endedAt: null,
      sentAt: null,
      sentFrameCount: 0,
      frameCount: 3,
      chunks: [frames],
    };
    const historyStore = {
      getValue: vi.fn(() => state),
      latestWithinMs: vi.fn(() => frames.slice(1)),
      markSent: vi.fn(),
    };
    const gateway = { send: vi.fn() };
    const reporter = new RenderDiagnosticsReporterService(
      { enabled: true } as RenderDiagnosticsSettingsService,
      gateway as unknown as RealtimeGatewayService,
      { get: vi.fn(() => historyStore) } as never,
    );

    expect(reporter.sendPending('gameFinished')).toBe(true);

    expect(historyStore.latestWithinMs).toHaveBeenCalledWith(30_000);
    const message = gateway.send.mock.calls[0][0] as RenderDiagnosticsClientMessage;
    expect(message).toEqual(
      expect.objectContaining({
        type: 'renderDiagnostics',
        roomId: 'ROOM1',
        matchId: 'match-1',
        reason: 'gameFinished',
        capturedWindowMs: 30_000,
        totalRecordedFrameCount: 3,
        sentFrameCount: 2,
      }),
    );
    expect(message.frames.map((frame) => frame.frameIndex)).toEqual([2, 3]);
    expect(message.frames[0]).toEqual(
      expect.objectContaining({
        renderTickDelta: -1,
        frameServerLeadMs: 400,
        receivedFrameLeadMs: 600,
        estimatedServerOffsetMs: 100,
      }),
    );
    expect(message.frames[0].snakes[0].projectedHead).toEqual({ x: 2.4, y: 0 });
    expect(historyStore.markSent).toHaveBeenCalledWith(message.clientSentAt);
  });
});

function createFrame(input: {
  frameIndex: number;
  capturedAt: number;
  headX: number;
}): RenderFrameHistoryEntry {
  return {
    frameIndex: input.frameIndex,
    capturedAt: input.capturedAt,
    performanceTime: input.frameIndex * 16,
    roomId: 'ROOM1',
    matchId: 'match-1',
    localPlayerId: 'player-1',
    latestFrameTick: 10,
    latestFrameRevision: 11,
    latestFrameSource: 'authoritativeFrame',
    latestFrameServerTime: input.capturedAt + 500,
    latestFrameReceivedAt: input.capturedAt - 100,
    latestFrameStateHash: `hash-${input.frameIndex}`,
    estimatedServerNow: input.capturedAt + 100,
    renderContinuousTick: 9.4,
    renderTick: 9,
    tileAlpha: 0.4,
    quantizedTileAlpha: 0.4,
    snakes: [
      {
        playerId: 'player-1',
        isLocal: true,
        alive: true,
        direction: 'Right',
        projectedHead: { x: input.headX, y: 0 },
        segmentCount: 3,
        authoritativeHead: { x: 2, y: 0 },
        authoritativeDirection: 'Right',
        authoritativeAlive: true,
        authoritativeBodyLength: 3,
      },
    ],
  };
}
