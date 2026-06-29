import { Direction } from '../../../core/realtime/protocol.models';

export interface DirectionVector {
  dx: number;
  dy: number;
}

export function directionVector(direction: Direction): DirectionVector {
  switch (direction) {
    case 'Up':
      return { dx: 0, dy: 1 };
    case 'Right':
      return { dx: 1, dy: 0 };
    case 'Down':
      return { dx: 0, dy: -1 };
    case 'Left':
      return { dx: -1, dy: 0 };
  }
}

export function isOppositeDirection(a: Direction, b: Direction): boolean {
  const first = directionVector(a);
  const second = directionVector(b);
  return first.dx + second.dx === 0 && first.dy + second.dy === 0;
}
