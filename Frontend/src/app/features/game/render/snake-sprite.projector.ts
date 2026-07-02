import {
  AuthoritativeGameState,
  AuthoritativeSnake,
  Board,
  Cell,
} from '../../../core/realtime/protocol.models';
import { directionVector, DirectionVector } from '../netcode/direction';
import { PredictedInput } from '../netcode/local-prediction';
import { ProjectedPoint } from '../netcode/projection-engine';

export const defaultSnakeColors = [
  '#2f80ed',
  '#f2994a',
  '#27ae60',
  '#eb5757',
  '#9b51e0',
  '#00a6a6',
] as const;

export const fallbackFoodColor = '#6b7280';

export interface GameBoardRenderInput {
  state: AuthoritativeGameState;
  localPlayerId: string | null;
  tileAlpha: number;
  snakeColors?: Record<string, string>;
}

export interface RenderSnake {
  renderKey: string;
  playerId: string;
  alive: boolean;
  isLocal: boolean;
  color: string;
  direction: AuthoritativeSnake['direction'];
  segments: ProjectedPoint[];
  segmentKeys: string[];
}

export interface RenderFood {
  renderKey: string;
  ownerPlayerId: string;
  cell: Cell;
  color: string;
}

export interface GameBoardRenderModel {
  board: Board;
  status: AuthoritativeGameState['status'];
  localPlayerId: string | null;
  snakes: RenderSnake[];
  food: RenderFood[];
}

export interface GameStateProjectionOptions {
  localPlayerId: string | null;
  predictedInputs: PredictedInput[];
}

export function buildGameBoardRenderModel(input: GameBoardRenderInput): GameBoardRenderModel {
  const colorsByPlayer = buildColorMap(input.state.snakes, input.snakeColors);
  const tileAlpha = input.state.status === 'Running' ? input.tileAlpha : 0;

  return {
    board: input.state.board,
    status: input.state.status,
    localPlayerId: input.localPlayerId,
    snakes: input.state.snakes.map((snake) => {
      const segments = projectSnakeSegments(snake, snake.alive ? tileAlpha : 0, input.state.board);

      return {
        renderKey: snakeRenderKey(snake.playerId),
        playerId: snake.playerId,
        alive: snake.alive,
        isLocal: snake.playerId === input.localPlayerId,
        color: colorsByPlayer.get(snake.playerId) ?? fallbackFoodColor,
        direction: snake.direction,
        segments,
        segmentKeys: segments.map((_, index) => snakeSegmentRenderKey(snake.playerId, index)),
      };
    }),
    food: input.state.food.map((food) => ({
      renderKey: foodRenderKey(food.ownerPlayerId, food.cell),
      ...food,
      color: colorsByPlayer.get(food.ownerPlayerId) ?? fallbackFoodColor,
    })),
  };
}

export function projectSnakeSegments(
  snake: AuthoritativeSnake,
  tileAlpha: number,
  board?: Board,
): ProjectedPoint[] {
  if (!snake.alive && snake.body.length === 0) {
    return [];
  }

  const clampedAlpha = clamp01(tileAlpha);
  const vector = directionVector(snake.direction);
  const body = snake.body.length > 0 ? snake.body : [snake.head];

  return body.map((cell, index) => {
    const from = index === 0 ? snake.head : cell;
    const to =
      index === 0
        ? {
            x: snake.head.x + vector.dx,
            y: snake.head.y + vector.dy,
          }
        : body[index - 1];

    return board
      ? lerpWrappedPoint(from, to, clampedAlpha, board)
      : lerpPoint(from, to, clampedAlpha);
  });
}

export function projectGameStateToRenderTick(
  state: AuthoritativeGameState,
  sourceTick: number,
  renderTick: number,
  options: GameStateProjectionOptions = { localPlayerId: null, predictedInputs: [] },
): AuthoritativeGameState {
  const ticksToPredict = Math.max(0, Math.floor(renderTick - sourceTick));
  if (state.status !== 'Running') {
    return state;
  }

  const inputsByPlayerAndTick = buildInputsByPlayerAndTick(
    options.predictedInputs.filter((input) => input.effectiveTick >= sourceTick),
  );
  const projectedSnakes =
    ticksToPredict === 0
      ? state.snakes.map((snake) =>
          applyRenderTickDirection(
            snake,
            inputsByPlayerAndTick.get(snake.playerId)?.get(renderTick),
          ),
        )
      : state.snakes.map((snake) =>
          predictSnakeToTick(
            snake,
            state.board,
            sourceTick,
            renderTick,
            inputsByPlayerAndTick.get(snake.playerId) ?? new Map(),
          ),
        );

  return {
    ...state,
    snakes: projectedSnakes,
  };
}

export function blendGameBoardRenderModel(
  from: GameBoardRenderModel,
  to: GameBoardRenderModel,
  progress: number,
): GameBoardRenderModel {
  const clampedProgress = clamp01(progress);
  const fromSnakes = new Map(from.snakes.map((snake) => [snake.playerId, snake]));

  return {
    ...to,
    snakes: to.snakes.map((targetSnake) => {
      const sourceSnake = fromSnakes.get(targetSnake.playerId);
      if (!sourceSnake || sourceSnake.segments.length !== targetSnake.segments.length) {
        return targetSnake;
      }

      return {
        ...targetSnake,
        segments: targetSnake.segments.map((segment, index) =>
          lerpPoint(sourceSnake.segments[index], segment, clampedProgress),
        ),
      };
    }),
  };
}

function buildColorMap(
  snakes: AuthoritativeSnake[],
  assignedColors: Record<string, string> = {},
): Map<string, string> {
  const colorsByPlayer = new Map<string, string>(Object.entries(assignedColors));

  snakes.forEach((snake, index) => {
    if (!colorsByPlayer.has(snake.playerId)) {
      colorsByPlayer.set(snake.playerId, defaultSnakeColors[index % defaultSnakeColors.length]);
    }
  });

  return colorsByPlayer;
}

function snakeRenderKey(playerId: string): string {
  return `snake:${playerId}`;
}

function snakeSegmentRenderKey(playerId: string, index: number): string {
  return `${snakeRenderKey(playerId)}:segment:${index}`;
}

function foodRenderKey(ownerPlayerId: string, cell: Cell): string {
  return `food:${ownerPlayerId}:${cell.x}:${cell.y}`;
}

function lerpPoint(from: Cell, to: Cell, alpha: number): ProjectedPoint {
  return {
    x: from.x + (to.x - from.x) * alpha,
    y: from.y + (to.y - from.y) * alpha,
  };
}

function lerpWrappedPoint(from: Cell, to: Cell, alpha: number, board: Board): ProjectedPoint {
  return {
    x: from.x + wrappedDelta(from.x, to.x, board.width) * alpha,
    y: from.y + wrappedDelta(from.y, to.y, board.height) * alpha,
  };
}

function predictSnakeToTick(
  snake: AuthoritativeSnake,
  board: Board,
  sourceTick: number,
  renderTick: number,
  inputsByTick: Map<number, AuthoritativeSnake['direction']>,
): AuthoritativeSnake {
  if (!snake.alive || snake.body.length === 0) {
    return snake;
  }

  let body = snake.body.map((cell) => ({ ...cell }));
  let direction = snake.direction;

  for (let tick = sourceTick; tick < renderTick; tick++) {
    direction = nextDirectionForTick(direction, inputsByTick.get(tick));
    const vector = directionVector(direction);
    const nextHead = wrapCell(moveCell(body[0], vector), board);
    body = [nextHead, ...body.slice(0, -1)];
  }

  direction = nextDirectionForTick(direction, inputsByTick.get(renderTick));

  return {
    ...snake,
    direction,
    head: body[0],
    body,
  };
}

function applyRenderTickDirection(
  snake: AuthoritativeSnake,
  requestedDirection: AuthoritativeSnake['direction'] | undefined,
): AuthoritativeSnake {
  const direction = nextDirectionForTick(snake.direction, requestedDirection);

  return direction === snake.direction ? snake : { ...snake, direction };
}

function nextDirectionForTick(
  currentDirection: AuthoritativeSnake['direction'],
  requestedDirection: AuthoritativeSnake['direction'] | undefined,
): AuthoritativeSnake['direction'] {
  return requestedDirection ?? currentDirection;
}

function buildInputsByPlayerAndTick(
  predictedInputs: PredictedInput[],
): Map<string, Map<number, AuthoritativeSnake['direction']>> {
  const inputsByPlayerAndTick = new Map<string, Map<number, AuthoritativeSnake['direction']>>();
  const orderedInputs = [...predictedInputs].sort(
    (a, b) =>
      a.effectiveTick - b.effectiveTick ||
      (a.sequence ?? 0) - (b.sequence ?? 0) ||
      (a.source === b.source ? 0 : a.source === 'server' ? 1 : -1),
  );

  for (const input of orderedInputs) {
    let inputsByTick = inputsByPlayerAndTick.get(input.playerId);
    if (!inputsByTick) {
      inputsByTick = new Map<number, AuthoritativeSnake['direction']>();
      inputsByPlayerAndTick.set(input.playerId, inputsByTick);
    }

    inputsByTick.set(input.effectiveTick, input.direction);
  }

  return inputsByPlayerAndTick;
}

function moveCell(cell: Cell, vector: DirectionVector): Cell {
  return {
    x: cell.x + vector.dx,
    y: cell.y + vector.dy,
  };
}

function wrapCell(cell: Cell, board: Board): Cell {
  return {
    x: modulo(cell.x, board.width),
    y: modulo(cell.y, board.height),
  };
}

function wrappedDelta(from: number, to: number, size: number): number {
  if (size <= 0) {
    return to - from;
  }

  const direct = to - from;
  if (Math.abs(direct) <= size / 2) {
    return direct;
  }

  return direct > 0 ? direct - size : direct + size;
}

function modulo(value: number, size: number): number {
  return ((value % size) + size) % size;
}

function clamp01(value: number): number {
  return Math.min(1, Math.max(0, value));
}
