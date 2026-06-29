import {
  AuthoritativeSnake,
  Board,
  Cell,
} from '../../../core/realtime/protocol.models';
import { directionVector } from './direction';

export interface ProjectedPoint {
  x: number;
  y: number;
}

export function projectSnakeHead(
  snake: AuthoritativeSnake,
  tileAlpha: number,
): ProjectedPoint {
  const clampedAlpha = Math.min(1, Math.max(0, tileAlpha));
  const vector = directionVector(snake.direction);

  return {
    x: snake.head.x + vector.dx * clampedAlpha,
    y: snake.head.y + vector.dy * clampedAlpha,
  };
}

export function projectCellToCanvas(
  point: Cell | ProjectedPoint,
  board: Board,
  tileSize: number,
): ProjectedPoint {
  return {
    x: point.x * tileSize,
    y: (board.height - 1 - point.y) * tileSize,
  };
}
