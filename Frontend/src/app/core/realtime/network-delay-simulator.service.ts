import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export interface NetworkDelaySettings {
  clientToServerMs: number;
  serverToClientMs: number;
  jitterMs: number;
}

export const defaultNetworkDelaySettings: NetworkDelaySettings = {
  clientToServerMs: 0,
  serverToClientMs: 0,
  jitterMs: 0,
};

export const networkDelayLimits = {
  oneWayDelayMaxMs: 3000,
  jitterMaxMs: 1000,
  stepMs: 25,
} as const;

const storageKey = 'snakeForTwo.networkDelaySettings';

@Injectable({ providedIn: 'root' })
export class NetworkDelaySimulatorService {
  private readonly settingsSubject = new BehaviorSubject<NetworkDelaySettings>(this.loadSettings());

  readonly settings$: Observable<NetworkDelaySettings> = this.settingsSubject.asObservable();

  get snapshot(): NetworkDelaySettings {
    return this.settingsSubject.value;
  }

  update(settings: Partial<NetworkDelaySettings>): void {
    const next = normalizeSettings({
      ...this.snapshot,
      ...settings,
    });

    this.settingsSubject.next(next);
    this.saveSettings(next);
  }

  reset(): void {
    this.update(defaultNetworkDelaySettings);
  }

  clientToServerDelayMs(): number {
    return this.sampleDelay(this.snapshot.clientToServerMs, this.snapshot.jitterMs);
  }

  serverToClientDelayMs(): number {
    return this.sampleDelay(this.snapshot.serverToClientMs, this.snapshot.jitterMs);
  }

  private sampleDelay(baseDelayMs: number, jitterMs: number): number {
    if (baseDelayMs === 0 && jitterMs === 0) {
      return 0;
    }

    const jitterOffsetMs = jitterMs === 0 ? 0 : (Math.random() * 2 - 1) * jitterMs;
    return Math.max(0, Math.round(baseDelayMs + jitterOffsetMs));
  }

  private loadSettings(): NetworkDelaySettings {
    const storage = getSessionStorage();
    if (!storage) {
      return defaultNetworkDelaySettings;
    }

    try {
      const raw = storage.getItem(storageKey);
      return raw
        ? normalizeSettings({
            ...defaultNetworkDelaySettings,
            ...(JSON.parse(raw) as Partial<NetworkDelaySettings>),
          })
        : defaultNetworkDelaySettings;
    } catch {
      return defaultNetworkDelaySettings;
    }
  }

  private saveSettings(settings: NetworkDelaySettings): void {
    try {
      getSessionStorage()?.setItem(storageKey, JSON.stringify(settings));
    } catch {
      // Some browser privacy modes can reject sessionStorage writes.
    }
  }
}

function normalizeSettings(settings: Partial<NetworkDelaySettings>): NetworkDelaySettings {
  return {
    clientToServerMs: normalizeDelayMs(
      settings.clientToServerMs,
      networkDelayLimits.oneWayDelayMaxMs,
    ),
    serverToClientMs: normalizeDelayMs(
      settings.serverToClientMs,
      networkDelayLimits.oneWayDelayMaxMs,
    ),
    jitterMs: normalizeDelayMs(settings.jitterMs, networkDelayLimits.jitterMaxMs),
  };
}

function normalizeDelayMs(value: unknown, maxMs: number): number {
  const numericValue = typeof value === 'number' ? value : Number(value);
  if (!Number.isFinite(numericValue)) {
    return 0;
  }

  return Math.min(maxMs, Math.max(0, Math.round(numericValue)));
}

function getSessionStorage(): Storage | null {
  try {
    return typeof globalThis.sessionStorage === 'undefined' ? null : globalThis.sessionStorage;
  } catch {
    return null;
  }
}
