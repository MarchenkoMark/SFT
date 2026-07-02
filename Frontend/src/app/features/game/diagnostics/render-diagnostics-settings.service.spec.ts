import { RenderDiagnosticsSettingsService } from './render-diagnostics-settings.service';

describe('RenderDiagnosticsSettingsService', () => {
  const originalUrl = window.location.href;

  afterEach(() => {
    sessionStorage.clear();
    window.history.pushState({}, '', originalUrl);
  });

  it('enables diagnostics from the query string and persists it for the tab', () => {
    setLocationSearch('?renderDiagnostics=1');

    const service = new RenderDiagnosticsSettingsService();

    expect(service.enabled).toBe(true);

    setLocationSearch('');
    const reloadedService = new RenderDiagnosticsSettingsService();

    expect(reloadedService.enabled).toBe(true);
  });

  it('clears the tab flag when renderDiagnostics is explicitly disabled', () => {
    sessionStorage.setItem('snakeForTwo.renderDiagnostics', '1');
    setLocationSearch('?renderDiagnostics=0');

    const service = new RenderDiagnosticsSettingsService();

    expect(service.enabled).toBe(false);
    expect(sessionStorage.getItem('snakeForTwo.renderDiagnostics')).toBeNull();
  });

  function setLocationSearch(search: string): void {
    window.history.pushState({}, '', `/room/ROOM${search}`);
  }
});
