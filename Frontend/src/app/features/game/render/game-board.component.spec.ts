import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ClockSyncService } from '../../../core/clock/clock-sync.service';
import { AuthoritativeGameState } from '../../../core/realtime/protocol.models';
import { RenderDiagnosticsReporterService } from '../diagnostics/render-diagnostics-reporter.service';
import { RenderDiagnosticsSettingsService } from '../diagnostics/render-diagnostics-settings.service';
import { GameSessionQuery } from '../state/game-session.query';
import { GameSessionState } from '../state/game-session.store';
import { LocalPredictionStore } from '../state/local-prediction.store';
import { RenderFrameHistoryStore } from '../state/render-frame-history.store';
import { GameBoardComponent } from './game-board.component';
import { GameFieldRenderer } from './game-field-renderer';

describe('GameBoardComponent', () => {
  let fixture: ComponentFixture<GameBoardComponent>;
  let renderer: {
    mount: ReturnType<typeof vi.fn>;
    resize: ReturnType<typeof vi.fn>;
    render: ReturnType<typeof vi.fn>;
    drawEmpty: ReturnType<typeof vi.fn>;
    destroy: ReturnType<typeof vi.fn>;
  };
  let historyStore: {
    beginMatch: ReturnType<typeof vi.fn>;
    recordFrame: ReturnType<typeof vi.fn>;
    finishMatch: ReturnType<typeof vi.fn>;
  };
  let diagnosticsReporter: {
    sendPending: ReturnType<typeof vi.fn>;
  };
  let sessionSnapshot: Partial<GameSessionState>;
  let requestAnimationFrameSpy: ReturnType<typeof vi.spyOn>;
  let cancelAnimationFrameSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(async () => {
    renderer = {
      mount: vi.fn(),
      resize: vi.fn(),
      render: vi.fn(),
      drawEmpty: vi.fn(),
      destroy: vi.fn(),
    };
    historyStore = {
      beginMatch: vi.fn(),
      recordFrame: vi.fn(),
      finishMatch: vi.fn(),
    };
    diagnosticsReporter = {
      sendPending: vi.fn(),
    };
    sessionSnapshot = {
      phase: 'idle',
      roomId: null,
      matchId: null,
      latestFrame: null,
      timing: null,
      startServerTime: null,
      localPlayerId: null,
    };
    requestAnimationFrameSpy = vi
      .spyOn(globalThis, 'requestAnimationFrame')
      .mockImplementation(() => 1);
    cancelAnimationFrameSpy = vi
      .spyOn(globalThis, 'cancelAnimationFrame')
      .mockImplementation(() => undefined);

    TestBed.configureTestingModule({
      imports: [GameBoardComponent],
      providers: [
        { provide: ClockSyncService, useValue: { estimatedServerNow: () => 1250 } },
        {
          provide: GameSessionQuery,
          useValue: {
            get snapshot() {
              return sessionSnapshot as GameSessionState;
            },
          },
        },
        { provide: LocalPredictionStore, useValue: { snapshot: [] } },
        { provide: RenderDiagnosticsReporterService, useValue: diagnosticsReporter },
        { provide: RenderDiagnosticsSettingsService, useValue: { enabled: true } },
        { provide: RenderFrameHistoryStore, useValue: historyStore },
      ],
    });
    TestBed.overrideComponent(GameBoardComponent, {
      set: {
        providers: [{ provide: GameFieldRenderer, useValue: renderer }],
      },
    });
    await TestBed.compileComponents();
  });

  afterEach(() => {
    requestAnimationFrameSpy.mockRestore();
    cancelAnimationFrameSpy.mockRestore();
  });

  it('mounts the renderer host and draws an empty board before frames arrive', async () => {
    fixture = TestBed.createComponent(GameBoardComponent);
    fixture.detectChanges();
    await Promise.resolve();

    const host = fixture.nativeElement.querySelector('.game-board-renderer');
    expect(renderer.mount).toHaveBeenCalledWith(host);
    expect(renderer.drawEmpty).toHaveBeenCalled();
    expect(requestAnimationFrameSpy).toHaveBeenCalled();
  });

  it('renders projected game state through the renderer port', async () => {
    sessionSnapshot = {
      phase: 'running',
      roomId: 'room-1',
      matchId: 'match-1',
      latestFrame: {
        tick: 0,
        serverTime: 1000,
        stateHash: 'frame-0',
        state: createRunningState(),
        source: 'authoritativeFrame',
        revision: 1,
        receivedAt: 1000,
      },
      timing: {
        tilesPerSecond: 2,
        animationFramesPerTile: 5,
        tickDurationMs: 500,
        animationFrameDurationMs: 100,
        inputFutureBufferTicks: 1,
        disconnectGracePeriodSeconds: 10,
      },
      startServerTime: 1000,
      localPlayerId: 'player-1',
    };

    fixture = TestBed.createComponent(GameBoardComponent);
    fixture.detectChanges();
    await Promise.resolve();

    expect(renderer.render).toHaveBeenCalledWith(
      expect.objectContaining({
        board: { width: 8, height: 6 },
        snakes: [
          expect.objectContaining({
            renderKey: 'snake:player-1',
            segmentKeys: ['snake:player-1:segment:0', 'snake:player-1:segment:1'],
          }),
        ],
        food: [
          expect.objectContaining({
            renderKey: 'food:player-1:4:1',
          }),
        ],
      }),
    );
    expect(historyStore.beginMatch).toHaveBeenCalledWith({
      roomId: 'room-1',
      matchId: 'match-1',
      startedAt: 1000,
    });
    expect(historyStore.recordFrame).toHaveBeenCalledWith(
      expect.objectContaining({
        matchId: 'match-1',
        latestFrameTick: 0,
        latestFrameRevision: 1,
        estimatedServerNow: 1250,
        renderTick: 0,
        quantizedTileAlpha: 0.4,
        snakes: [
          expect.objectContaining({
            playerId: 'player-1',
            direction: 'Right',
            projectedHead: { x: 2.4, y: 3 },
            authoritativeHead: { x: 2, y: 3 },
            authoritativeDirection: 'Right',
          }),
        ],
      }),
    );
  });

  it('cancels animation and destroys the renderer on teardown', async () => {
    fixture = TestBed.createComponent(GameBoardComponent);
    fixture.detectChanges();
    await Promise.resolve();

    fixture.destroy();

    expect(cancelAnimationFrameSpy).toHaveBeenCalledWith(1);
    expect(diagnosticsReporter.sendPending).toHaveBeenCalledWith('gameBoardDestroyed');
    expect(renderer.destroy).toHaveBeenCalled();
  });
});

function createRunningState(): AuthoritativeGameState {
  return {
    board: { width: 8, height: 6 },
    status: 'Running',
    snakes: [
      {
        playerId: 'player-1',
        alive: true,
        head: { x: 2, y: 3 },
        direction: 'Right',
        body: [
          { x: 2, y: 3 },
          { x: 1, y: 3 },
        ],
      },
    ],
    food: [{ ownerPlayerId: 'player-1', cell: { x: 4, y: 1 } }],
  };
}
