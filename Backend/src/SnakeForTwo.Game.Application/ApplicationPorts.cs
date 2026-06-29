namespace SnakeForTwo.Game.Application;

public interface IGameEventPublisher
{
    ValueTask PublishAsync(GameEvent gameEvent, CancellationToken cancellationToken);
}

public abstract record GameEvent(string MatchId, DateTimeOffset OccurredAt);

public sealed record MatchStartedEvent(
    string MatchId,
    string RoomId,
    DateTimeOffset OccurredAt) : GameEvent(MatchId, OccurredAt);

public sealed record RollbackPerformedEvent(
    string MatchId,
    long FromTick,
    long ToTick,
    DateTimeOffset OccurredAt) : GameEvent(MatchId, OccurredAt);
