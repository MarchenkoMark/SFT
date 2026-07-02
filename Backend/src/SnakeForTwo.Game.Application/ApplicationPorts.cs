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

public interface IGameMetrics
{
    void ObserveRoomInventory(int roomCount, int activeMatchCount);

    void RecordDisconnect();

    void RecordInputLatency(long latencyMs);

    void RecordStaleInput();

    void RecordRollbackDepth(long rollbackDepthTicks);

    void RecordCorrections(int correctionCount);

    void RecordTickDuration(double durationMs, bool overran);

    void RecordMalformedMessage();

    void RecordRateLimitedMessage();

    void RecordRenderDiagnosticsBatch(int frameCount, string reason);

    void RecordRenderDiagnosticsFrame(
        long renderTickDelta,
        double frameServerLeadMs,
        double receivedFrameLeadMs,
        double estimatedServerOffsetMs);
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

public sealed class NullGameMetrics : IGameMetrics
{
    public static NullGameMetrics Instance { get; } = new();

    private NullGameMetrics()
    {
    }

    public void ObserveRoomInventory(int roomCount, int activeMatchCount)
    {
    }

    public void RecordDisconnect()
    {
    }

    public void RecordInputLatency(long latencyMs)
    {
    }

    public void RecordStaleInput()
    {
    }

    public void RecordRollbackDepth(long rollbackDepthTicks)
    {
    }

    public void RecordCorrections(int correctionCount)
    {
    }

    public void RecordTickDuration(double durationMs, bool overran)
    {
    }

    public void RecordMalformedMessage()
    {
    }

    public void RecordRateLimitedMessage()
    {
    }

    public void RecordRenderDiagnosticsBatch(int frameCount, string reason)
    {
    }

    public void RecordRenderDiagnosticsFrame(
        long renderTickDelta,
        double frameServerLeadMs,
        double receivedFrameLeadMs,
        double estimatedServerOffsetMs)
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
