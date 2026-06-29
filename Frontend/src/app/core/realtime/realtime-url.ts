const defaultBackendPort = '5012';

export function resolveRealtimeUrl(): string {
  const override = window.localStorage.getItem('sft.wsUrl');
  if (override && override.trim().length > 0) {
    return override.trim();
  }

  if (isLocalFrontendHost(window.location.hostname)) {
    return `ws://localhost:${defaultBackendPort}/ws`;
  }

  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
  return `${protocol}//${window.location.host}/ws`;
}

function isLocalFrontendHost(hostname: string): boolean {
  return hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '::1';
}
