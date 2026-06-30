using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Api;

internal sealed class RoomLifecycleLogger : IRoomLifecycleLogger
{
    private readonly ILogger<RoomLifecycleLogger> _logger;

    public RoomLifecycleLogger(ILogger<RoomLifecycleLogger> logger)
    {
        _logger = logger;
    }

    public void RoomCreated(string roomId, string playerId, string connectionId)
    {
        _logger.LogInformation(
            "Room created: {RoomId} by player {PlayerId} on connection {ConnectionId}.",
            roomId,
            playerId,
            connectionId);
    }

    public void RoomJoined(string roomId, string playerId, string connectionId)
    {
        _logger.LogInformation(
            "Room joined: {RoomId} by player {PlayerId} on connection {ConnectionId}.",
            roomId,
            playerId,
            connectionId);
    }

    public void RoomLeft(string roomId, string playerId, string? connectionId, string reason)
    {
        _logger.LogInformation(
            "Room left: {RoomId} by player {PlayerId} on connection {ConnectionId}. Reason: {Reason}.",
            roomId,
            playerId,
            connectionId ?? "none",
            reason);
    }

    public void PlayerDisconnected(string roomId, string playerId, string connectionId)
    {
        _logger.LogInformation(
            "Player disconnected: {PlayerId} from room {RoomId} on connection {ConnectionId}.",
            playerId,
            roomId,
            connectionId);
    }

    public void GameStarted(string roomId, string matchId)
    {
        _logger.LogInformation(
            "Game started: {MatchId} in room {RoomId}.",
            matchId,
            roomId);
    }

    public void GameEnded(string roomId, string matchId, string result, string reason)
    {
        _logger.LogInformation(
            "Game ended: {MatchId} in room {RoomId}. Result: {Result}. Reason: {Reason}.",
            matchId,
            roomId,
            result,
            reason);
    }
}
