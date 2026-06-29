import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';

import { GameResult } from '../game/state/game-session.store';

@Component({
  selector: 'app-game-over-dialog',
  template: `
    @if (open && result) {
      <div class="backdrop" role="presentation">
        <section class="dialog" role="dialog" aria-modal="true" aria-labelledby="gameOverTitle">
          <h2 id="gameOverTitle">Game over</h2>
          <p>{{ result.result }}</p>
          <p class="muted">{{ result.reason }}</p>
          <div class="actions">
            <button type="button" (click)="ready.emit()">Ready for rematch</button>
            <button type="button" (click)="dismiss.emit()">Dismiss</button>
          </div>
        </section>
      </div>
    }
  `,
  styles: `
    .backdrop {
      position: fixed;
      inset: 0;
      display: grid;
      place-items: center;
      padding: 24px;
      background: rgb(0 0 0 / 0.24);
    }

    .dialog {
      width: min(100%, 360px);
      display: grid;
      gap: 12px;
      padding: 18px;
      background: white;
      border: 1px solid #bbb;
      box-shadow: 0 8px 24px rgb(0 0 0 / 0.16);
    }

    h2,
    p {
      margin: 0;
    }

    .muted {
      color: #555;
    }

    .actions {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
    }

    button {
      min-height: 40px;
      font: inherit;
      padding: 0 14px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GameOverDialogComponent {
  @Input() open = false;
  @Input() result: GameResult | null = null;
  @Output() readonly ready = new EventEmitter<void>();
  @Output() readonly dismiss = new EventEmitter<void>();
}
