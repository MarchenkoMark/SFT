namespace SnakeForTwo.Game.Application;

public interface IGameEventPublisher
{
    ValueTask PublishAsync(GameEvent gameEvent, CancellationToken cancellationToken);
}

public interface IRoomLifecycleLogger
{
    void RoomCreated(string roomId, string playerId, string connectionId);

    void RoomJoined(string roomId, string playerId, string connectionId);

    void RoomLeft(string roomId, string playerId, string? connectionId, string reason);

    void PlayerDisconnected(string roomId, string playerId, string connectionId);

    void GameStarted(string roomId, string matchId);

    void GameEnded(string roomId, string matchId, string result, string reason);
}

public sealed class NullRoomLifecycleLogger : IRoomLifecycleLogger
{
    public static NullRoomLifecycleLogger Instance { get; } = new();

    private NullRoomLifecycleLogger()
    {
    }

    public void RoomCreated(string roomId, string playerId, string connectionId)
    {
    }

    public void RoomJoined(string roomId, string playerId, string connectionId)
    {
    }

    public void RoomLeft(string roomId, string playerId, string? connectionId, string reason)
    {
    }

    public void PlayerDisconnected(string roomId, string playerId, string connectionId)
    {
    }

    public void GameStarted(string roomId, string matchId)
    {
    }

    public void GameEnded(string roomId, string matchId, string result, string reason)
    {
    }
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
