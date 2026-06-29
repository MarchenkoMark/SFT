import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  NgZone,
  OnDestroy,
  ViewChild,
} from '@angular/core';

import { ClockSyncService } from '../../../core/clock/clock-sync.service';
import { computeRenderPhase } from '../netcode/render-clock';
import { GameSessionQuery } from '../state/game-session.query';
import { StoredAuthoritativeFrame } from '../state/game-session.store';
import { LocalPredictionStore } from '../state/local-prediction.store';
import { CanvasGameRenderer } from './canvas-game-renderer.service';
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
      <canvas #canvas class="game-board-canvas" aria-label="Snake board"></canvas>
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

    .game-board-canvas {
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
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GameBoardComponent implements AfterViewInit, OnDestroy {
  @ViewChild('canvas', { static: true })
  private readonly canvasRef!: ElementRef<HTMLCanvasElement>;

  private animationFrameId: number | null = null;
  private lastDrawnModel: GameBoardRenderModel | null = null;
  private lastSeenFrame: Pick<StoredAuthoritativeFrame, 'revision' | 'tick' | 'stateHash'> | null =
    null;
  private activeBlend: ActiveBlend | null = null;

  constructor(
    private readonly clockSync: ClockSyncService,
    private readonly gameSessionQuery: GameSessionQuery,
    private readonly localPredictionStore: LocalPredictionStore,
    private readonly renderer: CanvasGameRenderer,
    private readonly zone: NgZone,
  ) {}

  ngAfterViewInit(): void {
    this.zone.runOutsideAngular(() => {
      this.draw();
      this.scheduleNextFrame();
    });
  }

  ngOnDestroy(): void {
    if (this.animationFrameId !== null) {
      this.cancelAnimationFrame(this.animationFrameId);
    }
  }

  private scheduleNextFrame(): void {
    this.animationFrameId = this.requestAnimationFrame(() => {
      this.draw();
      this.scheduleNextFrame();
    });
  }

  private draw(): void {
    const canvas = this.canvasRef.nativeElement;
    const session = this.gameSessionQuery.snapshot;
    const latestFrame = session.latestFrame;

    if (!latestFrame) {
      this.renderer.drawEmpty(canvas);
      this.lastDrawnModel = null;
      this.lastSeenFrame = null;
      this.activeBlend = null;
      return;
    }

    const timing = session.timing;
    const tickDurationMs = timing?.tickDurationMs ?? 500;
    const animationFramesPerTile = timing?.animationFramesPerTile ?? 5;
    const phase = computeRenderPhase({
      estimatedServerNow: this.clockSync.estimatedServerNow(),
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

    this.renderer.draw(canvas, modelToDraw);
    this.lastDrawnModel = modelToDraw;
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
