import { ChangeDetectionStrategy, Component, HostListener, Input } from '@angular/core';

import { Direction } from '../../core/realtime/protocol.models';
import { PlayerInputFacade } from './input/player-input.facade';
import { GameBoardComponent } from './render/game-board.component';

const keyDirections: Record<string, Direction> = {
  arrowup: 'Up',
  keyw: 'Up',
  w: 'Up',
  arrowright: 'Right',
  keyd: 'Right',
  d: 'Right',
  arrowdown: 'Down',
  keys: 'Down',
  s: 'Down',
  arrowleft: 'Left',
  keya: 'Left',
  a: 'Left',
};

@Component({
  selector: 'app-game-controls',
  imports: [GameBoardComponent],
  template: `
    <section class="game-shell" aria-label="Game">
      <app-game-board />

      <div class="dpad" aria-label="Movement controls">
        <span></span>
        <button type="button" [disabled]="!enabled" aria-label="Move up" (click)="send('Up')">
          W
        </button>
        <span></span>
        <button type="button" [disabled]="!enabled" aria-label="Move left" (click)="send('Left')">
          A
        </button>
        <button type="button" [disabled]="!enabled" aria-label="Move down" (click)="send('Down')">
          S
        </button>
        <button
          type="button"
          [disabled]="!enabled"
          aria-label="Move right"
          (click)="send('Right')"
        >
          D
        </button>
      </div>
    </section>
  `,
  styles: `
    .game-shell {
      display: grid;
      gap: 16px;
    }

    .dpad {
      width: min(100%, 220px);
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 8px;
      justify-self: center;
    }

    button {
      aspect-ratio: 1;
      min-height: 48px;
      font: inherit;
      font-weight: 700;
      color: #111827;
      background: #ffffff;
      border: 1px solid #cdd5df;
      border-radius: 8px;
      cursor: pointer;
    }

    button:disabled {
      color: #9ca3af;
      background: #f3f4f6;
      cursor: not-allowed;
    }

    button:not(:disabled):hover {
      background: #eef6ef;
      border-color: #6b8f7a;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GameControlsComponent {
  @Input() enabled = false;

  constructor(private readonly playerInput: PlayerInputFacade) {}

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    const direction = keyDirections[event.code.toLowerCase()] ?? keyDirections[event.key.toLowerCase()];
    if (!direction || !this.enabled) {
      return;
    }

    event.preventDefault();
    this.send(direction);
  }

  send(direction: Direction): void {
    this.playerInput.sendDirection(direction);
  }
}
