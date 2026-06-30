import { appSettings } from '../../app.settings';

const defaultBackendPort = '5012';

export function resolveRealtimeUrl(): string {
  const override = window.localStorage.getItem('sft.wsUrl');
  if (override && override.trim().length > 0) {
    return override.trim();
  }

  if (isLocalFrontendHost(window.location.hostname)) {
    return `ws://localhost:${defaultBackendPort}/ws`;
  }

  if (isPrivateNetworkHost(window.location.hostname)) {
    return `ws://${window.location.hostname}:${defaultBackendPort}/ws`;
  }

  if (appSettings.productionWsUrl.trim().length > 0) {
    return appSettings.productionWsUrl.trim();
  }

  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
  return `${protocol}//${window.location.hostname}:${defaultBackendPort}/ws`;
}

function isLocalFrontendHost(hostname: string): boolean {
  return hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '::1';
}

function isPrivateNetworkHost(hostname: string): boolean {
  if (hostname.startsWith('192.168.') || hostname.startsWith('10.')) {
    return true;
  }

  const match = /^172\.(\d{1,2})\./.exec(hostname);
  if (!match) {
    return false;
  }

  const secondOctet = Number(match[1]);
  return secondOctet >= 16 && secondOctet <= 31;
}
