import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, catchError, of, tap } from 'rxjs';

import { resolveBackendApiUrl } from '../http/backend-api-url';
import { AccountResponse, AccountState } from './account.models';

const initialState: AccountState = {
  isAuthenticated: false,
  user: null,
  isLoading: false,
  isSavingUsername: false,
  error: null,
};

@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = resolveBackendApiUrl();
  private readonly stateSubject = new BehaviorSubject<AccountState>(initialState);

  readonly state$ = this.stateSubject.asObservable();

  load(): void {
    this.patchState({ isLoading: true, error: null });
    this.http
      .get<AccountResponse>(`${this.apiUrl}/auth/me`, { withCredentials: true })
      .pipe(
        tap((response) =>
          this.patchState({
            isAuthenticated: response.isAuthenticated,
            user: response.user,
            isLoading: false,
            error: null,
          }),
        ),
        catchError(() => {
          this.patchState({
            isAuthenticated: false,
            user: null,
            isLoading: false,
            error: 'Sign-in is unavailable right now.',
          });
          return of(null);
        }),
      )
      .subscribe();
  }

  login(): void {
    const returnUrl = encodeURIComponent(window.location.href);
    window.location.assign(`${this.apiUrl}/auth/login/google?returnUrl=${returnUrl}`);
  }

  logout(): void {
    this.patchState({ isLoading: true, error: null });
    this.http
      .post<{ isAuthenticated: false }>(`${this.apiUrl}/auth/logout`, {}, { withCredentials: true })
      .pipe(
        tap(() =>
          this.patchState({
            isAuthenticated: false,
            user: null,
            isLoading: false,
            error: null,
          }),
        ),
        catchError(() => {
          this.patchState({
            isLoading: false,
            error: 'Sign out failed. Try again.',
          });
          return of(null);
        }),
      )
      .subscribe();
  }

  updateUsername(username: string): void {
    this.patchState({ isSavingUsername: true, error: null });
    this.http
      .put<AccountResponse>(
        `${this.apiUrl}/auth/me/username`,
        { username },
        { withCredentials: true },
      )
      .pipe(
        tap((response) =>
          this.patchState({
            isAuthenticated: response.isAuthenticated,
            user: response.user,
            isSavingUsername: false,
            error: null,
          }),
        ),
        catchError((error: HttpErrorResponse) => {
          this.patchState({
            isSavingUsername: false,
            error: readErrorMessage(error) ?? 'Username could not be updated.',
          });
          return of(null);
        }),
      )
      .subscribe();
  }

  private patchState(patch: Partial<AccountState>): void {
    this.stateSubject.next({
      ...this.stateSubject.value,
      ...patch,
    });
  }
}

function readErrorMessage(error: HttpErrorResponse): string | null {
  if (typeof error.error?.message === 'string') {
    return error.error.message;
  }

  return null;
}
