import { Injectable } from '@angular/core';

import { ClockSyncService } from '../../../core/clock/clock-sync.service';
import { Direction } from '../../../core/realtime/protocol.models';
import { RealtimeGatewayService } from '../../../core/realtime/realtime-gateway.service';
import { RoomQuery } from '../../room/room.query';
import {
  schedulePredictedInput,
  selectPredictedInputTargetTick,
} from '../netcode/local-prediction';
import { computeRenderPhase } from '../netcode/render-clock';
import { projectGameStateToRenderTick } from '../render/snake-sprite.projector';
import { GameSessionQuery } from '../state/game-session.query';
import { LocalPredictionStore } from '../state/local-prediction.store';

@Injectable({ providedIn: 'root' })
export class PlayerInputFacade {
  constructor(
    private readonly clockSync: ClockSyncService,
    private readonly gameSessionQuery: GameSessionQuery,
    private readonly gateway: RealtimeGatewayService,
    private readonly localPredictionStore: LocalPredictionStore,
    private readonly roomQuery: RoomQuery,
  ) {}

  sendDirection(direction: Direction): void {
    const roomId = this.roomQuery.snapshot.room?.roomId;
    if (!roomId) {
      return;
    }

    const clientTime = Math.round(this.clockSync.estimatedServerNow());
    if (!this.applyLocalPrediction(direction, clientTime)) {
      return;
    }

    this.gateway.send({
      type: 'input',
      roomId,
      direction,
      clientTime,
    });
  }

  private applyLocalPrediction(direction: Direction, clientTime: number): boolean {
    const session = this.gameSessionQuery.snapshot;
    const latestFrame = session.latestFrame;
    const timing = session.timing;

    if (!latestFrame || !timing || !session.localPlayerId || !session.startServerTime) {
      return true;
    }

    const phase = computeRenderPhase({
      estimatedServerNow: clientTime,
      matchStartServerTime: session.startServerTime,
      tickDurationMs: timing.tickDurationMs,
      animationFramesPerTile: timing.animationFramesPerTile,
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
    const localSnake = projectedState.snakes.find(
      (snake) => snake.playerId === session.localPlayerId,
    );

    if (!localSnake) {
      return true;
    }

    const targetTick = selectPredictedInputTargetTick({
      currentTick: phase.currentTick,
      tileAlpha: phase.tileAlpha,
      tickDurationMs: timing.tickDurationMs,
      inputGraceMs: timing.animationFrameDurationMs,
    });
    const scheduled = schedulePredictedInput({
      pendingInputs: this.localPredictionStore.snapshot,
      targetTick,
      currentDirection: localSnake.direction,
      requestedDirection: direction,
      clientTime,
      sequence: this.localPredictionStore.nextSequence(),
    });

    if (!scheduled.accepted) {
      return false;
    }

    this.localPredictionStore.setPendingInputs(scheduled.queue);
    return true;
  }
}
