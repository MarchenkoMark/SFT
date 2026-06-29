import { AsyncPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { LobbyFacade } from './lobby.facade';

@Component({
  selector: 'app-join-route',
  imports: [AsyncPipe, RouterLink],
  template: `
    <main class="page">
      <section class="panel">
        <h1>Joining room</h1>
        @if (roomId) {
          <p>Room {{ roomId }}</p>
        } @else {
          <p class="error">Missing room code.</p>
          <a routerLink="/">Back</a>
        }

        @if (facade.vm$ | async; as vm) {
          <p class="muted">Connection: {{ vm.connectionStatus }}</p>
          @if (vm.lastError) {
            <p class="error" role="alert">{{ vm.lastError }}</p>
          }
        }
      </section>
    </main>
  `,
  styles: `
    .page {
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 24px;
    }

    .panel {
      width: min(100%, 420px);
      display: grid;
      gap: 12px;
    }

    h1,
    p {
      margin: 0;
    }

    .muted {
      color: #555;
    }

    .error {
      color: #9b1c1c;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JoinRouteComponent implements OnInit {
  roomId = '';

  constructor(
    readonly facade: LobbyFacade,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.roomId = this.route.snapshot.queryParamMap.get('id')?.trim() ?? '';
    if (!this.roomId) {
      void this.router.navigate(['/']);
      return;
    }

    this.facade.joinRoom(this.roomId);
  }
}
