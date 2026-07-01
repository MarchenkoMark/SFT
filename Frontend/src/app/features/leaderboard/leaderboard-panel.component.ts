import { AsyncPipe, DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { BehaviorSubject, Observable, catchError, combineLatest, map, of, shareReplay, startWith, switchMap } from 'rxjs';

import { LeaderboardEntry, LeaderboardWindow } from './leaderboard.models';
import { LeaderboardService } from './leaderboard.service';

interface LeaderboardViewModel {
  window: LeaderboardWindow;
  entries: LeaderboardEntry[];
  isLoading: boolean;
  error: string | null;
}

type LeaderboardLoadState =
  | { state: 'loading' }
  | { state: 'loaded'; entries: LeaderboardEntry[] }
  | { state: 'error'; message: string };

@Component({
  selector: 'app-leaderboard-panel',
  imports: [AsyncPipe, DatePipe, DecimalPipe],
  template: `
    @if (vm$ | async; as vm) {
      <section class="leaderboard-panel" aria-label="Leaderboard">
        <div class="panel-header">
          <div>
            <h2>Leaderboard</h2>
            <p>{{ windowLabel(vm.window) }}</p>
          </div>

          <div class="actions" aria-label="Leaderboard window">
            @for (window of windows; track window) {
              <button
                type="button"
                [class.active]="vm.window === window"
                (click)="setWindow(window)"
              >
                {{ shortWindowLabel(window) }}
              </button>
            }
            <button type="button" class="refresh" (click)="refresh()">Refresh</button>
          </div>
        </div>

        @if (vm.error) {
          <p class="message error" role="status">{{ vm.error }}</p>
        } @else if (vm.isLoading) {
          <p class="message" role="status">Loading leaderboard...</p>
        } @else if (vm.entries.length === 0) {
          <p class="message" role="status">No completed matches yet.</p>
        } @else {
          <ol class="entries">
            @for (entry of vm.entries; track entry.matchId + ':' + entry.playerId) {
              <li class="entry">
                <span class="rank">#{{ entry.rank }}</span>
                <span class="player">
                  <strong>{{ playerName(entry) }}</strong>
                  <small>{{ entry.mode }} · {{ entry.finishedAt | date: 'MMM d, HH:mm' }}</small>
                </span>
                <span class="score">{{ entry.score | number }}</span>
              </li>
            }
          </ol>
        }
      </section>
    }
  `,
  styles: `
    .leaderboard-panel {
      width: min(100% - 32px, 900px);
      margin: 12px auto 0;
      padding: 12px;
      display: grid;
      gap: 10px;
      color: #17211f;
      background: #f6f8f7;
      border: 1px solid #d7dfdc;
      border-radius: 8px;
    }

    .panel-header {
      display: grid;
      gap: 10px;
    }

    h2,
    p {
      margin: 0;
    }

    h2 {
      font-size: 1rem;
      font-weight: 750;
    }

    .panel-header p,
    small,
    .message {
      color: #586560;
      font-size: 0.82rem;
    }

    .actions {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 6px;
    }

    button {
      min-height: 34px;
      padding: 0 10px;
      color: #17211f;
      background: #ffffff;
      border: 1px solid #b8c5c0;
      border-radius: 6px;
      font: inherit;
      font-size: 0.85rem;
      cursor: pointer;
    }

    button:hover,
    button.active {
      background: #e6f2ee;
      border-color: #7ba094;
    }

    .refresh {
      color: #234940;
    }

    .entries {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 8px;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .entry {
      min-width: 0;
      display: grid;
      grid-template-columns: auto minmax(0, 1fr) auto;
      align-items: center;
      gap: 8px;
      padding: 8px;
      background: #ffffff;
      border: 1px solid #dde6e2;
      border-radius: 6px;
    }

    .rank,
    .score {
      color: #234940;
      font-variant-numeric: tabular-nums;
      white-space: nowrap;
    }

    .rank {
      font-weight: 750;
    }

    .score {
      font-weight: 700;
    }

    .player {
      min-width: 0;
      display: grid;
      gap: 2px;
    }

    .player strong,
    .player small {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .message {
      padding: 8px;
      background: #ffffff;
      border: 1px solid #dde6e2;
      border-radius: 6px;
    }

    .error {
      color: #7a2020;
      border-color: #e0b5b5;
      background: #fff6f6;
    }

    @media (min-width: 760px) {
      .leaderboard-panel {
        grid-template-columns: 220px minmax(0, 1fr);
        align-items: start;
      }

      .panel-header {
        align-content: start;
      }

      .message,
      .entries {
        grid-column: 2;
      }
    }

    @media (max-width: 700px) {
      .entries {
        grid-template-columns: 1fr;
      }

      .actions {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LeaderboardPanelComponent {
  private readonly leaderboard = inject(LeaderboardService);
  private readonly selectedWindowSubject = new BehaviorSubject<LeaderboardWindow>('daily');
  private readonly refreshSubject = new BehaviorSubject(0);

  readonly windows: LeaderboardWindow[] = ['daily', 'monthly', 'all-time'];

  readonly vm$: Observable<LeaderboardViewModel> = combineLatest([
    this.selectedWindowSubject,
    this.refreshSubject,
  ]).pipe(
    switchMap(([window]) =>
      this.leaderboard.getLeaderboard(window).pipe(
        map((response): LeaderboardLoadState => ({ state: 'loaded', entries: response.entries })),
        catchError((): Observable<LeaderboardLoadState> =>
          of({
            state: 'error',
            message: 'Leaderboard is unavailable right now.',
          }),
        ),
        startWith({ state: 'loading' } satisfies LeaderboardLoadState),
        map((loadState): LeaderboardViewModel => ({
          window,
          entries: loadState.state === 'loaded' ? loadState.entries : [],
          isLoading: loadState.state === 'loading',
          error: loadState.state === 'error' ? loadState.message : null,
        })),
      ),
    ),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  setWindow(window: LeaderboardWindow): void {
    this.selectedWindowSubject.next(window);
  }

  refresh(): void {
    this.refreshSubject.next(this.refreshSubject.value + 1);
  }

  windowLabel(window: LeaderboardWindow): string {
    switch (window) {
      case 'monthly':
        return 'Top players this month';
      case 'all-time':
        return 'All-time best scores';
      default:
        return 'Top players today';
    }
  }

  shortWindowLabel(window: LeaderboardWindow): string {
    switch (window) {
      case 'monthly':
        return 'Month';
      case 'all-time':
        return 'All-time';
      default:
        return 'Today';
    }
  }

  playerName(entry: LeaderboardEntry): string {
    if (entry.displayName?.trim()) {
      return entry.displayName.trim();
    }

    return `Player ${entry.playerId.slice(-6)}`;
  }
}
