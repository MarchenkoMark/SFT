import { AuthoritativeGameState, AuthoritativeSnake } from '../../../core/realtime/protocol.models';
import {
  blendGameBoardRenderModel,
  buildGameBoardRenderModel,
  defaultSnakeColors,
  projectGameStateToRenderTick,
  projectSnakeSegments,
} from './snake-sprite.projector';

describe('snake sprite projector', () => {
  it('interpolates the head and body toward the next tile position', () => {
    const snake = createSnake('player-1', 'Right', [
      { x: 5, y: 4 },
      { x: 4, y: 4 },
      { x: 3, y: 4 },
    ]);

    expect(projectSnakeSegments(snake, 0.4)).toEqual([
      { x: 5.4, y: 4 },
      { x: 4.4, y: 4 },
      { x: 3.4, y: 4 },
    ]);
  });

  it('freezes finished game states instead of replaying the final movement', () => {
    const state: AuthoritativeGameState = {
      board: { width: 16, height: 12 },
      status: 'Finished',
      snakes: [
        createSnake('player-1', 'Right', [
          { x: 5, y: 4 },
          { x: 4, y: 4 },
        ]),
      ],
      food: [],
    };

    const model = buildGameBoardRenderModel({
      state,
      localPlayerId: 'player-1',
      tileAlpha: 0.6,
    });

    expect(model.snakes[0].segments).toEqual([
      { x: 5, y: 4 },
      { x: 4, y: 4 },
    ]);
  });

  it('predicts a stale authoritative frame forward to the render tick', () => {
    const state: AuthoritativeGameState = {
      board: { width: 16, height: 12 },
      status: 'Running',
      snakes: [
        createSnake('player-1', 'Right', [
          { x: 1, y: 4 },
          { x: 0, y: 4 },
        ]),
      ],
      food: [],
    };

    const projected = projectGameStateToRenderTick(state, 0, 1);
    const model = buildGameBoardRenderModel({
      state: projected,
      localPlayerId: 'player-1',
      tileAlpha: 0,
    });

    expect(model.snakes[0].segments[0]).toEqual({ x: 2, y: 4 });
    expect(model.snakes[0].segments[1]).toEqual({ x: 1, y: 4 });
  });

  it('applies a queued local turn at the next tile boundary before authority arrives', () => {
    const state: AuthoritativeGameState = {
      board: { width: 16, height: 12 },
      status: 'Running',
      snakes: [
        createSnake('player-1', 'Right', [
          { x: 1, y: 0 },
          { x: 0, y: 0 },
        ]),
      ],
      food: [],
    };

    const projected = projectGameStateToRenderTick(state, 1, 2, {
      localPlayerId: 'player-1',
      predictedInputs: [
        {
          playerId: 'player-1',
          effectiveTick: 2,
          direction: 'Up',
          clientTime: 1250,
          sequence: 1,
          source: 'local',
        },
      ],
    });
    const model = buildGameBoardRenderModel({
      state: projected,
      localPlayerId: 'player-1',
      tileAlpha: 0.2,
    });

    expect(model.snakes[0].segments[0]).toEqual({ x: 2, y: 0.2 });
    expect(model.snakes[0].segments[1]).toEqual({ x: 1.2, y: 0 });
  });

  it('applies a server-accepted opponent turn at its effective tile boundary', () => {
    const state: AuthoritativeGameState = {
      board: { width: 16, height: 12 },
      status: 'Running',
      snakes: [
        createSnake('player-1', 'Left', [
          { x: 10, y: 6 },
          { x: 11, y: 6 },
        ]),
        createSnake('player-2', 'Right', [
          { x: 1, y: 0 },
          { x: 0, y: 0 },
        ]),
      ],
      food: [],
    };

    const projected = projectGameStateToRenderTick(state, 1, 2, {
      localPlayerId: 'player-1',
      predictedInputs: [
        {
          playerId: 'player-2',
          effectiveTick: 2,
          direction: 'Up',
          clientTime: 1250,
          sequence: null,
          source: 'server',
        },
      ],
    });
    const model = buildGameBoardRenderModel({
      state: projected,
      localPlayerId: 'player-1',
      tileAlpha: 0.2,
    });

    expect(model.snakes[1].segments[0]).toEqual({ x: 2, y: 0.2 });
    expect(model.snakes[1].segments[1]).toEqual({ x: 1.2, y: 0 });
  });

  it('applies a grace-window local turn to the current tile before moving out', () => {
    const state: AuthoritativeGameState = {
      board: { width: 16, height: 12 },
      status: 'Running',
      snakes: [
        createSnake('player-1', 'Right', [
          { x: 3, y: 0 },
          { x: 2, y: 0 },
        ]),
      ],
      food: [],
    };

    const projected = projectGameStateToRenderTick(state, 3, 3, {
      localPlayerId: 'player-1',
      predictedInputs: [
        {
          playerId: 'player-1',
          effectiveTick: 3,
          direction: 'Up',
          clientTime: 1550,
          sequence: 1,
          source: 'local',
        },
      ],
    });
    const model = buildGameBoardRenderModel({
      state: projected,
      localPlayerId: 'player-1',
      tileAlpha: 0.2,
    });

    expect(model.snakes[0].segments[0]).toEqual({ x: 3, y: 0.2 });
    expect(model.snakes[0].segments[1]).toEqual({ x: 2.2, y: 0 });
  });

  it('projects wrapped body motion through the nearest board edge', () => {
    const snake = createSnake('player-1', 'Right', [
      { x: 0, y: 4 },
      { x: 31, y: 4 },
      { x: 30, y: 4 },
    ]);

    expect(projectSnakeSegments(snake, 0.4, { width: 32, height: 24 })).toEqual([
      { x: 0.4, y: 4 },
      { x: 31.4, y: 4 },
      { x: 30.4, y: 4 },
    ]);
  });

  it('assigns stable snake colors and applies the owner color to food', () => {
    const state: AuthoritativeGameState = {
      board: { width: 16, height: 12 },
      status: 'Running',
      snakes: [
        createSnake('player-1', 'Up', [
          { x: 2, y: 2 },
          { x: 2, y: 1 },
        ]),
        createSnake('player-2', 'Left', [
          { x: 9, y: 6 },
          { x: 10, y: 6 },
        ]),
      ],
      food: [
        { ownerPlayerId: 'player-2', cell: { x: 7, y: 3 } },
        { ownerPlayerId: 'missing-player', cell: { x: 12, y: 9 } },
      ],
    };

    const model = buildGameBoardRenderModel({
      state,
      localPlayerId: 'player-1',
      tileAlpha: 0.2,
    });

    expect(model.snakes.map((snake) => snake.color)).toEqual([
      defaultSnakeColors[0],
      defaultSnakeColors[1],
    ]);
    expect(model.snakes[0].isLocal).toBe(true);
    expect(model.snakes[0].renderKey).toBe('snake:player-1');
    expect(model.snakes[0].segmentKeys).toEqual([
      'snake:player-1:segment:0',
      'snake:player-1:segment:1',
    ]);
    expect(model.food[0].renderKey).toBe('food:player-2:7:3');
    expect(model.food[0].color).toBe(defaultSnakeColors[1]);
    expect(model.food[1].color).toBe('#6b7280');
  });

  it('blends matching snake segments for visual-only rollback reconciliation', () => {
    const from = buildGameBoardRenderModel({
      state: {
        board: { width: 16, height: 12 },
        status: 'Running',
        snakes: [
          createSnake('player-1', 'Right', [
            { x: 4, y: 4 },
            { x: 3, y: 4 },
          ]),
        ],
        food: [],
      },
      localPlayerId: 'player-1',
      tileAlpha: 0,
    });
    const to = buildGameBoardRenderModel({
      state: {
        board: { width: 16, height: 12 },
        status: 'Running',
        snakes: [
          createSnake('player-1', 'Up', [
            { x: 4, y: 5 },
            { x: 4, y: 4 },
          ]),
        ],
        food: [],
      },
      localPlayerId: 'player-1',
      tileAlpha: 0,
    });

    const blended = blendGameBoardRenderModel(from, to, 0.5);

    expect(blended.snakes[0].segments).toEqual([
      { x: 4, y: 4.5 },
      { x: 3.5, y: 4 },
    ]);
  });
});

function createSnake(
  playerId: string,
  direction: AuthoritativeSnake['direction'],
  body: AuthoritativeSnake['body'],
): AuthoritativeSnake {
  return {
    playerId,
    alive: true,
    head: body[0] ?? { x: 0, y: 0 },
    direction,
    body,
  };
}
