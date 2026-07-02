import { isDevMode, Injectable } from '@angular/core';
import { Store, StoreConfig } from '@datorama/akita';

import { Cell, Direction } from '../../../core/realtime/protocol.models';
import { renderDiagnosticsWindowMs } from '../diagnostics/render-diagnostics.constants';
import { RenderDiagnosticsSettingsService } from '../diagnostics/render-diagnostics-settings.service';

export interface RenderFramePoint {
  x: number;
  y: number;
}

export interface RenderFrameSnakeHeadSample {
  playerId: string;
  isLocal: boolean;
  alive: boolean;
  direction: Direction;
  projectedHead: RenderFramePoint | null;
  segmentCount: number;
  authoritativeHead: Cell | null;
  authoritativeDirection: Direction | null;
  authoritativeAlive: boolean | null;
  authoritativeBodyLength: number | null;
}

export interface RenderFrameHistoryEntry {
  frameIndex: number;
  capturedAt: number;
  performanceTime: number;
  roomId: string | null;
  matchId: string;
  localPlayerId: string | null;
  latestFrameTick: number;
  latestFrameRevision: number;
  latestFrameSource: string;
  latestFrameServerTime: number;
  latestFrameReceivedAt: number;
  latestFrameStateHash: string;
  estimatedServerNow: number;
  renderContinuousTick: number;
  renderTick: number;
  tileAlpha: number;
  quantizedTileAlpha: number;
  snakes: RenderFrameSnakeHeadSample[];
}

export interface RecordRenderFrameHistoryInput {
  capturedAt: number;
  performanceTime: number;
  roomId: string | null;
  matchId: string | null;
  localPlayerId: string | null;
  latestFrameTick: number;
  latestFrameRevision: number;
  latestFrameSource: string;
  latestFrameServerTime: number;
  latestFrameReceivedAt: number;
  latestFrameStateHash: string;
  estimatedServerNow: number;
  renderContinuousTick: number;
  renderTick: number;
  tileAlpha: number;
  quantizedTileAlpha: number;
  snakes: RenderFrameSnakeHeadSample[];
}

export interface RenderFrameHistoryState {
  roomId: string | null;
  matchId: string | null;
  startedAt: number | null;
  endedAt: number | null;
  sentAt: number | null;
  sentFrameCount: number;
  frameCount: number;
  chunks: RenderFrameHistoryEntry[][];
}

export interface RenderFrameHistoryDebugApi {
  getState(): RenderFrameHistoryState;
  getFrames(): RenderFrameHistoryEntry[];
  latest(count?: number): RenderFrameHistoryEntry[];
  clear(): void;
  copyAll(): Promise<number>;
  copyLatest(count?: number): Promise<number>;
}

declare global {
  interface Window {
    __SFT_RENDER_HISTORY__?: RenderFrameHistoryDebugApi;
  }
}

const framesPerChunk = 240;

const initialState: RenderFrameHistoryState = {
  roomId: null,
  matchId: null,
  startedAt: null,
  endedAt: null,
  sentAt: null,
  sentFrameCount: 0,
  frameCount: 0,
  chunks: [],
};

@Injectable({ providedIn: 'root' })
@StoreConfig({ name: 'renderFrameHistory' })
export class RenderFrameHistoryStore extends Store<RenderFrameHistoryState> {
  constructor(private readonly diagnosticsSettings: RenderDiagnosticsSettingsService) {
    super(initialState);
    this.installDebugApi();
  }

  beginMatch(input: { roomId: string | null; matchId: string; startedAt: number }): void {
    const state = this.getValue();

    if (state.matchId === input.matchId) {
      return;
    }

    this.update({
      roomId: input.roomId,
      matchId: input.matchId,
      startedAt: input.startedAt,
      endedAt: null,
      sentAt: null,
      sentFrameCount: 0,
      frameCount: 0,
      chunks: [],
    });
  }

  recordFrame(input: RecordRenderFrameHistoryInput): void {
    const matchId = input.matchId;

    if (!matchId) {
      return;
    }

    this.update((state) => {
      if (state.matchId !== matchId || state.endedAt !== null) {
        return state;
      }

      const entry: RenderFrameHistoryEntry = {
        ...input,
        matchId,
        frameIndex: state.frameCount,
        snakes: input.snakes.map((snake) => ({ ...snake })),
      };
      const chunks = state.chunks.slice();
      const lastChunk = chunks[chunks.length - 1];

      if (!lastChunk || lastChunk.length >= framesPerChunk) {
        chunks.push([entry]);
      } else {
        chunks[chunks.length - 1] = [...lastChunk, entry];
      }
      const retainedChunks = this.pruneOldEntries(
        chunks,
        input.capturedAt - renderDiagnosticsWindowMs,
      );

      return {
        ...state,
        frameCount: state.frameCount + 1,
        chunks: retainedChunks,
      };
    });
  }

  finishMatch(endedAt = Date.now()): void {
    this.update((state) => (state.endedAt === null ? { ...state, endedAt } : state));
  }

  markSent(sentAt = Date.now()): void {
    this.update((state) => ({
      ...state,
      sentAt,
      sentFrameCount: state.frameCount,
    }));
  }

  clear(): void {
    this.update(initialState);
  }

  entries(): RenderFrameHistoryEntry[] {
    return this.getValue().chunks.flatMap((chunk) => [...chunk]);
  }

  latest(count = 120): RenderFrameHistoryEntry[] {
    const safeCount = Math.max(0, Math.floor(count));
    if (safeCount === 0) {
      return [];
    }

    return this.entries().slice(-safeCount);
  }

  latestWithinMs(windowMs: number): RenderFrameHistoryEntry[] {
    const safeWindowMs = Math.max(0, Math.floor(windowMs));
    const entries = this.entries();
    const latestEntry = entries[entries.length - 1];

    if (!latestEntry || safeWindowMs === 0) {
      return [];
    }

    const minimumCapturedAt = latestEntry.capturedAt - safeWindowMs;
    return entries.filter((entry) => entry.capturedAt >= minimumCapturedAt);
  }

  private installDebugApi(): void {
    if (
      typeof window === 'undefined' ||
      !this.diagnosticsSettings.enabled ||
      !this.shouldExposeDebugApi()
    ) {
      return;
    }

    window.__SFT_RENDER_HISTORY__ = {
      getState: () => this.getValue(),
      getFrames: () => this.entries(),
      latest: (count = 120) => this.latest(count),
      clear: () => this.clear(),
      copyAll: () => this.copyToClipboard(this.entries()),
      copyLatest: (count = 120) => this.copyToClipboard(this.latest(count)),
    };
  }

  private shouldExposeDebugApi(): boolean {
    if (isDevMode()) {
      return true;
    }

    const host = window.location.hostname;
    return host === 'localhost' || host === '127.0.0.1' || host === '::1';
  }

  private pruneOldEntries(
    chunks: RenderFrameHistoryEntry[][],
    minimumCapturedAt: number,
  ): RenderFrameHistoryEntry[][] {
    return chunks
      .map((chunk) => chunk.filter((entry) => entry.capturedAt >= minimumCapturedAt))
      .filter((chunk) => chunk.length > 0);
  }

  private async copyToClipboard(entries: RenderFrameHistoryEntry[]): Promise<number> {
    const text = JSON.stringify(entries);

    if (!navigator.clipboard?.writeText) {
      console.warn('Clipboard API is unavailable. Use getFrames() or latest() instead.');
      return entries.length;
    }

    await navigator.clipboard.writeText(text);
    return entries.length;
  }
}
