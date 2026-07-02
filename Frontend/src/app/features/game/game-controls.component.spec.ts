import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ClockSyncService } from '../../core/clock/clock-sync.service';
import { RenderDiagnosticsReporterService } from './diagnostics/render-diagnostics-reporter.service';
import { RenderDiagnosticsSettingsService } from './diagnostics/render-diagnostics-settings.service';
import { PlayerInputFacade } from './input/player-input.facade';
import { GameBoardComponent } from './render/game-board.component';
import { GameFieldRenderer } from './render/game-field-renderer';
import { GameSessionQuery } from './state/game-session.query';
import { RenderFrameHistoryStore } from './state/render-frame-history.store';
import { GameControlsComponent } from './game-controls.component';

describe('GameControlsComponent', () => {
  let fixture: ComponentFixture<GameControlsComponent>;
  let sendDirection: ReturnType<typeof vi.fn>;
  let boardRenderer: {
    mount: ReturnType<typeof vi.fn>;
    resize: ReturnType<typeof vi.fn>;
    render: ReturnType<typeof vi.fn>;
    drawEmpty: ReturnType<typeof vi.fn>;
    destroy: ReturnType<typeof vi.fn>;
  };
  let requestAnimationFrameSpy: ReturnType<typeof vi.spyOn>;
  let cancelAnimationFrameSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(async () => {
    sendDirection = vi.fn();
    boardRenderer = {
      mount: vi.fn(),
      resize: vi.fn(),
      render: vi.fn(),
      drawEmpty: vi.fn(),
      destroy: vi.fn(),
    };
    requestAnimationFrameSpy = vi
      .spyOn(globalThis, 'requestAnimationFrame')
      .mockImplementation(() => 1);
    cancelAnimationFrameSpy = vi
      .spyOn(globalThis, 'cancelAnimationFrame')
      .mockImplementation(() => undefined);

    TestBed.configureTestingModule({
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
        { provide: RenderDiagnosticsReporterService, useValue: { sendPending: vi.fn() } },
        { provide: RenderDiagnosticsSettingsService, useValue: { enabled: false } },
        {
          provide: RenderFrameHistoryStore,
          useValue: {
            beginMatch: vi.fn(),
            recordFrame: vi.fn(),
            finishMatch: vi.fn(),
          },
        },
      ],
    });
    TestBed.overrideComponent(GameBoardComponent, {
      set: {
        providers: [{ provide: GameFieldRenderer, useValue: boardRenderer }],
      },
    });
    await TestBed.compileComponents();

    fixture = TestBed.createComponent(GameControlsComponent);
    fixture.componentInstance.enabled = true;
    fixture.detectChanges();
  });

  afterEach(() => {
    requestAnimationFrameSpy.mockRestore();
    cancelAnimationFrameSpy.mockRestore();
  });

  it('renders the board renderer host', () => {
    expect(fixture.nativeElement.querySelector('.game-board-renderer')).toBeTruthy();
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
