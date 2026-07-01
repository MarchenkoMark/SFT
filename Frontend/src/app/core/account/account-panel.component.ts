import { AsyncPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';

import { AccountService } from './account.service';

@Component({
  selector: 'app-account-panel',
  imports: [AsyncPipe, ReactiveFormsModule],
  template: `
    @if (account.state$ | async; as state) {
      <section class="account-panel" aria-label="Account">
        @if (state.isAuthenticated && state.user) {
          <div class="identity">
            @if (state.user.pictureUrl) {
              <img [src]="state.user.pictureUrl" alt="" referrerpolicy="no-referrer" />
            } @else {
              <span class="avatar" aria-hidden="true">{{ state.user.username.slice(0, 1) }}</span>
            }
            <div class="identity-text">
              <span>Signed in</span>
              <strong>{{ state.user.username }}</strong>
            </div>
          </div>

          <form class="username-form" (ngSubmit)="saveUsername()">
            <label for="accountUsername">Username</label>
            <input
              id="accountUsername"
              type="text"
              autocomplete="nickname"
              placeholder="Choose a username"
              [formControl]="username"
            />
            <button
              type="button"
              [disabled]="username.invalid || state.isSavingUsername"
              (click)="saveUsername()"
            >
              Save
            </button>
          </form>

          <button type="button" class="secondary" (click)="account.logout()">Sign out</button>
        } @else {
          <span class="signed-out">Play anonymously or sign in for a leaderboard name.</span>
          <button type="button" (click)="account.login()" [disabled]="state.isLoading">
            Sign in with Google
          </button>
        }

        @if (state.error) {
          <p class="error" role="status">{{ state.error }}</p>
        } @else if (username.invalid && username.dirty) {
          <p class="hint" role="status">Use 3-24 letters, numbers, underscores, or hyphens.</p>
        }
      </section>
    }
  `,
  styles: `
    .account-panel {
      width: min(100% - 32px, 900px);
      margin: 12px auto 0;
      padding: 10px 12px;
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      align-items: center;
      gap: 10px;
      color: #17211f;
      background: #ffffff;
      border: 1px solid #d7dfdc;
      border-radius: 8px;
    }

    .identity {
      min-width: 0;
      display: flex;
      align-items: center;
      gap: 8px;
    }

    img,
    .avatar {
      width: 32px;
      height: 32px;
      flex: 0 0 auto;
      border-radius: 50%;
      background: #234940;
      color: #ffffff;
      display: grid;
      place-items: center;
      font-weight: 800;
      text-transform: uppercase;
    }

    .identity-text {
      min-width: 0;
      display: grid;
      gap: 1px;
    }

    .identity-text span,
    .signed-out,
    .hint,
    .error {
      color: #586560;
      font-size: 0.82rem;
    }

    .identity-text strong {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .username-form {
      min-width: 0;
      display: grid;
      grid-template-columns: auto minmax(120px, 180px) auto;
      align-items: center;
      gap: 6px;
    }

    label {
      color: #586560;
      font-size: 0.82rem;
    }

    input {
      min-width: 0;
      min-height: 34px;
      padding: 0 10px;
      color: #17211f;
      border: 1px solid #b8c5c0;
      border-radius: 6px;
      font: inherit;
    }

    button {
      min-height: 34px;
      padding: 0 12px;
      color: #ffffff;
      background: #234940;
      border: 1px solid #234940;
      border-radius: 6px;
      font: inherit;
      font-size: 0.88rem;
      cursor: pointer;
      white-space: nowrap;
    }

    button:hover:not(:disabled) {
      background: #17211f;
      border-color: #17211f;
    }

    button:disabled {
      cursor: not-allowed;
      opacity: 0.55;
    }

    .secondary {
      color: #234940;
      background: #ffffff;
      border-color: #b8c5c0;
    }

    .hint,
    .error {
      grid-column: 1 / -1;
      margin: 0;
    }

    .error {
      color: #7a2020;
    }

    @media (min-width: 760px) {
      .account-panel {
        grid-template-columns: minmax(0, 1fr) auto auto;
      }
    }

    @media (max-width: 720px) {
      .account-panel,
      .username-form {
        grid-template-columns: 1fr;
      }

      button,
      input {
        width: 100%;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountPanelComponent {
  private lastSyncedUsername: string | null = null;
  private pendingSavedUsername: string | null = null;
  readonly account = inject(AccountService);
  readonly username = new FormControl('', {
    nonNullable: true,
    validators: [
      Validators.required,
      Validators.minLength(3),
      Validators.maxLength(24),
      Validators.pattern(/^[A-Za-z0-9][A-Za-z0-9_-]{2,23}$/),
    ],
  });

  constructor() {
    this.account.load();
    this.account.state$
      .pipe(takeUntilDestroyed())
      .subscribe((state) => {
        if (!state.user || state.isSavingUsername) {
          return;
        }

        const formUsername = state.user.hasCustomUsername ? state.user.username : '';
        if (
          formUsername !== this.lastSyncedUsername &&
          (!this.username.dirty || this.pendingSavedUsername === formUsername)
        ) {
          this.lastSyncedUsername = formUsername;
          this.pendingSavedUsername = null;
          this.username.setValue(formUsername, { emitEvent: false });
          this.username.markAsPristine();
        }
      });
  }

  saveUsername(): void {
    if (this.username.invalid) {
      this.username.markAsDirty();
      return;
    }

    const username = this.username.value.trim();
    this.pendingSavedUsername = username;
    this.account.updateUsername(username);
  }
}
