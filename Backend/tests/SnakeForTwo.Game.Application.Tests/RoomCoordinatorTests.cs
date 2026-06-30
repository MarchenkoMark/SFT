using SnakeForTwo.Contracts;

namespace SnakeForTwo.Game.Application.Tests;

public sealed class RoomCoordinatorTests
{
    [Fact]
    public void Create_room_returns_room_id_player_assignment_and_reclaim_token()
    {
        var coordinator = CreateCoordinator();

        var result = coordinator.CreateRoom("connection-1");

        var created = Assert.Single(result.Messages).Message as RoomCreatedMessage;
        Assert.NotNull(created);
        Assert.Equal("ROOM1", created.RoomId);
        Assert.Equal("player-1", created.PlayerId);
        Assert.Equal("token-1", created.PlayerSessionToken);
        Assert.Equal(RoomStatusDto.WaitingForPlayers, created.Room.Status);

        var player = Assert.Single(created.Room.Players);
        Assert.Equal(1, player.Seat);
        Assert.True(player.IsConnected);
        Assert.False(player.IsReady);
    }

    [Fact]
    public void Join_room_assigns_second_seat_and_broadcasts_room_state()
    {
        var coordinator = CreateCoordinator();
        var roomId = GetCreated(coordinator.CreateRoom("connection-1")).RoomId;

        var result = coordinator.JoinRoom("connection-2", roomId);

        var joined = Assert.IsType<RoomJoinedMessage>(
            result.Messages.Select(message => message.Message).OfType<RoomJoinedMessage>().Single());
        Assert.Equal("player-2", joined.PlayerId);
        Assert.Equal(2, joined.Room.Players.Single(player => player.PlayerId == joined.PlayerId).Seat);
        Assert.Equal(RoomStatusDto.ReadyCheck, joined.Room.Status);

        var roomState = Assert.IsType<RoomStateMessage>(
            result.Messages.Select(message => message.Message).OfType<RoomStateMessage>().Single());
        Assert.Equal(RoomStatusDto.ReadyCheck, roomState.Room.Status);
        Assert.Equal(2, roomState.Room.Players.Count);
        Assert.Contains(result.Messages, message =>
            message.Message is RoomStateMessage && message.ConnectionIds.Count == 2);
    }

    [Fact]
    public void Resume_room_with_valid_token_reclaims_same_player_and_replaces_old_connection()
    {
        var coordinator = CreateCoordinator();
        var created = GetCreated(coordinator.CreateRoom("connection-1"));

        var result = coordinator.ResumeRoom("connection-1b", created.RoomId, created.PlayerSessionToken);

        var resumed = Assert.IsType<RoomResumedMessage>(
            result.Messages.Select(message => message.Message).OfType<RoomResumedMessage>().Single());
        Assert.Equal(created.PlayerId, resumed.PlayerId);
        Assert.Equal(1, resumed.Room.Players.Single(player => player.PlayerId == created.PlayerId).Seat);
        Assert.True(resumed.Room.Players.Single(player => player.PlayerId == created.PlayerId).IsConnected);

        var closure = Assert.Single(result.ConnectionsToClose);
        Assert.Equal("connection-1", closure.ConnectionId);

        var staleDisconnect = coordinator.MarkDisconnected("connection-1");
        Assert.Empty(staleDisconnect.Messages);
    }

    [Fact]
    public void Resume_room_with_invalid_token_is_rejected()
    {
        var coordinator = CreateCoordinator();
        var roomId = GetCreated(coordinator.CreateRoom("connection-1")).RoomId;

        var result = coordinator.ResumeRoom("connection-2", roomId, "not-the-token");

        var error = Assert.IsType<ErrorMessage>(Assert.Single(result.Messages).Message);
        Assert.Equal("invalidPlayerSessionToken", error.Code);
    }

    [Fact]
    public void Game_starts_only_after_both_players_are_ready_and_start_time_is_due()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var coordinator = CreateCoordinator(clock: clock);
        var created = GetCreated(coordinator.CreateRoom("connection-1"));
        coordinator.JoinRoom("connection-2", created.RoomId);

        var firstReady = coordinator.SetReady("connection-1", created.RoomId, isReady: true);

        Assert.DoesNotContain(firstReady.Messages, message => message.Message is GameStartingMessage);

        var secondReady = coordinator.SetReady("connection-2", created.RoomId, isReady: true);

        var starting = Assert.IsType<GameStartingMessage>(
            secondReady.Messages.Select(message => message.Message).OfType<GameStartingMessage>().Single());
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(3).ToUnixTimeMilliseconds(), starting.StartServerTime);
        Assert.Equal(2, starting.TickRate);
        Assert.Equal(500, starting.Timing.TickDurationMs);
        Assert.Equal(100, starting.Timing.AnimationFrameDurationMs);

        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Empty(coordinator.ProcessTimers().Messages);

        clock.Advance(TimeSpan.FromSeconds(1));
        var due = coordinator.ProcessTimers();

        Assert.Equal(2, due.Messages.Count(message => message.Message is GameStartedMessage));
        var inGameState = due.Messages
            .Select(message => message.Message)
            .OfType<RoomStateMessage>()
            .Single();
        Assert.Equal(RoomStatusDto.InGame, inGameState.Room.Status);
    }

    [Fact]
    public void Game_start_broadcasts_initial_authoritative_frame_with_snakes_and_food()
    {
        var coordinator = CreateCoordinator(options: new GameRuntimeOptions { StartCountdownSeconds = 0 });
        var created = GetCreated(coordinator.CreateRoom("connection-1"));
        coordinator.JoinRoom("connection-2", created.RoomId);
        coordinator.SetReady("connection-1", created.RoomId, isReady: true);
        coordinator.SetReady("connection-2", created.RoomId, isReady: true);

        var result = coordinator.ProcessTimers();

        var frame = Assert.IsType<AuthoritativeFrameMessage>(
            result.Messages.Select(message => message.Message).OfType<AuthoritativeFrameMessage>().Single());
        Assert.Equal(created.RoomId, frame.RoomId);
        Assert.Equal("match-1", frame.MatchId);
        Assert.Equal(0, frame.Tick);
        Assert.Equal("Running", frame.State.Status);
        Assert.Equal(32, frame.State.Board.Width);
        Assert.Equal(24, frame.State.Board.Height);
        Assert.Equal(2, frame.State.Snakes.Count);
        Assert.Equal(2, frame.State.Food.Count);
        Assert.Contains(frame.State.Snakes, snake =>
            snake.PlayerId == "player-1" &&
            snake.Alive &&
            snake.Body.Count > 0 &&
            snake.Head == snake.Body[0]);
        Assert.Contains(frame.State.Snakes, snake =>
            snake.PlayerId == "player-2" &&
            snake.Alive &&
            snake.Body.Count > 0 &&
            snake.Head == snake.Body[0]);
        Assert.All(frame.State.Food, food =>
            Assert.Contains(frame.State.Snakes, snake => snake.PlayerId == food.OwnerPlayerId));
    }

    [Fact]
    public void Finish_game_broadcasts_game_finished_and_returns_room_to_postgame()
    {
        var coordinator = CreateCoordinator(options: new GameRuntimeOptions { StartCountdownSeconds = 0 });
        var created = GetCreated(coordinator.CreateRoom("connection-1"));
        coordinator.JoinRoom("connection-2", created.RoomId);
        coordinator.SetReady("connection-1", created.RoomId, isReady: true);
        coordinator.SetReady("connection-2", created.RoomId, isReady: true);
        coordinator.ProcessTimers();

        var result = coordinator.FinishGame(created.RoomId, "teamLost", "engineFinished");

        var finished = Assert.IsType<GameFinishedMessage>(
            result.Messages.Select(message => message.Message).OfType<GameFinishedMessage>().Single());
        Assert.Equal("teamLost", finished.Result);
        Assert.Equal("engineFinished", finished.Reason);

        var state = result.Messages
            .Select(message => message.Message)
            .OfType<RoomStateMessage>()
            .Single();
        Assert.Equal(RoomStatusDto.PostGame, state.Room.Status);
        Assert.All(state.Room.Players, player => Assert.False(player.IsReady));
    }

    [Fact]
    public void Input_inside_grace_window_applies_to_current_tick()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var coordinator = CreateCoordinator(
            clock: clock,
            options: new GameRuntimeOptions { StartCountdownSeconds = 0 });
        var created = StartGame(coordinator);

        var inputResult = coordinator.SubmitInput(
            "connection-1",
            new ClientInputMessage(created.RoomId, DirectionDto.Up, ClientTime: 50, ClientSequence: 7),
            serverReceivedAt: 50);

        var accepted = Assert.IsType<TurnIntentAcceptedMessage>(
            Assert.Single(inputResult.Messages).Message);
        Assert.Equal(created.RoomId, accepted.RoomId);
        Assert.Equal("match-1", accepted.MatchId);
        Assert.Equal(created.PlayerId, accepted.PlayerId);
        Assert.Equal(DirectionDto.Up, accepted.Direction);
        Assert.Equal(0, accepted.EffectiveTick);
        Assert.Equal(50, accepted.ClientTime);
        Assert.Equal(7, accepted.ClientSequence);
        Assert.Equal(50, accepted.ServerReceivedAt);

        clock.Advance(TimeSpan.FromMilliseconds(500));
        var result = coordinator.ProcessTimers();

        var frame = result.Messages
            .Select(message => message.Message)
            .OfType<AuthoritativeFrameMessage>()
            .Single(message => message.Tick == 1);
        var localSnake = frame.State.Snakes.Single(snake => snake.PlayerId == created.PlayerId);
        Assert.Equal(DirectionDto.Up, localSnake.Direction);
        Assert.Equal(new CellDto(8, 9), localSnake.Head);
    }

    [Fact]
    public void Input_after_grace_window_applies_to_next_tick()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var coordinator = CreateCoordinator(
            clock: clock,
            options: new GameRuntimeOptions { StartCountdownSeconds = 0 });
        var created = StartGame(coordinator);

        var inputResult = coordinator.SubmitInput(
            "connection-1",
            new ClientInputMessage(created.RoomId, DirectionDto.Up, ClientTime: 150),
            serverReceivedAt: 150);

        var accepted = Assert.IsType<TurnIntentAcceptedMessage>(
            Assert.Single(inputResult.Messages).Message);
        Assert.Equal(created.PlayerId, accepted.PlayerId);
        Assert.Equal(DirectionDto.Up, accepted.Direction);
        Assert.Equal(1, accepted.EffectiveTick);

        clock.Advance(TimeSpan.FromMilliseconds(500));
        var firstTick = coordinator.ProcessTimers();

        var frameOne = firstTick.Messages
            .Select(message => message.Message)
            .OfType<AuthoritativeFrameMessage>()
            .Single(message => message.Tick == 1);
        var firstLocalSnake = frameOne.State.Snakes.Single(snake => snake.PlayerId == created.PlayerId);
        Assert.Equal(DirectionDto.Up, firstLocalSnake.Direction);
        Assert.Equal(new CellDto(9, 8), firstLocalSnake.Head);

        clock.Advance(TimeSpan.FromMilliseconds(500));
        var secondTick = coordinator.ProcessTimers();

        var frameTwo = secondTick.Messages
            .Select(message => message.Message)
            .OfType<AuthoritativeFrameMessage>()
            .Single(message => message.Tick == 2);
        var secondLocalSnake = frameTwo.State.Snakes.Single(snake => snake.PlayerId == created.PlayerId);
        Assert.Equal(DirectionDto.Up, secondLocalSnake.Direction);
        Assert.Equal(new CellDto(9, 9), secondLocalSnake.Head);
    }

    [Fact]
    public void Disconnected_ingame_player_can_resume_before_grace_period_or_forfeits_after_it()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var coordinator = CreateCoordinator(
            clock: clock,
            options: new GameRuntimeOptions
            {
                StartCountdownSeconds = 0,
                DisconnectGracePeriodSeconds = 10
            });
        var created = GetCreated(coordinator.CreateRoom("connection-1"));
        coordinator.JoinRoom("connection-2", created.RoomId);
        coordinator.SetReady("connection-1", created.RoomId, isReady: true);
        coordinator.SetReady("connection-2", created.RoomId, isReady: true);
        coordinator.ProcessTimers();

        var disconnected = coordinator.MarkDisconnected("connection-1");
        var disconnectedState = disconnected.Messages
            .Select(message => message.Message)
            .OfType<RoomStateMessage>()
            .Single();
        Assert.Equal(RoomStatusDto.InGame, disconnectedState.Room.Status);
        Assert.False(disconnectedState.Room.Players.Single(player => player.PlayerId == created.PlayerId).IsConnected);
        Assert.True(disconnectedState.Room.Players.Single(player => player.PlayerId == created.PlayerId).IsReady);

        clock.Advance(TimeSpan.FromSeconds(9));
        Assert.DoesNotContain(coordinator.ProcessTimers().Messages, message => message.Message is GameFinishedMessage);

        var resumed = coordinator.ResumeRoom("connection-1b", created.RoomId, created.PlayerSessionToken);
        var resumedMessage = Assert.IsType<RoomResumedMessage>(
            resumed.Messages.Select(message => message.Message).OfType<RoomResumedMessage>().Single());
        Assert.Equal(RoomStatusDto.InGame, resumedMessage.Room.Status);
        Assert.True(resumedMessage.Room.Players.Single(player => player.PlayerId == created.PlayerId).IsConnected);
        Assert.True(resumedMessage.Room.Players.Single(player => player.PlayerId == created.PlayerId).IsReady);

        var resumedGame = Assert.IsType<GameStartedMessage>(
            resumed.Messages.Select(message => message.Message).OfType<GameStartedMessage>().Single());
        Assert.Equal(created.RoomId, resumedGame.RoomId);
        Assert.Equal(created.PlayerId, resumedGame.PlayerId);
        Assert.Equal("match-1", resumedGame.MatchId);

        coordinator.MarkDisconnected("connection-1b");
        clock.Advance(TimeSpan.FromSeconds(10));
        var forfeited = coordinator.ProcessTimers();

        var finished = Assert.IsType<GameFinishedMessage>(
            forfeited.Messages.Select(message => message.Message).OfType<GameFinishedMessage>().Single());
        Assert.Equal("forfeit", finished.Result);
        Assert.Equal("disconnectTimeout", finished.Reason);

        var postGame = forfeited.Messages
            .Select(message => message.Message)
            .OfType<RoomStateMessage>()
            .Single();
        Assert.Equal(RoomStatusDto.PostGame, postGame.Room.Status);
    }

    private static GameRoomCoordinator CreateCoordinator(
        ManualTimeProvider? clock = null,
        GameRuntimeOptions? options = null) =>
        new(
            options ?? new GameRuntimeOptions(),
            new DeterministicIdentityProvider(),
            clock ?? new ManualTimeProvider(DateTimeOffset.UnixEpoch));

    private static RoomCreatedMessage GetCreated(RoomCommandResult result) =>
        Assert.IsType<RoomCreatedMessage>(Assert.Single(result.Messages).Message);

    private static RoomCreatedMessage StartGame(GameRoomCoordinator coordinator)
    {
        var created = GetCreated(coordinator.CreateRoom("connection-1"));
        coordinator.JoinRoom("connection-2", created.RoomId);
        coordinator.SetReady("connection-1", created.RoomId, isReady: true);
        coordinator.SetReady("connection-2", created.RoomId, isReady: true);
        coordinator.ProcessTimers();
        return created;
    }

    private sealed class DeterministicIdentityProvider : IRoomIdentityProvider
    {
        private int _roomSequence;
        private int _playerSequence;
        private int _tokenSequence;
        private int _matchSequence;

        public string CreateRoomId() => $"ROOM{++_roomSequence}";

        public string CreatePlayerId() => $"player-{++_playerSequence}";

        public string CreatePlayerSessionToken() => $"token-{++_tokenSequence}";

        public string CreateMatchId() => $"match-{++_matchSequence}";

        public int CreateSeed() => 8675309 + _matchSequence;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
