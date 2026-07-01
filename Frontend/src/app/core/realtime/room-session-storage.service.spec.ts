import { RoomSessionStorageService } from './room-session-storage.service';

describe('RoomSessionStorageService', () => {
  let service: RoomSessionStorageService;

  beforeEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
    service = new RoomSessionStorageService();
  });

  afterEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
  });

  it('stores seat reclaim tokens in persistent browser storage', () => {
    service.saveToken(' room1 ', 'token-1');

    expect(window.localStorage.getItem('sft.room.ROOM1.playerSessionToken')).toBe('token-1');
    expect(service.getToken('ROOM1')).toBe('token-1');
  });

  it('migrates old per-tab tokens when a player returns in the same tab', () => {
    window.sessionStorage.setItem('sft.room.ROOM1.playerSessionToken', 'legacy-token');

    expect(service.getToken('ROOM1')).toBe('legacy-token');
    expect(window.localStorage.getItem('sft.room.ROOM1.playerSessionToken')).toBe(
      'legacy-token',
    );
    expect(window.sessionStorage.getItem('sft.room.ROOM1.playerSessionToken')).toBeNull();
  });

  it('clears both persistent and legacy token copies', () => {
    window.localStorage.setItem('sft.room.ROOM1.playerSessionToken', 'token-1');
    window.sessionStorage.setItem('sft.room.ROOM1.playerSessionToken', 'legacy-token');

    service.clearToken('room1');

    expect(service.getToken('ROOM1')).toBeNull();
  });
});
