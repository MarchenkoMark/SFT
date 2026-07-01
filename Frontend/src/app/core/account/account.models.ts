export interface AccountUser {
  userId: string;
  username: string;
  email: string | null;
  pictureUrl: string | null;
}

export interface AccountResponse {
  isAuthenticated: boolean;
  user: AccountUser | null;
}

export interface AccountState extends AccountResponse {
  isLoading: boolean;
  isSavingUsername: boolean;
  error: string | null;
}
