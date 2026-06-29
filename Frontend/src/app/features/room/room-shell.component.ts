import { AsyncPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { GameControlsComponent } from '../game/game-controls.component';
import { GameOverDialogComponent } from './game-over-dialog.component';
import { ReadyPanelComponent } from './ready-panel.component';
import { RoomFacade } from './room.facade';

@Component({
  selector: 'app-room-shell',
  imports: [AsyncPipe, GameControlsComponent, GameOverDialogComponent, ReadyPanelComponent],
  template: `
    <main class="room-page">
      @if (facade.vm$ | async; as vm) {
        <header class="room-header">
          <div>
            <h1>Room {{ vm.room?.roomId ?? roomId }}</h1>
            <p class="muted">Status: {{ vm.roomStatus }} | Connection: {{ vm.connectionStatus }}</p>
          </div>
          <button type="button" (click)="facade.leaveRoom()">Leave</button>
        </header>

        @if (vm.lastError) {
          <p class="error" role="alert">{{ vm.lastError }}</p>
        }

        @if (vm.inviteUrl) {
          <section class="invite">
            <label for="inviteUrl">Invite URL</label>
            <input id="inviteUrl" type="text" [value]="vm.inviteUrl" readonly />
          </section>
        }

        <app-ready-panel
          [players]="vm.players"
          [localPlayerId]="vm.localPlayerId"
          [readyDisabled]="vm.readyDisabled"
          [readyLabel]="vm.readyLabel"
          (toggleReady)="facade.toggleReady()"
        />

        @if (vm.waitingForPlayers) {
          <p class="muted">Waiting for another player.</p>
        }

        @if (vm.countdownSeconds !== null) {
          <section class="status-panel" aria-live="polite">
            <h2>Starting in {{ vm.countdownSeconds }}</h2>
          </section>
        }

        @if (vm.gamePhase === 'running' || vm.gamePhase === 'finished') {
          <app-game-controls [enabled]="vm.isGameRunning" />
        }

        <app-game-over-dialog
          [open]="vm.showGameOver"
          [result]="vm.gameResult"
          (ready)="facade.readyForRematch()"
          (dismiss)="facade.dismissGameOver()"
        />
      }
    </main>
  `,
  styleUrl: './room-shell.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomShellComponent implements OnInit {
  roomId = '';

  constructor(
    readonly facade: RoomFacade,
    private readonly route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    this.roomId = this.route.snapshot.paramMap.get('roomId')?.trim() ?? '';
    this.facade.enterRoom(this.roomId);
  }
}
