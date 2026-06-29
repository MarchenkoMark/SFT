import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ClockSyncService } from '../../core/clock/clock-sync.service';
import { PlayerInputFacade } from './input/player-input.facade';
import { CanvasGameRenderer } from './render/canvas-game-renderer.service';
import { GameSessionQuery } from './state/game-session.query';
import { GameControlsComponent } from './game-controls.component';

describe('GameControlsComponent', () => {
  let fixture: ComponentFixture<GameControlsComponent>;
  let sendDirection: ReturnType<typeof vi.fn>;
  let requestAnimationFrameSpy: ReturnType<typeof vi.spyOn>;
  let cancelAnimationFrameSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(async () => {
    sendDirection = vi.fn();
    requestAnimationFrameSpy = vi
      .spyOn(globalThis, 'requestAnimationFrame')
      .mockImplementation(() => 1);
    cancelAnimationFrameSpy = vi
      .spyOn(globalThis, 'cancelAnimationFrame')
      .mockImplementation(() => undefined);

    await TestBed.configureTestingModule({
      imports: [GameControlsComponent],
      providers: [
        { provide: PlayerInputFacade, useValue: { sendDirection } },
        { provide: ClockSyncService, useValue: { estimatedServerNow: () => 1000 } },
        {
          provide: GameSessionQuery,
          useValue: {
            snapshot: {
              latestFrame: null,
              timing: null,
              startServerTime: null,
              localPlayerId: null,
            },
          },
        },
        { provide: CanvasGameRenderer, useValue: { draw: vi.fn(), drawEmpty: vi.fn() } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(GameControlsComponent);
    fixture.componentInstance.enabled = true;
    fixture.detectChanges();
  });

  afterEach(() => {
    requestAnimationFrameSpy.mockRestore();
    cancelAnimationFrameSpy.mockRestore();
  });

  it('renders the canvas board surface', () => {
    expect(fixture.nativeElement.querySelector('canvas')).toBeTruthy();
  });

  it('sends WASD keyboard input for the local snake', () => {
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'w', code: 'KeyW' }));
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'd', code: 'KeyD' }));

    expect(sendDirection).toHaveBeenCalledWith('Up');
    expect(sendDirection).toHaveBeenCalledWith('Right');
  });

  it('uses physical WASD key codes when keyboard layout changes the key value', () => {
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'z', code: 'KeyW' }));

    expect(sendDirection).toHaveBeenCalledWith('Up');
  });
});
