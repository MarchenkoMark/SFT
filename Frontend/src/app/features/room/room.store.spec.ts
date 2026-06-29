import { RoomCreatedMessage, RoomState } from '../../core/realtime/protocol.models';
import { RoomStore } from './room.store';

describe('RoomStore', () => {
  it('stores room assignment, local player id, and invite metadata', () => {
    const store = new RoomStore();
    const message: RoomCreatedMessage = {
      type: 'roomCreated',
      roomId: 'ROOM1',
      playerId: 'player-1',
      playerSessionToken: 'private-token',
      room: createRoomState(),
    };

    store.acceptAssignment(message, 'http://localhost:4200/join?id=ROOM1');

    expect(store.getValue()).toEqual({
      room: message.room,
      localPlayerId: 'player-1',
      hasPlayerSessionToken: true,
      inviteUrl: 'http://localhost:4200/join?id=ROOM1',
      pendingReady: false,
      lastError: null,
    });
  });

  it('clears pending ready when authoritative room state arrives', () => {
    const store = new RoomStore();
    store.setPendingReady(true);

    store.setRoom({
      ...createRoomState(),
      status: 'ReadyCheck',
      players: [
        { playerId: 'player-1', seat: 1, isConnected: true, isReady: true },
        { playerId: 'player-2', seat: 2, isConnected: true, isReady: false },
      ],
    });

    expect(store.getValue().pendingReady).toBe(false);
    expect(store.getValue().room?.players[0].isReady).toBe(true);
  });

  it('records server errors and releases pending ready', () => {
    const store = new RoomStore();
    store.setPendingReady(true);

    store.setRoomError({
      type: 'error',
      code: 'roomWaitingForPlayers',
      message: 'Ready state can only be changed after both seats are reserved.',
      roomId: 'ROOM1',
    });

    expect(store.getValue().pendingReady).toBe(false);
    expect(store.getValue().lastError?.code).toBe('roomWaitingForPlayers');
  });
});

function createRoomState(): RoomState {
  return {
    roomId: 'ROOM1',
    status: 'WaitingForPlayers',
    players: [{ playerId: 'player-1', seat: 1, isConnected: true, isReady: false }],
    matchId: null,
  };
}
