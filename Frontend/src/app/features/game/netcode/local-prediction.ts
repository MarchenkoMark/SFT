import { Direction } from '../../../core/realtime/protocol.models';
import { isOppositeDirection } from './direction';

export type PredictedInputSource = 'local' | 'server';

export interface PredictedInput {
  playerId: string;
  effectiveTick: number;
  direction: Direction;
  clientTime: number;
  sequence: number | null;
  source: PredictedInputSource;
}

export interface SelectPredictedInputEffectiveTickInput {
  currentTick: number;
  tileAlpha: number;
  tickDurationMs: number;
  inputGraceMs: number;
}

export interface SchedulePredictedInputInput {
  playerId: string;
  pendingInputs: PredictedInput[];
  effectiveTick: number;
  currentDirection: Direction;
  requestedDirection: Direction;
  clientTime: number;
  sequence: number;
}

export interface ScheduledPredictedInputAccepted {
  accepted: true;
  input: PredictedInput;
  queue: PredictedInput[];
}

export interface ScheduledPredictedInputRejected {
  accepted: false;
  reason: 'duplicate' | 'directReversal';
  queue: PredictedInput[];
}

export type ScheduledPredictedInputResult =
  | ScheduledPredictedInputAccepted
  | ScheduledPredictedInputRejected;

export function selectPredictedInputEffectiveTick(
  input: SelectPredictedInputEffectiveTickInput,
): number {
  const elapsedIntoTickMs = clamp01(input.tileAlpha) * Math.max(1, input.tickDurationMs);
  const graceMs = Math.max(0, input.inputGraceMs);

  return elapsedIntoTickMs <= graceMs ? input.currentTick : input.currentTick + 1;
}

export function schedulePredictedInput(
  input: SchedulePredictedInputInput,
): ScheduledPredictedInputResult {
  const orderedPending = input.pendingInputs
    .filter((pendingInput) => pendingInput.playerId === input.playerId)
    .sort((a, b) => a.effectiveTick - b.effectiveTick);
  const previousDirection =
    orderedPending.length > 0
      ? orderedPending[orderedPending.length - 1].direction
      : input.currentDirection;

  if (previousDirection === input.requestedDirection) {
    return { accepted: false, reason: 'duplicate', queue: orderedPending };
  }

  if (isOppositeDirection(previousDirection, input.requestedDirection)) {
    return { accepted: false, reason: 'directReversal', queue: orderedPending };
  }

  const lastEffectiveTick =
    orderedPending.length > 0
      ? orderedPending[orderedPending.length - 1].effectiveTick
      : Number.NEGATIVE_INFINITY;
  const predictedInput = {
    playerId: input.playerId,
    effectiveTick: Math.max(input.effectiveTick, lastEffectiveTick + 1),
    direction: input.requestedDirection,
    clientTime: input.clientTime,
    sequence: input.sequence,
    source: 'local' as const,
  };

  return {
    accepted: true,
    input: predictedInput,
    queue: [...orderedPending, predictedInput],
  };
}

function clamp01(value: number): number {
  return Math.min(1, Math.max(0, value));
}
