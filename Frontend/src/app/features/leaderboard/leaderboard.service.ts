import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { resolveBackendApiUrl } from '../../core/http/backend-api-url';
import { LeaderboardResponse, LeaderboardWindow } from './leaderboard.models';

@Injectable({ providedIn: 'root' })
export class LeaderboardService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = resolveBackendApiUrl();

  getLeaderboard(window: LeaderboardWindow, limit = 8): Observable<LeaderboardResponse> {
    const params = new HttpParams().set('window', window).set('limit', limit);
    return this.http.get<LeaderboardResponse>(`${this.apiUrl}/leaderboard`, { params });
  }
}
