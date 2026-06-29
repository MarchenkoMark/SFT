import { NetworkDelaySimulatorService } from './network-delay-simulator.service';

describe('NetworkDelaySimulatorService', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    sessionStorage.clear();
  });

  it('starts with no simulated delay', () => {
    const service = new NetworkDelaySimulatorService();

    expect(service.snapshot).toEqual({
      clientToServerMs: 0,
      serverToClientMs: 0,
      jitterMs: 0,
    });
    expect(service.clientToServerDelayMs()).toBe(0);
    expect(service.serverToClientDelayMs()).toBe(0);
  });

  it('persists settings in tab storage', () => {
    const service = new NetworkDelaySimulatorService();

    service.update({
      clientToServerMs: 125,
      serverToClientMs: 250,
      jitterMs: 50,
    });

    const reloadedService = new NetworkDelaySimulatorService();

    expect(reloadedService.snapshot).toEqual({
      clientToServerMs: 125,
      serverToClientMs: 250,
      jitterMs: 50,
    });
  });

  it('clamps settings to supported ranges', () => {
    const service = new NetworkDelaySimulatorService();

    service.update({
      clientToServerMs: -50,
      serverToClientMs: 10_000,
      jitterMs: Number.POSITIVE_INFINITY,
    });

    expect(service.snapshot).toEqual({
      clientToServerMs: 0,
      serverToClientMs: 3000,
      jitterMs: 0,
    });
  });

  it('samples jitter as bounded variance around the base delay', () => {
    const service = new NetworkDelaySimulatorService();
    service.update({
      clientToServerMs: 100,
      serverToClientMs: 200,
      jitterMs: 25,
    });
    vi.spyOn(Math, 'random').mockReturnValueOnce(1).mockReturnValueOnce(0);

    expect(service.clientToServerDelayMs()).toBe(125);
    expect(service.serverToClientDelayMs()).toBe(175);
  });
});
