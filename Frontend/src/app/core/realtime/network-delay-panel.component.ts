import { AsyncPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import {
  NetworkDelaySettings,
  NetworkDelaySimulatorService,
  networkDelayLimits,
} from './network-delay-simulator.service';

@Component({
  selector: 'app-network-delay-panel',
  imports: [AsyncPipe],
  template: `
    @if (settings$ | async; as settings) {
      <section class="network-delay-panel" aria-label="Network delay simulator">
        <div class="panel-header">
          <strong>Network delay</strong>
          <button type="button" (click)="reset()">Reset</button>
        </div>

        <label class="control">
          <span class="control-header">
            <span>Client to server</span>
            <output>{{ settings.clientToServerMs }} ms</output>
          </span>
          <span class="control-row">
            <input
              type="range"
              min="0"
              [max]="limits.oneWayDelayMaxMs"
              [step]="limits.stepMs"
              [value]="settings.clientToServerMs"
              (input)="setValue($event, 'clientToServerMs')"
            />
            <input
              class="number-input"
              type="number"
              min="0"
              [max]="limits.oneWayDelayMaxMs"
              [step]="limits.stepMs"
              [value]="settings.clientToServerMs"
              (input)="setValue($event, 'clientToServerMs')"
              aria-label="Client to server delay in milliseconds"
            />
          </span>
        </label>

        <label class="control">
          <span class="control-header">
            <span>Server to client</span>
            <output>{{ settings.serverToClientMs }} ms</output>
          </span>
          <span class="control-row">
            <input
              type="range"
              min="0"
              [max]="limits.oneWayDelayMaxMs"
              [step]="limits.stepMs"
              [value]="settings.serverToClientMs"
              (input)="setValue($event, 'serverToClientMs')"
            />
            <input
              class="number-input"
              type="number"
              min="0"
              [max]="limits.oneWayDelayMaxMs"
              [step]="limits.stepMs"
              [value]="settings.serverToClientMs"
              (input)="setValue($event, 'serverToClientMs')"
              aria-label="Server to client delay in milliseconds"
            />
          </span>
        </label>

        <label class="control">
          <span class="control-header">
            <span>Jitter</span>
            <output>+/- {{ settings.jitterMs }} ms</output>
          </span>
          <span class="control-row">
            <input
              type="range"
              min="0"
              [max]="limits.jitterMaxMs"
              [step]="limits.stepMs"
              [value]="settings.jitterMs"
              (input)="setValue($event, 'jitterMs')"
            />
            <input
              class="number-input"
              type="number"
              min="0"
              [max]="limits.jitterMaxMs"
              [step]="limits.stepMs"
              [value]="settings.jitterMs"
              (input)="setValue($event, 'jitterMs')"
              aria-label="Jitter in milliseconds"
            />
          </span>
        </label>
      </section>
    }
  `,
  styles: `
    .network-delay-panel {
      width: min(100% - 32px, 900px);
      margin: 12px auto 0;
      padding: 12px;
      display: grid;
      gap: 10px;
      color: #111827;
      background: #f7f8fa;
      border: 1px solid #d8dee8;
      border-radius: 8px;
    }

    .panel-header,
    .control-header,
    .control-row {
      display: flex;
      align-items: center;
      gap: 10px;
    }

    .panel-header,
    .control-header {
      justify-content: space-between;
    }

    .control {
      display: grid;
      gap: 6px;
      font-size: 0.9rem;
    }

    .control-row {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 92px;
    }

    input,
    button {
      min-height: 34px;
      font: inherit;
    }

    input[type='range'] {
      width: 100%;
      accent-color: #25636f;
    }

    .number-input {
      width: 100%;
      padding: 0 8px;
    }

    button {
      padding: 0 10px;
      color: #111827;
      background: #ffffff;
      border: 1px solid #b8c2cf;
      border-radius: 6px;
      cursor: pointer;
    }

    button:hover {
      background: #edf4f5;
    }

    output {
      color: #374151;
      font-variant-numeric: tabular-nums;
      white-space: nowrap;
    }

    @media (min-width: 760px) {
      .network-delay-panel {
        grid-template-columns: 160px repeat(3, minmax(0, 1fr));
        align-items: center;
      }

      .panel-header {
        display: grid;
        gap: 8px;
        justify-content: stretch;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NetworkDelayPanelComponent {
  private readonly networkDelay = inject(NetworkDelaySimulatorService);

  readonly limits = networkDelayLimits;
  readonly settings$ = this.networkDelay.settings$;

  setValue(event: Event, key: keyof NetworkDelaySettings): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement)) {
      return;
    }

    this.networkDelay.update({ [key]: target.valueAsNumber });
  }

  reset(): void {
    this.networkDelay.reset();
  }
}
