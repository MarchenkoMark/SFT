import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class RoomSessionStorageService {
  getToken(roomId: string): string | null {
    return window.sessionStorage.getItem(this.key(roomId));
  }

  saveToken(roomId: string, token: string): void {
    window.sessionStorage.setItem(this.key(roomId), token);
  }

  clearToken(roomId: string): void {
    window.sessionStorage.removeItem(this.key(roomId));
  }

  private key(roomId: string): string {
    return `sft.room.${roomId.trim().toUpperCase()}.playerSessionToken`;
  }
}
