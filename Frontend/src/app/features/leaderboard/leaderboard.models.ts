export type LeaderboardWindow = 'daily' | 'monthly' | 'all-time';

export interface LeaderboardEntry {
  rank: number;
  matchId: string;
  mode: string;
  finishedAt: string;
  temporaryUserId: string;
  userId: string | null;
  playerId: string;
  displayName: string | null;
  seat: number;
  score: number;
  durationTicks: number;
  ownFoodEaten: number;
  teammateFoodEaten: number;
  foodEatenByTeammates: number;
  playerCount: number;
}

export interface LeaderboardResponse {
  window: LeaderboardWindow;
  entries: LeaderboardEntry[];
}
