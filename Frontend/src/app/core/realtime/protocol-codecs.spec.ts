import { decodeServerMessage } from './protocol-codecs';

describe('decodeServerMessage', () => {
  it('decodes roomCreated with the assigned player and room state', () => {
    const decoded = decodeServerMessage(
      JSON.stringify({
        type: 'roomCreated',
        roomId: 'ROOM1',
        playerId: 'player-1',
        playerSessionToken: 'private-token',
        room: {
          roomId: 'ROOM1',
          status: 'WaitingForPlayers',
          players: [
            {
              playerId: 'player-1',
              seat: 1,
              isConnected: true,
              isReady: false,
            },
          ],
          matchId: null,
        },
      }),
    );

    expect(decoded.ok).toBe(true);
    if (decoded.ok && decoded.message.type === 'roomCreated') {
      expect(decoded.message.type).toBe('roomCreated');
      expect(decoded.message.room.players[0].seat).toBe(1);
    }
  });

  it('decodes authoritative frames with object-oriented game state', () => {
    const decoded = decodeServerMessage(
      JSON.stringify({
        type: 'authoritativeFrame',
        roomId: 'ROOM1',
        matchId: 'match-1',
        tick: 42,
        serverTime: 1000,
        stateHash: 'abc123',
        state: {
          board: { width: 32, height: 24 },
          snakes: [
            {
              playerId: 'player-1',
              alive: true,
              head: { x: 10, y: 6 },
              direction: 'Right',
              body: [
                { x: 10, y: 6 },
                { x: 9, y: 6 },
              ],
            },
          ],
          food: [{ ownerPlayerId: 'player-1', cell: { x: 14, y: 6 } }],
          status: 'Running',
        },
      }),
    );

    expect(decoded.ok).toBe(true);
    if (decoded.ok && decoded.message.type === 'authoritativeFrame') {
      expect(decoded.message.tick).toBe(42);
      expect(decoded.message.state.snakes[0].direction).toBe('Right');
      expect(decoded.message.state.food[0].ownerPlayerId).toBe('player-1');
    }
  });

  it('decodes accepted turn intents with effective outgoing tick semantics', () => {
    const decoded = decodeServerMessage(
      JSON.stringify({
        type: 'turnIntentAccepted',
        roomId: 'ROOM1',
        matchId: 'match-1',
        playerId: 'player-2',
        direction: 'Up',
        effectiveTick: 12,
        clientTime: 1500,
        clientSequence: null,
        serverReceivedAt: 1610,
      }),
    );

    expect(decoded.ok).toBe(true);
    if (decoded.ok && decoded.message.type === 'turnIntentAccepted') {
      expect(decoded.message.playerId).toBe('player-2');
      expect(decoded.message.direction).toBe('Up');
      expect(decoded.message.effectiveTick).toBe(12);
      expect(decoded.message.clientSequence).toBeNull();
    }
  });

  it('ignores unknown message types without treating them as malformed known payloads', () => {
    const decoded = decodeServerMessage(JSON.stringify({ type: 'futureMessage' }));

    expect(decoded.ok).toBe(false);
    if (!decoded.ok) {
      expect(decoded.unknownType).toBe('futureMessage');
    }
  });

  it('rejects malformed known messages', () => {
    const decoded = decodeServerMessage(
      JSON.stringify({
        type: 'roomState',
        room: {
          roomId: 'ROOM1',
          status: 'NotAStatus',
          players: [],
          matchId: null,
        },
      }),
    );

    expect(decoded.ok).toBe(false);
    if (!decoded.ok) {
      expect(decoded.unknownType).toBeUndefined();
    }
  });
});
