import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';

import { RoomPlayer } from '../../core/realtime/protocol.models';

@Component({
  selector: 'app-ready-panel',
  template: `
    <section class="ready-panel" aria-label="Players">
      <h2>Players</h2>
      <ul>
        @for (player of players; track player.playerId) {
          <li [class.local]="player.playerId === localPlayerId">
            <span>Seat {{ player.seat }}</span>
            <span>{{ player.isConnected ? 'Connected' : 'Disconnected' }}</span>
            <span>{{ player.isReady ? 'Ready' : 'Not ready' }}</span>
          </li>
        } @empty {
          <li>No players yet</li>
        }
      </ul>

      <button type="button" [disabled]="readyDisabled" (click)="toggleReady.emit()">
        {{ readyLabel }}
      </button>
    </section>
  `,
  styles: `
    .ready-panel {
      display: grid;
      gap: 12px;
    }

    h2 {
      margin: 0;
      font-size: 1.1rem;
    }

    ul {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: 8px;
    }

    li {
      display: grid;
      grid-template-columns: 1fr 1fr 1fr;
      gap: 8px;
      padding: 8px;
      border: 1px solid #ddd;
    }

    li.local {
      border-color: #333;
    }

    button {
      min-height: 40px;
      width: fit-content;
      font: inherit;
      padding: 0 14px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReadyPanelComponent {
  @Input({ required: true }) players: RoomPlayer[] = [];
  @Input() localPlayerId: string | null = null;
  @Input() readyDisabled = true;
  @Input() readyLabel = 'Ready';
  @Output() readonly toggleReady = new EventEmitter<void>();
}
