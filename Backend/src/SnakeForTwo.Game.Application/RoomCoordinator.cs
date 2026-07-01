using System.Diagnostics;
using SnakeForTwo.Contracts;
using SnakeForTwo.Game.Domain;

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
        new([new OutboundServerMessage([connectionId], message)],
            []);

    public static RoomCommandResult Error(
        string connectionId,
        string code,
        string message,
        string? roomId = null) =>
        ToConnection(connectionId, new ErrorMessage(code, message, roomId));
}

public sealed class GameRoomCoordinator(
    GameRuntimeOptions options,
    IRoomIdentityProvider identityProvider,
    TimeProvider? timeProvider = null,
    IRoomLifecycleLogger? lifecycleLogger = null,
    IGameMetrics? metrics = null)
    : IRoomCoordinator
{
    private const int MaxPlayersPerRoom = 2;

    private readonly Lock _syncRoot = new();
    private readonly Dictionary<string, RoomRecord> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PlayerLocation> _connections = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly IRoomLifecycleLogger _lifecycleLogger =
        lifecycleLogger ?? NullRoomLifecycleLogger.Instance;
    private readonly IGameMetrics _metrics = metrics ?? NullGameMetrics.Instance;

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
            _lifecycleLogger.RoomCreated(room.Id, player.PlayerId, connectionId);
            ObserveRoomInventoryLocked();

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
            _lifecycleLogger.RoomJoined(room.Id, player.PlayerId, connectionId);
            ObserveRoomInventoryLocked();

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

            if (room is { Status: RoomStatusDto.InGame, Match: not null })
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
                if (room.Match.Session is not null)
                {
                    messages.Add(Direct(
                        connectionId,
                        ToAuthoritativeFrameMessage(room, CreateFrame(room.Match.Session))));
                }
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

                return room.Players.All(candidate => candidate is { IsConnected: true, IsReady: true }) ?
                    BeginStartLocked(room) :
                    Result([Broadcast(room, new RoomStateMessage(ToRoomState(room)))]);
            }

            player.IsReady = false;
            if (room.Status == RoomStatusDto.Starting)
            {
                room.Status = RoomStatusDto.ReadyCheck;
                room.Match = null;
            }

            return Result([Broadcast(room, new RoomStateMessage(ToRoomState(room)))]);
        }
    }

    public RoomCommandResult SubmitInput(string connectionId, ClientInputMessage input, long serverReceivedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);

        lock (_syncRoot)
        {
            if (!TryGetAttachedPlayer(connectionId, input.RoomId, out var room, out var player, out var error))
            {
                return error;
            }

            if (room is not { Status: RoomStatusDto.InGame, Match.Session: not null })
            {
                return RoomCommandResult.Error(
                    connectionId,
                    "gameNotRunning",
                    "Inputs are only accepted while a game is running.",
                    room.Id);
            }

            _metrics.RecordInputLatency(Math.Max(0, serverReceivedAt - input.ClientTime));

            var targetTick = EstimateInputTargetTick(room.Match, input, serverReceivedAt);
            var inputResult = room.Match.Session.SubmitInput(
                new PlayerId(player.PlayerId),
                targetTick,
                ToDomainDirection(input.Direction));
            RecordInputSchedulingMetrics(room.Match.Session, inputResult);

            var messages = new List<OutboundServerMessage>();
            if (inputResult is
                {
                    Status: InputSchedulingStatus.Scheduled,
                    ScheduledTick: not null
                })
            {
                messages.Add(Broadcast(
                    room,
                    new TurnIntentAcceptedMessage(
                        room.Id,
                        room.Match.MatchId,
                        player.PlayerId,
                        input.Direction,
                        inputResult.ScheduledTick.Value.Value,
                        input.ClientTime,
                        input.ClientSequence,
                        serverReceivedAt)));
            }

            messages.AddRange(inputResult.Corrections
                .Select(frame => Broadcast(room, ToCorrectionMessage(room, frame))));

            if (room.Match.Session.CurrentState.Status == GameStatus.Finished)
            {
                FinishGameLocked(room, "teamLost", room.Match.Session.CurrentState.GameOverReason.ToString(), messages);
            }

            return Result(messages);
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

            _lifecycleLogger.RoomLeft(room.Id, player.PlayerId, player.ConnectionId, "explicitLeave");
            RemovePlayerLocked(room, player);
            if (room.Players.Count == 0)
            {
                _rooms.Remove(room.Id);
                ObserveRoomInventoryLocked();
                return Result(messages);
            }

            NormalizeRoomStatusAfterRosterChange(room);
            ObserveRoomInventoryLocked();
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
            _lifecycleLogger.PlayerDisconnected(room.Id, player.PlayerId, connectionId);
            _metrics.RecordDisconnect();

            if (room.Status != RoomStatusDto.InGame)
            {
                player.IsReady = false;
            }

            if (room.Status == RoomStatusDto.Starting)
            {
                room.Status = RoomStatusDto.ReadyCheck;
                room.Match = null;
            }

            return Result([Broadcast(room, new RoomStateMessage(ToRoomState(room)))]);
        }
    }

    public RoomCommandResult FinishGame(string roomId, string result, string reason)
    {
        lock (_syncRoot)
        {
            if (!TryGetRoom(roomId, out var room) || room.Status is not (RoomStatusDto.Starting or RoomStatusDto.InGame))
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
                if (room is { Status: RoomStatusDto.Starting, Match: not null } &&
                    room.Match.StartServerTime <= now)
                {
                    room.Match.Session = CreateGameSession(room, room.Match);
                    room.Status = RoomStatusDto.InGame;
                    _lifecycleLogger.GameStarted(room.Id, room.Match.MatchId);
                    ObserveRoomInventoryLocked();
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

                    messages.Add(Broadcast(room, ToAuthoritativeFrameMessage(room, CreateFrame(room.Match.Session))));
                }

                if (room is { Status: RoomStatusDto.InGame, Match.Session: not null })
                {
                    while (NextFrameServerTime(room.Match) <= now &&
                        room.Match.Session.CurrentState.Status == GameStatus.Running)
                    {
                        var tickStarted = Stopwatch.GetTimestamp();
                        var frame = room.Match.Session.AdvanceOneTick();
                        var tickDuration = Stopwatch.GetElapsedTime(tickStarted);
                        _metrics.RecordTickDuration(
                            tickDuration.TotalMilliseconds,
                            tickDuration > options.TickDuration);
                        messages.Add(Broadcast(room, ToAuthoritativeFrameMessage(room, frame)));
                    }

                    if (room.Match.Session.CurrentState.Status == GameStatus.Finished)
                    {
                        FinishGameLocked(
                            room,
                            "teamLost",
                            room.Match.Session.CurrentState.GameOverReason.ToString(),
                            messages);
                    }
                }

                if (room.Status == RoomStatusDto.InGame &&
                    room.Players.Any(player => player.DisconnectedAt is not null &&
                        player.DisconnectedAt.Value + TimeSpan.FromSeconds(options.DisconnectGracePeriodSeconds) <= now))
                {
                    FinishGameLocked(room, "forfeit", "disconnectTimeout", messages);
                }

                RemoveExpiredDisconnectedPlayersLocked(room, now, messages);
            }

            ObserveRoomInventoryLocked();
            return Result(messages);
        }
    }

    private RoomCommandResult BeginStartLocked(RoomRecord room)
    {
        var startServerTime = _timeProvider.GetUtcNow() + TimeSpan.FromSeconds(options.StartCountdownSeconds);
        room.Status = RoomStatusDto.Starting;
        room.Match = new MatchRecord(
            identityProvider.CreateMatchId(),
            identityProvider.CreateSeed(),
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
                    (int)Math.Round(options.TicksPerSecond),
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

        var wasGameActive = room.Status == RoomStatusDto.InGame || match.Session is not null;
        room.Status = RoomStatusDto.PostGame;
        foreach (var player in room.Players)
        {
            player.IsReady = false;
        }

        if (wasGameActive)
        {
            _lifecycleLogger.GameEnded(room.Id, match.MatchId, result, reason);
        }
        ObserveRoomInventoryLocked();

        messages.Add(Broadcast(
            room,
            new GameFinishedMessage(
                room.Id,
                match.MatchId,
                result,
                reason,
                match.Session is null
                    ? null
                    : ToAuthoritativeGameStateDto(WithOutgoingDirections(
                        match.Session.CurrentState,
                        match.Session.OutgoingDirectionsAt(match.Session.CurrentTick))))));
        messages.Add(Broadcast(room, new RoomStateMessage(ToRoomState(room))));
    }

    private RollbackGameSession CreateGameSession(RoomRecord room, MatchRecord match)
    {
        var rules = new GameRules(
            BoardWidth: options.BoardWidth,
            BoardHeight: options.BoardHeight,
            TilesPerSecond: options.TilesPerSecond,
            RollbackWindowTicks: Math.Max(1, (int)Math.Ceiling(options.RollbackHistorySeconds * options.TicksPerSecond)),
            WallWrapping: true,
            FoodSeed: match.Seed,
            InputFutureBufferTicks: options.InputFutureBufferTicks);
        var initialState = SnakeGameEngine.CreateInitialState(
            rules,
            room.Players
                .OrderBy(player => player.Seat)
                .Select(player => CreateSnakeSpawn(player, rules)));

        return new RollbackGameSession(initialState);
    }

    private void RemoveExpiredDisconnectedPlayersLocked(
        RoomRecord room,
        DateTimeOffset now,
        List<OutboundServerMessage> messages)
    {
        if (room.Status is RoomStatusDto.Starting or RoomStatusDto.InGame)
        {
            return;
        }

        var expiredPlayers = room.Players
            .Where(player => player.DisconnectedAt is not null &&
                player.DisconnectedAt.Value + options.DisconnectedPlayerRetention <= now)
            .ToArray();

        if (expiredPlayers.Length == 0)
        {
            return;
        }

        foreach (var player in expiredPlayers)
        {
            _lifecycleLogger.RoomLeft(room.Id, player.PlayerId, player.ConnectionId, "disconnectCleanup");
            RemovePlayerLocked(room, player);
        }

        if (room.Players.Count == 0)
        {
            _rooms.Remove(room.Id);
            ObserveRoomInventoryLocked();
            return;
        }

        NormalizeRoomStatusAfterRosterChange(room);
        ObserveRoomInventoryLocked();
        messages.Add(Broadcast(room, new RoomStateMessage(ToRoomState(room))));
    }

    private void RecordInputSchedulingMetrics(
        RollbackGameSession session,
        GameInputResult inputResult)
    {
        if (inputResult.Status == InputSchedulingStatus.RejectedStale)
        {
            _metrics.RecordStaleInput();
        }

        if (inputResult is { RolledBack: true, RollbackFromTick: not null })
        {
            _metrics.RecordRollbackDepth(
                Math.Max(0, session.CurrentTick.Value - inputResult.RollbackFromTick.Value.Value));
        }

        if (inputResult.Corrections.Count > 0)
        {
            _metrics.RecordCorrections(inputResult.Corrections.Count);
        }
    }

    private void ObserveRoomInventoryLocked()
    {
        _metrics.ObserveRoomInventory(
            _rooms.Count,
            _rooms.Values.Count(room => room.Status == RoomStatusDto.InGame));
    }

    private static SnakeSpawn CreateSnakeSpawn(PlayerRecord player, GameRules rules)
    {
        var length = Math.Min(4, Math.Max(2, rules.BoardWidth / 8));

        if (player.Seat % 2 == 1)
        {
            var y = Math.Max(1, rules.BoardHeight / 3);
            var headX = Math.Max(length, rules.BoardWidth / 4);
            return new SnakeSpawn(
                new PlayerId(player.PlayerId),
                new SnakeColor(ColorForSeat(player.Seat)),
                Direction.Right,
                Enumerable.Range(0, length).Select(offset => new Cell(headX - offset, y)));
        }

        var evenY = Math.Min(rules.BoardHeight - 2, (rules.BoardHeight * 2) / 3);
        var rightHeadX = Math.Min(rules.BoardWidth - length - 1, (rules.BoardWidth * 3) / 4);
        return new SnakeSpawn(
            new PlayerId(player.PlayerId),
            new SnakeColor(ColorForSeat(player.Seat)),
            Direction.Left,
            Enumerable.Range(0, length).Select(offset => new Cell(rightHeadX + offset, evenY)));
    }

    private static string ColorForSeat(int seat) =>
        seat switch
        {
            1 => "blue",
            2 => "orange",
            3 => "green",
            4 => "red",
            _ => $"seat-{seat}"
        };

    private GameTick EstimateInputTargetTick(
        MatchRecord match,
        ClientInputMessage input,
        long serverReceivedAt)
    {
        var inputServerTime = input.ClientTime > 0
            ? input.ClientTime
            : serverReceivedAt;
        var tickDurationMs = Math.Max(1, options.TickDuration.TotalMilliseconds);
        var elapsedMs = Math.Max(0, inputServerTime - match.StartServerTime.ToUnixTimeMilliseconds());
        var inputTick = (long)Math.Floor(elapsedMs / tickDurationMs);
        var elapsedIntoTickMs = elapsedMs - (inputTick * tickDurationMs);

        return new GameTick(
            elapsedIntoTickMs <= options.InputGrace.TotalMilliseconds
                ? inputTick
                : inputTick + 1);
    }

    private DateTimeOffset NextFrameServerTime(MatchRecord match) =>
        match.StartServerTime + (options.TickDuration * ((match.Session?.CurrentTick.Value ?? 0) + 1));

    private AuthoritativeFrameMessage ToAuthoritativeFrameMessage(RoomRecord room, GameFrame frame)
    {
        var match = room.Match ?? throw new InvalidOperationException("A frame requires an active match.");
        var state = WithOutgoingDirections(frame.State, match.Session!.OutgoingDirectionsAt(frame.Tick));
        return new AuthoritativeFrameMessage(
            room.Id,
            match.MatchId,
            frame.Tick.Value,
            FrameServerTime(match, frame.Tick),
            SnakeGameEngine.ComputeStateHash(state),
            ToAuthoritativeGameStateDto(state));
    }

    private CorrectionMessage ToCorrectionMessage(RoomRecord room, GameFrame frame)
    {
        var match = room.Match ?? throw new InvalidOperationException("A correction requires an active match.");
        var state = WithOutgoingDirections(frame.State, match.Session!.OutgoingDirectionsAt(frame.Tick));
        return new CorrectionMessage(
            room.Id,
            match.MatchId,
            frame.Tick.Value,
            FrameServerTime(match, frame.Tick),
            SnakeGameEngine.ComputeStateHash(state),
            ToAuthoritativeGameStateDto(state));
    }

    private long FrameServerTime(MatchRecord match, GameTick tick) =>
        (match.StartServerTime + (options.TickDuration * tick.Value)).ToUnixTimeMilliseconds();

    private static GameFrame CreateFrame(RollbackGameSession session) =>
        new(
            session.CurrentTick,
            session.CurrentState.Copy(),
            SnakeGameEngine.ComputeStateHash(session.CurrentState));

    private static GameState WithOutgoingDirections(
        GameState state,
        IReadOnlyDictionary<PlayerId, Direction> outgoingDirections) =>
        state.With(snakes: state.Snakes.Select(snake =>
            outgoingDirections.TryGetValue(snake.PlayerId, out var outgoingDirection)
                ? snake.With(outgoingDirection)
                : snake.With()));

    private static AuthoritativeGameStateDto ToAuthoritativeGameStateDto(GameState state) =>
        new(
            new BoardDto(state.Rules.BoardWidth, state.Rules.BoardHeight),
            state.Snakes.Select(ToAuthoritativeSnakeDto).ToArray(),
            state.Food.Select(food => new FoodItemDto(
                food.OwnerPlayerId.Value,
                new CellDto(food.Cell.X, food.Cell.Y))).ToArray(),
            state.Status.ToString());

    private static AuthoritativeSnakeDto ToAuthoritativeSnakeDto(Snake snake)
    {
        var head = snake.Body.Count > 0 ? snake.Head : new Cell(0, 0);
        return new AuthoritativeSnakeDto(
            snake.PlayerId.Value,
            snake.Alive,
            new CellDto(head.X, head.Y),
            ToDirectionDto(snake.Direction),
            snake.Body.Select(cell => new CellDto(cell.X, cell.Y)).ToArray());
    }

    private static Direction ToDomainDirection(DirectionDto direction) =>
        direction switch
        {
            DirectionDto.Up => Direction.Up,
            DirectionDto.Right => Direction.Right,
            DirectionDto.Down => Direction.Down,
            DirectionDto.Left => Direction.Left,
            _ => Direction.Right
        };

    private static DirectionDto ToDirectionDto(Direction direction) =>
        direction switch
        {
            Direction.Up => DirectionDto.Up,
            Direction.Right => DirectionDto.Right,
            Direction.Down => DirectionDto.Down,
            Direction.Left => DirectionDto.Left,
            _ => DirectionDto.Right
        };

    private PlayerRecord CreatePlayer(int seat, string connectionId) =>
        new(
            identityProvider.CreatePlayerId(),
            seat,
            identityProvider.CreatePlayerSessionToken(),
            connectionId);

    private string CreateUniqueRoomId()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var roomId = NormalizeRoomId(identityProvider.CreateRoomId());
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
            options.TilesPerSecond,
            options.AnimationFramesPerTile,
            (int)Math.Round(options.TickDuration.TotalMilliseconds),
            (int)Math.Round(options.AnimationFrameDuration.TotalMilliseconds),
            options.InputFutureBufferTicks,
            options.DisconnectGracePeriodSeconds);

    private static OutboundServerMessage Direct(string connectionId, ServerMessage message) =>
        new([connectionId], message);

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

    private sealed class RoomRecord(string id, RoomStatusDto status)
    {
        public string Id { get; } = id;

        public RoomStatusDto Status { get; set; } = status;

        public List<PlayerRecord> Players { get; } = [];

        public MatchRecord? Match { get; set; }
    }

    private sealed class PlayerRecord(string playerId, int seat, string sessionToken, string connectionId)
    {
        public string PlayerId { get; } = playerId;

        public int Seat { get; } = seat;

        public string SessionToken { get; } = sessionToken;

        public string? ConnectionId { get; set; } = connectionId;

        public bool IsConnected { get; set; } = true;

        public bool IsReady { get; set; }

        public DateTimeOffset? DisconnectedAt { get; set; }
    }

    private sealed record MatchRecord(
        string MatchId,
        int Seed,
        DateTimeOffset StartServerTime)
    {
        public RollbackGameSession? Session { get; set; }
    }
}
