import { Board, Cell } from '../../../core/realtime/protocol.models';
import { ProjectedPoint } from '../netcode/projection-engine';

export interface GameFieldBounds {
  cssWidth: number;
  cssHeight: number;
}

export interface GameFieldLayout extends GameFieldBounds {
  originX: number;
  originY: number;
  tileSize: number;
  boardWidth: number;
  boardHeight: number;
}

export function computeGameFieldLayout(board: Board, bounds: GameFieldBounds): GameFieldLayout {
  const cssWidth = Math.max(1, bounds.cssWidth);
  const cssHeight = Math.max(1, bounds.cssHeight);
  const padding = Math.min(18, Math.max(8, Math.min(cssWidth, cssHeight) * 0.04));
  const tileSize = Math.max(
    1,
    Math.min((cssWidth - padding * 2) / board.width, (cssHeight - padding * 2) / board.height),
  );
  const boardWidth = tileSize * board.width;
  const boardHeight = tileSize * board.height;

  return {
    cssWidth,
    cssHeight,
    originX: (cssWidth - boardWidth) / 2,
    originY: (cssHeight - boardHeight) / 2,
    tileSize,
    boardWidth,
    boardHeight,
  };
}

export function pointTopLeft(
  point: Cell | ProjectedPoint,
  layout: GameFieldLayout,
  board: Board,
): ProjectedPoint {
  return {
    x: layout.originX + point.x * layout.tileSize,
    y: layout.originY + (board.height - 1 - point.y) * layout.tileSize,
  };
}

export function cellCenter(cell: Cell, layout: GameFieldLayout, board: Board): ProjectedPoint {
  const topLeft = pointTopLeft(cell, layout, board);

  return {
    x: topLeft.x + layout.tileSize / 2,
    y: topLeft.y + layout.tileSize / 2,
  };
}
