import { Injectable } from '@angular/core';

const storageKey = 'snakeForTwo.renderDiagnostics';

@Injectable({ providedIn: 'root' })
export class RenderDiagnosticsSettingsService {
  private enabledValue = false;

  constructor() {
    this.refreshFromCurrentUrl();
  }

  get enabled(): boolean {
    return this.enabledValue;
  }

  refreshFromCurrentUrl(): void {
    const queryValue = this.readQueryFlag();

    if (queryValue === true) {
      this.enabledValue = true;
      this.writeSessionFlag(true);
      return;
    }

    if (queryValue === false) {
      this.enabledValue = false;
      this.writeSessionFlag(false);
      return;
    }

    this.enabledValue = this.readSessionFlag();
  }

  private readQueryFlag(): boolean | null {
    if (typeof window === 'undefined') {
      return null;
    }

    const value = new URLSearchParams(window.location.search).get('renderDiagnostics');
    if (value === null) {
      return null;
    }

    const normalizedValue = value.toLowerCase();
    if (normalizedValue === '1' || normalizedValue === 'true') {
      return true;
    }

    if (normalizedValue === '0' || normalizedValue === 'false') {
      return false;
    }

    return null;
  }

  private readSessionFlag(): boolean {
    try {
      return globalThis.sessionStorage?.getItem(storageKey) === '1';
    } catch {
      return false;
    }
  }

  private writeSessionFlag(enabled: boolean): void {
    try {
      if (enabled) {
        globalThis.sessionStorage?.setItem(storageKey, '1');
      } else {
        globalThis.sessionStorage?.removeItem(storageKey);
      }
    } catch {
      // Some browser privacy modes can reject sessionStorage writes.
    }
  }
}
