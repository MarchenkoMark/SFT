import { Direction } from '../../../core/realtime/protocol.models';
import { isOppositeDirection } from './direction';

export interface QueuedInput {
  targetTick: number;
  direction: Direction;
}

export interface ScheduledInputAccepted {
  accepted: true;
  input: QueuedInput;
  queue: QueuedInput[];
}

export interface ScheduledInputRejected {
  accepted: false;
  reason: 'duplicate' | 'directReversal';
  queue: QueuedInput[];
}

export type ScheduledInputResult = ScheduledInputAccepted | ScheduledInputRejected;

export function scheduleLocalDirection(
  currentTick: number,
  currentDirection: Direction,
  requestedDirection: Direction,
  queuedInputs: QueuedInput[] = [],
): ScheduledInputResult {
  const previousDirection =
    queuedInputs.length > 0
      ? queuedInputs[queuedInputs.length - 1].direction
      : currentDirection;

  if (previousDirection === requestedDirection) {
    return { accepted: false, reason: 'duplicate', queue: queuedInputs };
  }

  if (isOppositeDirection(previousDirection, requestedDirection)) {
    return { accepted: false, reason: 'directReversal', queue: queuedInputs };
  }

  const input = {
    targetTick: currentTick + queuedInputs.length + 1,
    direction: requestedDirection,
  };

  return {
    accepted: true,
    input,
    queue: [...queuedInputs, input],
  };
}
