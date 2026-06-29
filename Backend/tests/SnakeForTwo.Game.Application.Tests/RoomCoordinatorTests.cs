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

        clock.Advance(TimeSpan.FromSeconds(9));
        Assert.DoesNotContain(coordinator.ProcessTimers().Messages, message => message.Message is GameFinishedMessage);

        var resumed = coordinator.ResumeRoom("connection-1b", created.RoomId, created.PlayerSessionToken);
        var resumedMessage = Assert.IsType<RoomResumedMessage>(
            resumed.Messages.Select(message => message.Message).OfType<RoomResumedMessage>().Single());
        Assert.Equal(RoomStatusDto.InGame, resumedMessage.Room.Status);
        Assert.True(resumedMessage.Room.Players.Single(player => player.PlayerId == created.PlayerId).IsConnected);

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
