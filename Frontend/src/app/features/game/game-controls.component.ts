import { ChangeDetectionStrategy, Component, HostListener, Input } from '@angular/core';

import { Direction } from '../../core/realtime/protocol.models';
import { PlayerInputFacade } from './input/player-input.facade';

const keyDirections: Record<string, Direction> = {
  arrowup: 'Up',
  w: 'Up',
  arrowright: 'Right',
  d: 'Right',
  arrowdown: 'Down',
  s: 'Down',
  arrowleft: 'Left',
  a: 'Left',
};

@Component({
  selector: 'app-game-controls',
  template: `
    <section class="game-shell" aria-label="Game">
      <div class="board-placeholder">
        <p>Game board</p>
      </div>

      <div class="dpad" aria-label="Movement controls">
        <span></span>
        <button type="button" [disabled]="!enabled" (click)="send('Up')">Up</button>
        <span></span>
        <button type="button" [disabled]="!enabled" (click)="send('Left')">Left</button>
        <button type="button" [disabled]="!enabled" (click)="send('Down')">Down</button>
        <button type="button" [disabled]="!enabled" (click)="send('Right')">Right</button>
      </div>
    </section>
  `,
  styles: `
    .game-shell {
      display: grid;
      gap: 16px;
    }

    .board-placeholder {
      min-height: 280px;
      border: 1px solid #bbb;
      display: grid;
      place-items: center;
      background: #fafafa;
    }

    .board-placeholder p {
      margin: 0;
      color: #555;
    }

    .dpad {
      width: min(100%, 260px);
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 8px;
    }

    button {
      min-height: 40px;
      font: inherit;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GameControlsComponent {
  @Input() enabled = false;

  constructor(private readonly playerInput: PlayerInputFacade) {}

  @HostListener('window:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    const direction = keyDirections[event.key.toLowerCase()];
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
