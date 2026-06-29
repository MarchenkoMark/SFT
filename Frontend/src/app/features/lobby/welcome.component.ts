import { AsyncPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

import { LobbyFacade } from './lobby.facade';

@Component({
  selector: 'app-welcome',
  imports: [AsyncPipe, ReactiveFormsModule],
  template: `
    <main class="page">
      <section class="panel">
        <h1>SnakeForTwo</h1>

        @if (facade.vm$ | async; as vm) {
          <p class="muted">Connection: {{ vm.connectionStatus }}</p>
          @if (vm.lastError) {
            <p class="error" role="alert">{{ vm.lastError }}</p>
          }
        }

        <div class="actions">
          <button type="button" (click)="createRoom()">Create room</button>
        </div>

        <form [formGroup]="joinForm" (ngSubmit)="joinRoom()">
          <label for="roomId">Room code</label>
          <div class="join-row">
            <input id="roomId" type="text" formControlName="roomId" autocomplete="off" />
            <button type="submit" [disabled]="joinForm.invalid">Join</button>
          </div>
        </form>
      </section>
    </main>
  `,
  styleUrl: './welcome.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WelcomeComponent {
  readonly joinForm = new FormGroup({
    roomId: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  constructor(readonly facade: LobbyFacade) {}

  createRoom(): void {
    this.facade.createRoom();
  }

  joinRoom(): void {
    this.facade.openJoinRoute(this.joinForm.controls.roomId.value);
  }
}
