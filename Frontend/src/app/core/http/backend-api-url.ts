import { appSettings } from '../../app.settings';

const defaultBackendPort = '5012';

export function resolveBackendApiUrl(): string {
  const override = window.localStorage.getItem('sft.apiUrl');
  if (override && override.trim().length > 0) {
    return stripTrailingSlash(override.trim());
  }

  if (isLocalFrontendHost(window.location.hostname)) {
    return `http://localhost:${defaultBackendPort}`;
  }

  if (isPrivateNetworkHost(window.location.hostname)) {
    return `http://${window.location.hostname}:${defaultBackendPort}`;
  }

  if (appSettings.productionApiUrl.trim().length > 0) {
    return stripTrailingSlash(appSettings.productionApiUrl.trim());
  }

  return `${window.location.protocol}//${window.location.hostname}`;
}

function stripTrailingSlash(value: string): string {
  return value.endsWith('/') ? value.slice(0, -1) : value;
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
