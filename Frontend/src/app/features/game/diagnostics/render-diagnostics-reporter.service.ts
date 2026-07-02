import { Injectable, Injector } from '@angular/core';

import {
  RenderDiagnosticsClientMessage,
  RenderDiagnosticsFrame,
} from '../../../core/realtime/protocol.models';
import { RealtimeGatewayService } from '../../../core/realtime/realtime-gateway.service';
import {
  RenderFrameHistoryEntry,
  RenderFrameHistoryStore,
} from '../state/render-frame-history.store';
import { renderDiagnosticsWindowMs } from './render-diagnostics.constants';
import { RenderDiagnosticsSettingsService } from './render-diagnostics-settings.service';

@Injectable({ providedIn: 'root' })
export class RenderDiagnosticsReporterService {
  constructor(
    private readonly diagnosticsSettings: RenderDiagnosticsSettingsService,
    private readonly gateway: RealtimeGatewayService,
    private readonly injector: Injector,
  ) {}

  sendPending(reason: string): boolean {
    if (!this.diagnosticsSettings.enabled) {
      return false;
    }

    const historyStore = this.injector.get(RenderFrameHistoryStore);
    const state = historyStore.getValue();
    if (!state.roomId || !state.matchId || state.frameCount === state.sentFrameCount) {
      return false;
    }

    const frames = historyStore.latestWithinMs(renderDiagnosticsWindowMs);
    if (frames.length === 0) {
      return false;
    }

    const message: RenderDiagnosticsClientMessage = {
      type: 'renderDiagnostics',
      roomId: state.roomId,
      matchId: state.matchId,
      localPlayerId: frames[frames.length - 1].localPlayerId,
      reason,
      clientSentAt: Date.now(),
      capturedWindowMs: renderDiagnosticsWindowMs,
      totalRecordedFrameCount: state.frameCount,
      sentFrameCount: frames.length,
      frames: frames.map((frame) => this.toDiagnosticFrame(frame)),
    };

    this.gateway.send(message);
    historyStore.markSent(message.clientSentAt);
    return true;
  }

  private toDiagnosticFrame(frame: RenderFrameHistoryEntry): RenderDiagnosticsFrame {
    return {
      frameIndex: frame.frameIndex,
      capturedAt: frame.capturedAt,
      performanceTime: frame.performanceTime,
      latestFrameTick: frame.latestFrameTick,
      latestFrameRevision: frame.latestFrameRevision,
      latestFrameSource: frame.latestFrameSource,
      latestFrameServerTime: frame.latestFrameServerTime,
      latestFrameReceivedAt: frame.latestFrameReceivedAt,
      estimatedServerNow: frame.estimatedServerNow,
      renderContinuousTick: frame.renderContinuousTick,
      renderTick: frame.renderTick,
      tileAlpha: frame.tileAlpha,
      quantizedTileAlpha: frame.quantizedTileAlpha,
      renderTickDelta: frame.renderTick - frame.latestFrameTick,
      frameServerLeadMs: frame.latestFrameServerTime - frame.estimatedServerNow,
      receivedFrameLeadMs: frame.latestFrameServerTime - frame.latestFrameReceivedAt,
      estimatedServerOffsetMs: frame.estimatedServerNow - frame.capturedAt,
      snakes: frame.snakes.map((snake) => ({
        playerId: snake.playerId,
        isLocal: snake.isLocal,
        alive: snake.alive,
        direction: snake.direction,
        projectedHead: snake.projectedHead,
        authoritativeHead: snake.authoritativeHead,
      })),
    };
  }
}
