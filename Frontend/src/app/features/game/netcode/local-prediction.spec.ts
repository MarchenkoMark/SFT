import { schedulePredictedInput, selectPredictedInputTargetTick } from './local-prediction';
import { LocalPredictionStore } from '../state/local-prediction.store';

describe('local prediction', () => {
  it('targets the current tick inside the input grace window', () => {
    expect(
      selectPredictedInputTargetTick({
        currentTick: 3,
        tileAlpha: 0.1,
        tickDurationMs: 500,
        inputGraceMs: 100,
      }),
    ).toBe(3);
  });

  it('targets the next tick after the input grace window', () => {
    expect(
      selectPredictedInputTargetTick({
        currentTick: 3,
        tileAlpha: 0.21,
        tickDurationMs: 500,
        inputGraceMs: 100,
      }),
    ).toBe(4);
  });

  it('queues legal predicted inputs in player order', () => {
    const first = schedulePredictedInput({
      pendingInputs: [],
      targetTick: 4,
      currentDirection: 'Right',
      requestedDirection: 'Up',
      clientTime: 1200,
      sequence: 1,
    });

    expect(first.accepted).toBe(true);
    if (!first.accepted) {
      return;
    }

    const second = schedulePredictedInput({
      pendingInputs: first.queue,
      targetTick: 4,
      currentDirection: 'Right',
      requestedDirection: 'Left',
      clientTime: 1210,
      sequence: 2,
    });

    expect(second.accepted).toBe(true);
    if (second.accepted) {
      expect(second.input).toEqual({
        targetTick: 5,
        direction: 'Left',
        clientTime: 1210,
        sequence: 2,
      });
    }
  });

  it('rejects direct reversals from the latest predicted direction', () => {
    const result = schedulePredictedInput({
      pendingInputs: [{ targetTick: 4, direction: 'Up', clientTime: 1200, sequence: 1 }],
      targetTick: 5,
      currentDirection: 'Right',
      requestedDirection: 'Down',
      clientTime: 1210,
      sequence: 2,
    });

    expect(result.accepted).toBe(false);
    if (!result.accepted) {
      expect(result.reason).toBe('directReversal');
    }
  });

  it('keeps an input until the authoritative frame after its target tick arrives', () => {
    const store = new LocalPredictionStore();
    store.setPendingInputs([{ targetTick: 2, direction: 'Up', clientTime: 1200, sequence: 1 }]);

    store.removeCoveredInputs(2);

    expect(store.snapshot).toEqual([
      { targetTick: 2, direction: 'Up', clientTime: 1200, sequence: 1 },
    ]);

    store.removeCoveredInputs(3);

    expect(store.snapshot).toEqual([]);
  });
});
