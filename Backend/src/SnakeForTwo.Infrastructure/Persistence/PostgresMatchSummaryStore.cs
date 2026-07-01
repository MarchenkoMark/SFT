using Microsoft.EntityFrameworkCore;
using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Infrastructure.Persistence;

public sealed class PostgresMatchSummaryStore(SnakeForTwoDbContext dbContext) :
    IMatchSummaryWriter,
    IMatchSummaryQuery,
    ILeaderboardQuery
{
    public async Task SaveAsync(MatchSummary summary, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (await dbContext.MatchSummaries.AnyAsync(
                match => match.MatchId == summary.MatchId,
                cancellationToken))
        {
            return;
        }

        dbContext.MatchSummaries.Add(ToEntity(summary));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var alreadySaved = await dbContext.MatchSummaries.AnyAsync(
                match => match.MatchId == summary.MatchId,
                CancellationToken.None);
            if (!alreadySaved)
            {
                throw;
            }
        }
    }

    public async Task<IReadOnlyList<MatchSummaryListItem>> GetRecentMatchesAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var matches = await dbContext.MatchSummaries
            .AsNoTracking()
            .Include(match => match.Participants)
            .OrderByDescending(match => match.FinishedAt)
            .Take(ClampLimit(limit))
            .ToListAsync(cancellationToken);

        return matches.Select(ToListItem).ToArray();
    }

    public async Task<MatchSummary?> GetMatchAsync(
        string matchId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            return null;
        }

        var match = await dbContext.MatchSummaries
            .AsNoTracking()
            .Include(candidate => candidate.Participants)
            .SingleOrDefaultAsync(
                candidate => candidate.MatchId == matchId.Trim(),
                cancellationToken);

        return match is null ? null : ToSummary(match);
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
        LeaderboardWindow window,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var windowStart = WindowStart(window, now);
        var query = dbContext.MatchParticipants
            .AsNoTracking()
            .Where(participant => participant.Match != null);

        if (windowStart is not null)
        {
            query = query.Where(participant => participant.Match!.FinishedAt >= windowStart.Value);
        }

        var rows = await query
            .OrderByDescending(participant => participant.Score)
            .ThenByDescending(participant => participant.Match!.DurationTicks)
            .ThenBy(participant => participant.Match!.FinishedAt)
            .Take(ClampLimit(limit))
            .Select(participant => new
            {
                participant.TemporaryUserId,
                participant.PlayerId,
                participant.DisplayName,
                participant.Seat,
                participant.Score,
                participant.OwnFoodEaten,
                participant.TeammateFoodEaten,
                participant.FoodEatenByTeammates,
                MatchId = participant.Match!.MatchId,
                participant.Match.Mode,
                participant.Match.FinishedAt,
                participant.Match.DurationTicks,
                participant.Match.PlayerCount
            })
            .ToListAsync(cancellationToken);

        return rows.Select((row, index) => new LeaderboardEntry(
            index + 1,
            row.MatchId,
            row.Mode,
            row.FinishedAt,
            row.TemporaryUserId,
            row.PlayerId,
            row.DisplayName,
            row.Seat,
            row.Score,
            row.DurationTicks,
            row.OwnFoodEaten,
            row.TeammateFoodEaten,
            row.FoodEatenByTeammates,
            row.PlayerCount)).ToArray();
    }

    private static MatchSummaryEntity ToEntity(MatchSummary summary)
    {
        var entity = new MatchSummaryEntity
        {
            Id = Guid.NewGuid(),
            MatchId = summary.MatchId,
            RoomId = summary.RoomId,
            Mode = summary.Mode,
            StartedAt = summary.StartedAt,
            FinishedAt = summary.FinishedAt,
            DurationTicks = summary.DurationTicks,
            DurationMs = summary.DurationMs,
            Result = summary.Result,
            Reason = summary.Reason,
            PlayerCount = summary.PlayerCount,
            BoardWidth = summary.BoardWidth,
            BoardHeight = summary.BoardHeight,
            Seed = summary.Seed,
            FinalStateHash = summary.FinalStateHash,
            CreatedAt = DateTimeOffset.UtcNow
        };

        entity.Participants.AddRange(summary.Participants.Select(participant => new MatchParticipantEntity
        {
            Id = Guid.NewGuid(),
            MatchSummaryId = entity.Id,
            TemporaryUserId = participant.TemporaryUserId,
            PlayerId = participant.PlayerId,
            DisplayName = participant.DisplayName,
            Seat = participant.Seat,
            Alive = participant.Alive,
            FinalLength = participant.FinalLength,
            OwnFoodEaten = participant.OwnFoodEaten,
            TeammateFoodEaten = participant.TeammateFoodEaten,
            FoodEatenByTeammates = participant.FoodEatenByTeammates,
            Score = participant.Score
        }));

        return entity;
    }

    private static MatchSummary ToSummary(MatchSummaryEntity entity) =>
        new(
            entity.MatchId,
            entity.RoomId,
            entity.Mode,
            entity.StartedAt,
            entity.FinishedAt,
            entity.DurationTicks,
            entity.DurationMs,
            entity.Result,
            entity.Reason,
            entity.PlayerCount,
            entity.BoardWidth,
            entity.BoardHeight,
            entity.Seed,
            entity.FinalStateHash,
            entity.Participants
                .OrderBy(participant => participant.Seat)
                .Select(ToSummary)
                .ToArray());

    private static MatchSummaryListItem ToListItem(MatchSummaryEntity entity) =>
        new(
            entity.MatchId,
            entity.RoomId,
            entity.Mode,
            entity.StartedAt,
            entity.FinishedAt,
            entity.DurationTicks,
            entity.Result,
            entity.Reason,
            entity.PlayerCount,
            entity.Participants
                .OrderBy(participant => participant.Seat)
                .Select(ToSummary)
                .ToArray());

    private static MatchParticipantSummary ToSummary(MatchParticipantEntity entity) =>
        new(
            entity.TemporaryUserId,
            entity.PlayerId,
            entity.DisplayName,
            entity.Seat,
            entity.Alive,
            entity.FinalLength,
            entity.OwnFoodEaten,
            entity.TeammateFoodEaten,
            entity.FoodEatenByTeammates,
            entity.Score);

    private static DateTimeOffset? WindowStart(LeaderboardWindow window, DateTimeOffset now)
    {
        var utcNow = now.ToUniversalTime();
        return window switch
        {
            LeaderboardWindow.Daily => new DateTimeOffset(
                utcNow.Year,
                utcNow.Month,
                utcNow.Day,
                0,
                0,
                0,
                TimeSpan.Zero),
            LeaderboardWindow.Monthly => new DateTimeOffset(
                utcNow.Year,
                utcNow.Month,
                1,
                0,
                0,
                0,
                TimeSpan.Zero),
            LeaderboardWindow.AllTime => null,
            _ => null
        };
    }

    private static int ClampLimit(int limit) => Math.Clamp(limit, 1, 100);
}
