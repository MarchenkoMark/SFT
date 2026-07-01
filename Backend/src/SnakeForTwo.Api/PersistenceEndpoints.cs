using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Api;

internal static class PersistenceEndpoints
{
    public static IEndpointRouteBuilder MapPersistenceEndpoints(
        this IEndpointRouteBuilder endpoints,
        bool persistenceEnabled)
    {
        if (!persistenceEnabled)
        {
            endpoints.MapGet("/leaderboard", PersistenceDisabled);
            endpoints.MapGet("/matches", PersistenceDisabled);
            endpoints.MapGet("/matches/{matchId}", PersistenceDisabled);
            return endpoints;
        }

        endpoints.MapGet("/leaderboard", GetLeaderboardAsync);
        endpoints.MapGet("/matches", GetMatchesAsync);
        endpoints.MapGet("/matches/{matchId}", GetMatchAsync);
        return endpoints;
    }

    private static IResult PersistenceDisabled() =>
        Results.Problem(
            title: "Persistence is disabled.",
            detail: "Configure ConnectionStrings:SnakeForTwo and enable Persistence:Enabled to use this endpoint.",
            statusCode: StatusCodes.Status503ServiceUnavailable);

    private static async Task<IResult> GetLeaderboardAsync(
        string? window,
        int? limit,
        ILeaderboardQuery leaderboard,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryParseWindow(window, out var parsedWindow))
        {
            return Results.BadRequest(new
            {
                error = "invalidWindow",
                message = "Leaderboard window must be daily, monthly, or all-time."
            });
        }

        var entries = await leaderboard.GetLeaderboardAsync(
            parsedWindow,
            limit ?? 50,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return Results.Ok(new
        {
            window = WindowName(parsedWindow),
            entries
        });
    }

    private static async Task<IResult> GetMatchesAsync(
        int? limit,
        IMatchSummaryQuery matches,
        CancellationToken cancellationToken)
    {
        var recentMatches = await matches.GetRecentMatchesAsync(limit ?? 50, cancellationToken);
        return Results.Ok(new
        {
            matches = recentMatches
        });
    }

    private static async Task<IResult> GetMatchAsync(
        string matchId,
        IMatchSummaryQuery matches,
        CancellationToken cancellationToken)
    {
        var match = await matches.GetMatchAsync(matchId, cancellationToken);
        return match is null ? Results.NotFound() : Results.Ok(match);
    }

    private static bool TryParseWindow(string? value, out LeaderboardWindow window)
    {
        switch ((value ?? "daily").Trim().ToLowerInvariant())
        {
            case "daily":
            case "day":
                window = LeaderboardWindow.Daily;
                return true;

            case "monthly":
            case "month":
                window = LeaderboardWindow.Monthly;
                return true;

            case "all-time":
            case "alltime":
            case "all":
                window = LeaderboardWindow.AllTime;
                return true;

            default:
                window = default;
                return false;
        }
    }

    private static string WindowName(LeaderboardWindow window) =>
        window switch
        {
            LeaderboardWindow.Daily => "daily",
            LeaderboardWindow.Monthly => "monthly",
            LeaderboardWindow.AllTime => "all-time",
            _ => "daily"
        };
}
