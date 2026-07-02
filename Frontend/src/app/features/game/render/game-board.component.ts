import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  NgZone,
  OnDestroy,
  ViewChild,
} from '@angular/core';

import { ClockSyncService } from '../../../core/clock/clock-sync.service';
import { RenderDiagnosticsReporterService } from '../diagnostics/render-diagnostics-reporter.service';
import { RenderDiagnosticsSettingsService } from '../diagnostics/render-diagnostics-settings.service';
import { computeRenderPhase, RenderPhase } from '../netcode/render-clock';
import { GameSessionQuery } from '../state/game-session.query';
import { GameSessionState, StoredAuthoritativeFrame } from '../state/game-session.store';
import { LocalPredictionStore } from '../state/local-prediction.store';
import { RenderFrameHistoryStore } from '../state/render-frame-history.store';
import { GameFieldRenderer } from './game-field-renderer';
import { PixiGameRenderer } from './pixi-game-renderer.service';
import {
  blendGameBoardRenderModel,
  buildGameBoardRenderModel,
  GameBoardRenderModel,
  projectGameStateToRenderTick,
} from './snake-sprite.projector';

interface ActiveBlend {
  from: GameBoardRenderModel;
  frameRevision: number;
  startedAt: number;
  durationMs: number;
}

@Component({
  selector: 'app-game-board',
  template: `
    <section class="game-board-frame" aria-label="Snake field">
      <div #rendererHost class="game-board-renderer" aria-label="Snake board"></div>
    </section>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }

    .game-board-frame {
      width: 100%;
      min-height: 280px;
      max-height: min(68vh, 680px);
      aspect-ratio: 4 / 3;
      overflow: hidden;
      border: 1px solid #cdd8d0;
      border-radius: 8px;
      background: #f4f7f2;
    }

    .game-board-renderer {
      display: block;
      width: 100%;
      height: 100%;
    }

    @media (max-width: 640px) {
      .game-board-frame {
        min-height: 240px;
        max-height: 62vh;
      }
    }
  `,
  providers: [{ provide: GameFieldRenderer, useClass: PixiGameRenderer }],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GameBoardComponent implements AfterViewInit, OnDestroy {
  @ViewChild('rendererHost', { static: true })
  private readonly rendererHostRef!: ElementRef<HTMLElement>;

  private animationFrameId: number | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private rendererMounted = false;
  private destroyed = false;
  private lastDrawnModel: GameBoardRenderModel | null = null;
  private lastSeenFrame: Pick<StoredAuthoritativeFrame, 'revision' | 'tick' | 'stateHash'> | null =
    null;
  private activeBlend: ActiveBlend | null = null;

  constructor(
    private readonly clockSync: ClockSyncService,
    private readonly gameSessionQuery: GameSessionQuery,
    private readonly localPredictionStore: LocalPredictionStore,
    private readonly renderDiagnosticsReporter: RenderDiagnosticsReporterService,
    private readonly renderDiagnosticsSettings: RenderDiagnosticsSettingsService,
    private readonly renderer: GameFieldRenderer,
    private readonly injector: Injector,
    private readonly zone: NgZone,
  ) {}

  ngAfterViewInit(): void {
    this.zone.runOutsideAngular(() => {
      void Promise.resolve(this.renderer.mount(this.rendererHostRef.nativeElement)).then(() => {
        if (this.destroyed) {
          return;
        }

        this.rendererMounted = true;
        this.observeResize();
        this.draw();
        this.scheduleNextFrame();
      });
    });
  }

  ngOnDestroy(): void {
    this.destroyed = true;

    if (this.animationFrameId !== null) {
      this.cancelAnimationFrame(this.animationFrameId);
    }

    this.resizeObserver?.disconnect();
    this.renderDiagnosticsReporter.sendPending('gameBoardDestroyed');
    this.renderer.destroy();
  }

  private scheduleNextFrame(): void {
    this.animationFrameId = this.requestAnimationFrame(() => {
      this.draw();
      this.scheduleNextFrame();
    });
  }

  private draw(): void {
    if (!this.rendererMounted) {
      return;
    }

    const session = this.gameSessionQuery.snapshot;
    if (this.renderDiagnosticsSettings.enabled) {
      this.syncRenderHistoryMatch(session);
    }

    const latestFrame = session.latestFrame;

    if (!latestFrame) {
      this.renderer.drawEmpty();
      this.lastDrawnModel = null;
      this.lastSeenFrame = null;
      this.activeBlend = null;
      return;
    }

    const timing = session.timing;
    const tickDurationMs = timing?.tickDurationMs ?? 500;
    const animationFramesPerTile = timing?.animationFramesPerTile ?? 5;
    const estimatedServerNow = this.clockSync.estimatedServerNow();
    const phase = computeRenderPhase({
      estimatedServerNow,
      matchStartServerTime: session.startServerTime ?? latestFrame.serverTime,
      tickDurationMs,
      animationFramesPerTile,
    });
    const projectedState = projectGameStateToRenderTick(
      latestFrame.state,
      latestFrame.tick,
      phase.currentTick,
      {
        localPlayerId: session.localPlayerId,
        predictedInputs: this.localPredictionStore.snapshot,
      },
    );
    const targetModel = buildGameBoardRenderModel({
      state: projectedState,
      localPlayerId: session.localPlayerId,
      tileAlpha: phase.quantizedTileAlpha,
    });
    const modelToDraw = this.applyVisualReconciliation(targetModel, latestFrame);

    this.renderer.render(modelToDraw);
    if (this.renderDiagnosticsSettings.enabled) {
      this.recordRenderFrameHistory(session, latestFrame, phase, estimatedServerNow, modelToDraw);
    }
    this.lastDrawnModel = modelToDraw;
  }

  private syncRenderHistoryMatch(session: GameSessionState): void {
    if (!session.matchId) {
      return;
    }

    this.injector.get(RenderFrameHistoryStore).beginMatch({
      roomId: session.roomId,
      matchId: session.matchId,
      startedAt: session.startServerTime ?? Date.now(),
    });
  }

  private recordRenderFrameHistory(
    session: GameSessionState,
    latestFrame: StoredAuthoritativeFrame,
    phase: RenderPhase,
    estimatedServerNow: number,
    model: GameBoardRenderModel,
  ): void {
    const authoritativeSnakes = new Map(
      latestFrame.state.snakes.map((snake) => [snake.playerId, snake]),
    );

    const renderFrameHistoryStore = this.injector.get(RenderFrameHistoryStore);

    renderFrameHistoryStore.recordFrame({
      capturedAt: Date.now(),
      performanceTime: this.now(),
      roomId: session.roomId,
      matchId: session.matchId,
      localPlayerId: session.localPlayerId,
      latestFrameTick: latestFrame.tick,
      latestFrameRevision: latestFrame.revision,
      latestFrameSource: latestFrame.source,
      latestFrameServerTime: latestFrame.serverTime,
      latestFrameReceivedAt: latestFrame.receivedAt,
      latestFrameStateHash: latestFrame.stateHash,
      estimatedServerNow,
      renderContinuousTick: phase.continuousTick,
      renderTick: phase.currentTick,
      tileAlpha: phase.tileAlpha,
      quantizedTileAlpha: phase.quantizedTileAlpha,
      snakes: model.snakes.map((snake) => {
        const authoritativeSnake = authoritativeSnakes.get(snake.playerId);
        const projectedHead = snake.segments[0] ?? null;

        return {
          playerId: snake.playerId,
          isLocal: snake.isLocal,
          alive: snake.alive,
          direction: snake.direction,
          projectedHead: projectedHead ? { x: projectedHead.x, y: projectedHead.y } : null,
          segmentCount: snake.segments.length,
          authoritativeHead: authoritativeSnake?.head ?? null,
          authoritativeDirection: authoritativeSnake?.direction ?? null,
          authoritativeAlive: authoritativeSnake?.alive ?? null,
          authoritativeBodyLength: authoritativeSnake?.body.length ?? null,
        };
      }),
    });

    if (session.phase === 'finished' || latestFrame.state.status === 'Finished') {
      renderFrameHistoryStore.finishMatch(Date.now());
    }
  }

  private observeResize(): void {
    if (typeof ResizeObserver !== 'undefined') {
      this.resizeObserver = new ResizeObserver(() => this.renderer.resize());
      this.resizeObserver.observe(this.rendererHostRef.nativeElement);
      return;
    }

    this.renderer.resize();
  }

  private applyVisualReconciliation(
    targetModel: GameBoardRenderModel,
    latestFrame: StoredAuthoritativeFrame,
  ): GameBoardRenderModel {
    const previousFrame = this.lastSeenFrame;
    const isNewFrame = previousFrame?.revision !== latestFrame.revision;

    if (isNewFrame) {
      const isRollbackOrCorrection =
        latestFrame.source === 'correction' ||
        (previousFrame !== null &&
          latestFrame.tick <= previousFrame.tick &&
          latestFrame.stateHash !== previousFrame.stateHash);

      if (isRollbackOrCorrection && this.lastDrawnModel) {
        this.activeBlend = {
          from: this.lastDrawnModel,
          frameRevision: latestFrame.revision,
          startedAt: this.now(),
          durationMs: Math.max(16, this.gameSessionQuery.snapshot.timing?.animationFrameDurationMs ?? 100),
        };
      } else if (this.activeBlend?.frameRevision !== latestFrame.revision) {
        this.activeBlend = null;
      }

      this.lastSeenFrame = {
        revision: latestFrame.revision,
        tick: latestFrame.tick,
        stateHash: latestFrame.stateHash,
      };
    }

    if (!this.activeBlend || this.activeBlend.frameRevision !== latestFrame.revision) {
      return targetModel;
    }

    const progress = (this.now() - this.activeBlend.startedAt) / this.activeBlend.durationMs;

    if (progress >= 1) {
      this.activeBlend = null;
      return targetModel;
    }

    return blendGameBoardRenderModel(this.activeBlend.from, targetModel, progress);
  }

  private requestAnimationFrame(callback: FrameRequestCallback): number {
    if (typeof globalThis.requestAnimationFrame === 'function') {
      return globalThis.requestAnimationFrame(callback);
    }

    return window.setTimeout(() => callback(this.now()), 16);
  }

  private cancelAnimationFrame(animationFrameId: number): void {
    if (typeof globalThis.cancelAnimationFrame === 'function') {
      globalThis.cancelAnimationFrame(animationFrameId);
      return;
    }

    window.clearTimeout(animationFrameId);
  }

  private now(): number {
    return typeof performance === 'undefined' ? Date.now() : performance.now();
  }
}
