import { schedulePredictedInput, selectPredictedInputEffectiveTick } from './local-prediction';
import { LocalPredictionStore } from '../state/local-prediction.store';

describe('local prediction', () => {
  it('targets the current tick inside the input grace window', () => {
    expect(
      selectPredictedInputEffectiveTick({
        currentTick: 3,
        tileAlpha: 0.1,
        tickDurationMs: 500,
        inputGraceMs: 100,
      }),
    ).toBe(3);
  });

  it('targets the next tick after the input grace window', () => {
    expect(
      selectPredictedInputEffectiveTick({
        currentTick: 3,
        tileAlpha: 0.21,
        tickDurationMs: 500,
        inputGraceMs: 100,
      }),
    ).toBe(4);
  });

  it('queues legal predicted inputs in player order', () => {
    const first = schedulePredictedInput({
      playerId: 'player-1',
      pendingInputs: [],
      effectiveTick: 4,
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
      playerId: 'player-1',
      pendingInputs: first.queue,
      effectiveTick: 4,
      currentDirection: 'Right',
      requestedDirection: 'Left',
      clientTime: 1210,
      sequence: 2,
    });

    expect(second.accepted).toBe(true);
    if (second.accepted) {
      expect(second.input).toEqual({
        playerId: 'player-1',
        effectiveTick: 5,
        direction: 'Left',
        clientTime: 1210,
        sequence: 2,
        source: 'local',
      });
    }
  });

  it('rejects direct reversals from the latest predicted direction', () => {
    const result = schedulePredictedInput({
      playerId: 'player-1',
      pendingInputs: [
        {
          playerId: 'player-1',
          effectiveTick: 4,
          direction: 'Up',
          clientTime: 1200,
          sequence: 1,
          source: 'local',
        },
      ],
      effectiveTick: 5,
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

  it('keeps an input until the authoritative frame after its effective tick arrives', () => {
    const store = new LocalPredictionStore();
    store.setPendingInputs([
      {
        playerId: 'player-1',
        effectiveTick: 2,
        direction: 'Up',
        clientTime: 1200,
        sequence: 1,
        source: 'local',
      },
    ]);

    store.removeCoveredInputs(2);

    expect(store.snapshot).toEqual([
      {
        playerId: 'player-1',
        effectiveTick: 2,
        direction: 'Up',
        clientTime: 1200,
        sequence: 1,
        source: 'local',
      },
    ]);

    store.removeCoveredInputs(3);

    expect(store.snapshot).toEqual([]);
  });

  it('replaces a matching local optimistic input when the server accepts it', () => {
    const store = new LocalPredictionStore();
    store.setPendingInputs([
      {
        playerId: 'player-1',
        effectiveTick: 2,
        direction: 'Up',
        clientTime: 1200,
        sequence: 1,
        source: 'local',
      },
    ]);

    store.acceptServerIntent({
      type: 'turnIntentAccepted',
      roomId: 'room-1',
      matchId: 'match-1',
      playerId: 'player-1',
      direction: 'Up',
      effectiveTick: 3,
      clientTime: 1200,
      clientSequence: 1,
      serverReceivedAt: 1300,
    });

    expect(store.snapshot).toEqual([
      {
        playerId: 'player-1',
        effectiveTick: 3,
        direction: 'Up',
        clientTime: 1200,
        sequence: 1,
        source: 'server',
      },
    ]);
  });
});
