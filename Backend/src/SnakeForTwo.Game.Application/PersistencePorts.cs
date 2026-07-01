namespace SnakeForTwo.Game.Application;

public interface IMatchSummaryWriter
{
    Task SaveAsync(MatchSummary summary, CancellationToken cancellationToken);
}

public interface IMatchSummaryQuery
{
    Task<IReadOnlyList<MatchSummaryListItem>> GetRecentMatchesAsync(
        int limit,
        CancellationToken cancellationToken);

    Task<MatchSummary?> GetMatchAsync(
        string matchId,
        CancellationToken cancellationToken);
}

public interface ILeaderboardQuery
{
    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
        LeaderboardWindow window,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public enum LeaderboardWindow
{
    Daily,
    Monthly,
    AllTime
}

public sealed record MatchSummary(
    string MatchId,
    string RoomId,
    string Mode,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    long DurationTicks,
    long DurationMs,
    string Result,
    string Reason,
    int PlayerCount,
    int BoardWidth,
    int BoardHeight,
    int Seed,
    string FinalStateHash,
    IReadOnlyList<MatchParticipantSummary> Participants);

public sealed record MatchParticipantSummary(
    Guid TemporaryUserId,
    Guid? UserId,
    string PlayerId,
    string? DisplayName,
    int Seat,
    bool Alive,
    int FinalLength,
    int OwnFoodEaten,
    int TeammateFoodEaten,
    int FoodEatenByTeammates,
    long Score);

public sealed record MatchSummaryListItem(
    string MatchId,
    string RoomId,
    string Mode,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    long DurationTicks,
    string Result,
    string Reason,
    int PlayerCount,
    IReadOnlyList<MatchParticipantSummary> Participants);

public sealed record LeaderboardEntry(
    int Rank,
    string MatchId,
    string Mode,
    DateTimeOffset FinishedAt,
    Guid TemporaryUserId,
    Guid? UserId,
    string PlayerId,
    string? DisplayName,
    int Seat,
    long Score,
    long DurationTicks,
    int OwnFoodEaten,
    int TeammateFoodEaten,
    int FoodEatenByTeammates,
    int PlayerCount);

public sealed record MatchFinishedEvent(
    MatchSummary Summary,
    DateTimeOffset OccurredAt) : GameEvent(Summary.MatchId, OccurredAt);
