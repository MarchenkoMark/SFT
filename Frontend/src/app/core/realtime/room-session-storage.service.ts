import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class RoomSessionStorageService {
  getToken(roomId: string): string | null {
    const key = this.key(roomId);
    const persistentToken = window.localStorage.getItem(key);
    if (persistentToken) {
      return persistentToken;
    }

    const legacySessionToken = window.sessionStorage.getItem(key);
    if (legacySessionToken) {
      this.saveToken(roomId, legacySessionToken);
      return legacySessionToken;
    }

    return null;
  }

  saveToken(roomId: string, token: string): void {
    const key = this.key(roomId);
    window.localStorage.setItem(key, token);
    window.sessionStorage.removeItem(key);
  }

  clearToken(roomId: string): void {
    const key = this.key(roomId);
    window.localStorage.removeItem(key);
    window.sessionStorage.removeItem(key);
  }

  private key(roomId: string): string {
    return `sft.room.${roomId.trim().toUpperCase()}.playerSessionToken`;
  }
}
