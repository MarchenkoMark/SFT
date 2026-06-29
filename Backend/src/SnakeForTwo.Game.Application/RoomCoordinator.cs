using SnakeForTwo.Contracts;

namespace SnakeForTwo.Game.Application;

public interface IRoomIdentityProvider
{
    string CreateRoomId();

    string CreatePlayerId();

    string CreatePlayerSessionToken();

    string CreateMatchId();

    int CreateSeed();
}

public interface IRoomCoordinator
{
    RoomCommandResult CreateRoom(string connectionId);

    RoomCommandResult JoinRoom(string connectionId, string roomId);

    RoomCommandResult ResumeRoom(string connectionId, string roomId, string playerSessionToken);

    RoomCommandResult SetReady(string connectionId, string roomId, bool isReady);

    RoomCommandResult SubmitInput(string connectionId, ClientInputMessage input, long serverReceivedAt);

    RoomCommandResult LeaveRoom(string connectionId, string roomId);

    RoomCommandResult MarkDisconnected(string connectionId);

    RoomCommandResult FinishGame(string roomId, string result, string reason);

    RoomCommandResult ProcessTimers();
}

public sealed record OutboundServerMessage(
    IReadOnlyList<string> ConnectionIds,
    ServerMessage Message);

public sealed record ConnectionClosure(string ConnectionId, string Reason);

public sealed record RoomCommandResult(
    IReadOnlyList<OutboundServerMessage> Messages,
    IReadOnlyList<ConnectionClosure> ConnectionsToClose)
{
    public static RoomCommandResult Empty { get; } = new(
        Array.Empty<OutboundServerMessage>(),
        Array.Empty<ConnectionClosure>());

    public static RoomCommandResult ToConnection(string connectionId, ServerMessage message) =>
        new(
            new[] { new OutboundServerMessage(new[] { connectionId }, message) },
            Array.Empty<ConnectionClosure>());

    public static RoomCommandResult Error(
        string connectionId,
        string code,
        string message,
        string? roomId = null) =>
        ToConnection(connectionId, new ErrorMessage(code, message, roomId));
}

public sealed class GameRoomCoordinator : IRoomCoordinator
{
    private const int MaxPlayersPerRoom = 2;

    private readonly object _syncRoot = new();
    private readonly Dictionary<string, RoomRecord> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PlayerLocation> _connections = new(StringComparer.Ordinal);
    private readonly GameRuntimeOptions _options;
    private readonly IRoomIdentityProvider _identityProvider;
    private readonly TimeProvider _timeProvider;

    public GameRoomCoordinator(
        GameRuntimeOptions options,
        IRoomIdentityProvider identityProvider,
        TimeProvider? timeProvider = null)
    {
        _options = options;
        _identityProvider = identityProvider;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public RoomCommandResult CreateRoom(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_syncRoot)
        {
            if (_connections.ContainsKey(connectionId))
            {
                return RoomCommandResult.Error(
                    connectionId,
                    "connectionAlreadyInRoom",
                    "This connection is already attached to a room.");
            }

            var roomId = CreateUniqueRoomId();
            var player = CreatePlayer(seat: 1, connectionId);
            var room = new RoomRecord(roomId, RoomStatusDto.WaitingForPlayers);
            room.Players.Add(player);

            _rooms.Add(room.Id, room);
            _connections[connectionId] = new PlayerLocation(room.Id, player.PlayerId);

            var state = ToRoomState(room);
            return RoomCommandResult.ToConnection(
                connectionId,
                new RoomCreatedMessage(room.Id, player.PlayerId, player.SessionToken, state));
        }
    }

    public RoomCommandResult JoinRoom(string connectionId, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_syncRoot)
        {
            if (_connections.ContainsKey(connectionId))
            {
                return RoomCommandResult.Error(
                    connectionId,
                    "connectionAlreadyInRoom",
                    "This connection is already attached to a room.",
                    NormalizeRoomId(roomId));
            }

            if (!TryGetRoom(roomId, out var room))
            {
                return RoomCommandResult.Error(
                    connectionId,
                    "roomNotFound",
                    "Room does not exist.",
                    NormalizeRoomId(roomId));
            }

            if (room.Status is RoomStatusDto.Starting or RoomStatusDto.InGame)
            {
                return RoomCommandResult.Error(
                    connectionId,
                    "roomNotJoinable",
                    "Room cannot be joined after the game has started.",
                    room.Id);
            }

            var seat = FindAvailableSeat(room);
            if (seat is null)
            {
                return RoomCommandResult.Error(
                    connectionId,
                    "roomFull",
                    "Room already has two reserved player seats.",
                    room.Id);
            }

            var player = CreatePlayer(seat.Value, connectionId);
            room.Players.Add(player);
            room.Status = room.Players.Count == MaxPlayersPerRoom
                ? RoomStatusDto.ReadyCheck
                : RoomStatusDto.WaitingForPlayers;

            _connections[connectionId] = new PlayerLocation(room.Id, player.PlayerId);

            var state = ToRoomState(room);
            var messages = new List<OutboundServerMessage>
            {
                Direct(connectionId, new RoomJoinedMessage(room.Id, player.PlayerId, player.SessionToken, state)),
                Broadcast(room, new RoomStateMessage(state))
            };

            return Result(messages);
        }
    }

    public RoomCommandResult ResumeRoom(string connectionId, string roomId, string playerSessionToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_syncRoot)
        {
            if (!TryGetRoom(roomId, out var room))
            {
                return RoomCommandResult.Error(
                    connectionId,
                    "roomNotFound",
                    "Room does not exist.",
                    NormalizeRoomId(roomId));
            }

            var player = room.Players.FirstOrDefault(candidate =>
                string.Equals(candidate.SessionToken, playerSessionToken, StringComparison.Ordinal));

            if (player is null)
            {
                return RoomCommandResult.Error(
                    connectionId,
                    "invalidPlayerSessionToken",
                    "The player session token does not match any seat in this room.",
                    room.Id);
            }

            var closures = new List<ConnectionClosure>();
            if (player.ConnectionId is { Length: > 0 } previousConnectionId &&
                !string.Equals(previousConnectionId, connectionId, StringComparison.Ordinal))
            {
                _connections.Remove(previousConnectionId);
                closures.Add(new ConnectionClosure(previousConnectionId, "Seat was resumed by a newer connection."));
            }

            player.ConnectionId = connectionId;
            player.IsConnected = true;
            player.DisconnectedAt = null;
            _connections[connectionId] = new PlayerLocation(room.Id, player.PlayerId);

            var state = ToRoomState(room);
            var messages = new List<OutboundServerMessage>
            {
                Direct(connectionId, new RoomResumedMessage(room.Id, player.PlayerId, player.SessionToken, state)),
                Broadcast(room, new RoomStateMessage(state))
            };

            if (room.Status == RoomStatusDto.InGame && room.Match is not null)
            {
                messages.Add(Direct(
                    connectionId,
                    new GameStartedMessage(
                        room.Id,
                        room.Match.MatchId,
                        player.PlayerId,
                        player.Seat,
                        room.Match.StartServerTime.ToUnixTimeMilliseconds(),
                        room.Match.Seed,
                        ToTimingSettings())));
            }

            return Result(messages, closures);
        }
    }

    public RoomCommandResult SetReady(string connectionId, string roomId, bool isReady)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_syncRoot)
        {
            if (!TryGetAttachedPlayer(connectionId, roomId, out var room, out var player, out var error))
            {
                return error;
            }

            if (room.Status == RoomStatusDto.InGame)
            {
                return RoomCommandResult.Error(
                    connectionId,
                    "gameAlreadyRunning",
                    "Ready state cannot be changed while a game is running.",
                    room.Id);
            }

            if (room.Players.Count < MaxPlayersPerRoom)
            {
                return RoomCommandResult.Error(
                    connectionId,
                    "roomWaitingForPlayers",
                    "Ready state can only be changed after both seats are reserved.",
                    room.Id);
            }

            if (isReady)
            {
                if (room.Status == RoomStatusDto.PostGame)
                {
                    room.Status = RoomStatusDto.ReadyCheck;
                }

                if (room.Status != RoomStatusDto.ReadyCheck)
                {
                    return RoomCommandResult.Error(
                        connectionId,
                        "roomNotReadyForReadyCheck",
                        "Room is not accepting ready changes right now.",
                        room.Id);
                }

                player.IsReady = true;

                if (room.Players.All(candidate => candidate.IsConnected && candidate.IsReady))
                {
                    return BeginStartLocked(room);
                }

                return Result(new[] { Broadcast(room, new RoomStateMessage(ToRoomState(room))) });
            }

            player.IsReady = false;
            if (room.Status == RoomStatusDto.Starting)
            {
                room.Status = RoomStatusDto.ReadyCheck;
                room.Match = null;
            }

            return Result(new[] { Broadcast(room, new RoomStateMessage(ToRoomState(room))) });
        }
    }

    public RoomCommandResult SubmitInput(string connectionId, ClientInputMessage input, long serverReceivedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_syncRoot)
        {
            if (!TryGetAttachedPlayer(connectionId, input.RoomId, out var room, out _, out var error))
            {
                return error;
            }

            return room.Status == RoomStatusDto.InGame
                ? RoomCommandResult.Empty
                : RoomCommandResult.Error(
                    connectionId,
                    "gameNotRunning",
                    "Inputs are only accepted while a game is running.",
                    room.Id);
        }
    }

    public RoomCommandResult LeaveRoom(string connectionId, string roomId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_syncRoot)
        {
            if (!TryGetAttachedPlayer(connectionId, roomId, out var room, out var player, out var error))
            {
                return error;
            }

            var messages = new List<OutboundServerMessage>();
            if (room.Status is RoomStatusDto.Starting or RoomStatusDto.InGame)
            {
                FinishGameLocked(room, "forfeit", "playerLeft", messages);
            }

            RemovePlayerLocked(room, player);
            if (room.Players.Count == 0)
            {
                _rooms.Remove(room.Id);
                return Result(messages);
            }

            NormalizeRoomStatusAfterRosterChange(room);
            messages.Add(Broadcast(room, new RoomStateMessage(ToRoomState(room))));

            return Result(messages);
        }
    }

    public RoomCommandResult MarkDisconnected(string connectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_syncRoot)
        {
            if (!_connections.TryGetValue(connectionId, out var location) ||
                !_rooms.TryGetValue(location.RoomId, out var room))
            {
                return RoomCommandResult.Empty;
            }

            var player = room.Players.FirstOrDefault(candidate =>
                string.Equals(candidate.PlayerId, location.PlayerId, StringComparison.Ordinal));
            if (player is null ||
                !string.Equals(player.ConnectionId, connectionId, StringComparison.Ordinal))
            {
                _connections.Remove(connectionId);
                return RoomCommandResult.Empty;
            }

            _connections.Remove(connectionId);
            player.ConnectionId = null;
            player.IsConnected = false;
            player.DisconnectedAt = _timeProvider.GetUtcNow();

            if (room.Status != RoomStatusDto.InGame)
            {
                player.IsReady = false;
            }

            if (room.Status == RoomStatusDto.Starting)
            {
                room.Status = RoomStatusDto.ReadyCheck;
                room.Match = null;
            }

            return Result(new[] { Broadcast(room, new RoomStateMessage(ToRoomState(room))) });
        }
    }

    public RoomCommandResult FinishGame(string roomId, string result, string reason)
    {
        lock (_syncRoot)
        {
            if (!TryGetRoom(roomId, out var room))
            {
                return RoomCommandResult.Empty;
            }

            if (room.Status is not (RoomStatusDto.Starting or RoomStatusDto.InGame))
            {
                return RoomCommandResult.Empty;
            }

            var messages = new List<OutboundServerMessage>();
            FinishGameLocked(room, result, reason, messages);
            return Result(messages);
        }
    }

    public RoomCommandResult ProcessTimers()
    {
        lock (_syncRoot)
        {
            var now = _timeProvider.GetUtcNow();
            var messages = new List<OutboundServerMessage>();

            foreach (var room in _rooms.Values.ToArray())
            {
                if (room.Status == RoomStatusDto.Starting &&
                    room.Match is not null &&
                    room.Match.StartServerTime <= now)
                {
                    room.Status = RoomStatusDto.InGame;
                    messages.Add(Broadcast(room, new RoomStateMessage(ToRoomState(room))));

                    foreach (var player in room.Players.Where(candidate => candidate.ConnectionId is not null))
                    {
                        messages.Add(Direct(
                            player.ConnectionId!,
                            new GameStartedMessage(
                                room.Id,
                                room.Match.MatchId,
                                player.PlayerId,
                                player.Seat,
                                room.Match.StartServerTime.ToUnixTimeMilliseconds(),
                                room.Match.Seed,
                                ToTimingSettings())));
                    }
                }

                if (room.Status == RoomStatusDto.InGame &&
                    room.Players.Any(player => player.DisconnectedAt is not null &&
                        player.DisconnectedAt.Value + TimeSpan.FromSeconds(_options.DisconnectGracePeriodSeconds) <= now))
                {
                    FinishGameLocked(room, "forfeit", "disconnectTimeout", messages);
                }
            }

            return Result(messages);
        }
    }

    private RoomCommandResult BeginStartLocked(RoomRecord room)
    {
        var startServerTime = _timeProvider.GetUtcNow() + TimeSpan.FromSeconds(_options.StartCountdownSeconds);
        room.Status = RoomStatusDto.Starting;
        room.Match = new MatchRecord(
            _identityProvider.CreateMatchId(),
            _identityProvider.CreateSeed(),
            startServerTime);

        var state = ToRoomState(room);
        var messages = new List<OutboundServerMessage>
        {
            Broadcast(room, new RoomStateMessage(state)),
            Broadcast(
                room,
                new GameStartingMessage(
                    room.Id,
                    room.Match.MatchId,
                    startServerTime.ToUnixTimeMilliseconds(),
                    (int)Math.Round(_options.TicksPerSecond),
                    room.Match.Seed,
                    ToTimingSettings()))
        };

        return Result(messages);
    }

    private void FinishGameLocked(
        RoomRecord room,
        string result,
        string reason,
        List<OutboundServerMessage> messages)
    {
        var match = room.Match;
        if (match is null)
        {
            return;
        }

        room.Status = RoomStatusDto.PostGame;
        foreach (var player in room.Players)
        {
            player.IsReady = false;
        }

        messages.Add(Broadcast(
            room,
            new GameFinishedMessage(room.Id, match.MatchId, result, reason, FinalState: null)));
        messages.Add(Broadcast(room, new RoomStateMessage(ToRoomState(room))));
    }

    private PlayerRecord CreatePlayer(int seat, string connectionId) =>
        new(
            _identityProvider.CreatePlayerId(),
            seat,
            _identityProvider.CreatePlayerSessionToken(),
            connectionId);

    private string CreateUniqueRoomId()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var roomId = NormalizeRoomId(_identityProvider.CreateRoomId());
            if (!_rooms.ContainsKey(roomId))
            {
                return roomId;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique room id.");
    }

    private static int? FindAvailableSeat(RoomRecord room)
    {
        for (var seat = 1; seat <= MaxPlayersPerRoom; seat++)
        {
            if (room.Players.All(player => player.Seat != seat))
            {
                return seat;
            }
        }

        return null;
    }

    private bool TryGetAttachedPlayer(
        string connectionId,
        string roomId,
        out RoomRecord room,
        out PlayerRecord player,
        out RoomCommandResult error)
    {
        room = null!;
        player = null!;

        if (!_connections.TryGetValue(connectionId, out var location) ||
            !_rooms.TryGetValue(location.RoomId, out var attachedRoom))
        {
            error = RoomCommandResult.Error(
                connectionId,
                "connectionNotInRoom",
                "This connection is not attached to a room.",
                NormalizeRoomId(roomId));
            return false;
        }

        room = attachedRoom;

        if (!string.Equals(location.RoomId, NormalizeRoomId(roomId), StringComparison.OrdinalIgnoreCase))
        {
            error = RoomCommandResult.Error(
                connectionId,
                "roomMismatch",
                "This connection is attached to a different room.",
                NormalizeRoomId(roomId));
            return false;
        }

        player = room.Players.First(candidate =>
            string.Equals(candidate.PlayerId, location.PlayerId, StringComparison.Ordinal));

        error = RoomCommandResult.Empty;
        return true;
    }

    private bool TryGetRoom(string roomId, out RoomRecord room) =>
        _rooms.TryGetValue(NormalizeRoomId(roomId), out room!);

    private static string NormalizeRoomId(string? roomId) => roomId?.Trim() ?? "";

    private void RemovePlayerLocked(RoomRecord room, PlayerRecord player)
    {
        if (player.ConnectionId is not null)
        {
            _connections.Remove(player.ConnectionId);
        }

        room.Players.Remove(player);
    }

    private static void NormalizeRoomStatusAfterRosterChange(RoomRecord room)
    {
        if (room.Players.Count < MaxPlayersPerRoom)
        {
            room.Status = RoomStatusDto.WaitingForPlayers;
            room.Match = null;
            foreach (var player in room.Players)
            {
                player.IsReady = false;
            }

            return;
        }

        if (room.Status is RoomStatusDto.WaitingForPlayers or RoomStatusDto.PostGame)
        {
            room.Status = RoomStatusDto.ReadyCheck;
        }
    }

    private RoomStateDto ToRoomState(RoomRecord room) =>
        new(
            room.Id,
            room.Status,
            room.Players
                .OrderBy(player => player.Seat)
                .Select(player => new RoomPlayerDto(
                    player.PlayerId,
                    player.Seat,
                    player.IsConnected,
                    player.IsReady))
                .ToArray(),
            room.Match?.MatchId);

    private TimingSettingsDto ToTimingSettings() =>
        new(
            _options.TilesPerSecond,
            _options.AnimationFramesPerTile,
            (int)Math.Round(_options.TickDuration.TotalMilliseconds),
            (int)Math.Round(_options.AnimationFrameDuration.TotalMilliseconds),
            _options.InputFutureBufferTicks,
            _options.DisconnectGracePeriodSeconds);

    private static OutboundServerMessage Direct(string connectionId, ServerMessage message) =>
        new(new[] { connectionId }, message);

    private static OutboundServerMessage Broadcast(RoomRecord room, ServerMessage message) =>
        new(
            room.Players
                .Where(player => player.ConnectionId is not null)
                .Select(player => player.ConnectionId!)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            message);

    private static RoomCommandResult Result(IEnumerable<OutboundServerMessage> messages) =>
        new(messages.Where(message => message.ConnectionIds.Count > 0).ToArray(), Array.Empty<ConnectionClosure>());

    private static RoomCommandResult Result(
        IEnumerable<OutboundServerMessage> messages,
        IEnumerable<ConnectionClosure> closures) =>
        new(
            messages.Where(message => message.ConnectionIds.Count > 0).ToArray(),
            closures.ToArray());

    private sealed record PlayerLocation(string RoomId, string PlayerId);

    private sealed class RoomRecord
    {
        public RoomRecord(string id, RoomStatusDto status)
        {
            Id = id;
            Status = status;
        }

        public string Id { get; }

        public RoomStatusDto Status { get; set; }

        public List<PlayerRecord> Players { get; } = [];

        public MatchRecord? Match { get; set; }
    }

    private sealed class PlayerRecord
    {
        public PlayerRecord(string playerId, int seat, string sessionToken, string connectionId)
        {
            PlayerId = playerId;
            Seat = seat;
            SessionToken = sessionToken;
            ConnectionId = connectionId;
            IsConnected = true;
        }

        public string PlayerId { get; }

        public int Seat { get; }

        public string SessionToken { get; }

        public string? ConnectionId { get; set; }

        public bool IsConnected { get; set; }

        public bool IsReady { get; set; }

        public DateTimeOffset? DisconnectedAt { get; set; }
    }

    private sealed record MatchRecord(
        string MatchId,
        int Seed,
        DateTimeOffset StartServerTime);
}
