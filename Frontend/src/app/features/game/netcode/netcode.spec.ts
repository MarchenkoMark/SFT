import { AuthoritativeSnake, Board } from '../../../core/realtime/protocol.models';
import { scheduleLocalDirection } from './input-scheduler';
import { projectCellToCanvas, projectSnakeHead } from './projection-engine';
import { computeRenderPhase } from './render-clock';

describe('netcode helpers', () => {
  it('computes tile alpha from one server tick per tile', () => {
    const phase = computeRenderPhase({
      estimatedServerNow: 1250,
      matchStartServerTime: 1000,
      tickDurationMs: 500,
      animationFramesPerTile: 5,
    });

    expect(phase.currentTick).toBe(0);
    expect(phase.tileAlpha).toBeCloseTo(0.5);
    expect(phase.visualFrameIndex).toBe(2);
    expect(phase.quantizedTileAlpha).toBe(0.4);
  });

  it('projects rightward and upward snake movement from the authoritative anchor', () => {
    const right = createSnake('Right');
    const up = createSnake('Up');

    expect(projectSnakeHead(right, 0.4)).toEqual({ x: 10.4, y: 6 });
    expect(projectSnakeHead(up, 0.4)).toEqual({ x: 10, y: 6.4 });
  });

  it('inverts canonical Y only at the canvas boundary', () => {
    const board: Board = { width: 32, height: 24 };

    expect(projectCellToCanvas({ x: 10, y: 6.4 }, board, 20)).toEqual({
      x: 200,
      y: 332,
    });
  });

  it('rejects direct reversals but accepts a reversal through a queued intermediate turn', () => {
    const rejected = scheduleLocalDirection(41, 'Up', 'Down');
    expect(rejected.accepted).toBe(false);
    if (!rejected.accepted) {
      expect(rejected.reason).toBe('directReversal');
    }

    const first = scheduleLocalDirection(41, 'Up', 'Left');
    expect(first.accepted).toBe(true);

    if (first.accepted) {
      const second = scheduleLocalDirection(41, 'Up', 'Down', first.queue);

      expect(second.accepted).toBe(true);
      if (second.accepted) {
        expect(second.input).toEqual({ targetTick: 43, direction: 'Down' });
      }
    }
  });
});

function createSnake(direction: AuthoritativeSnake['direction']): AuthoritativeSnake {
  return {
    playerId: 'player-1',
    alive: true,
    head: { x: 10, y: 6 },
    direction,
    body: [
      { x: 10, y: 6 },
      { x: 9, y: 6 },
    ],
  };
}
