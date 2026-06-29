using SnakeForTwo.Game.Domain;

namespace SnakeForTwo.Game.Domain.Tests;

public sealed class RollbackGameSessionTests
{
    private static readonly PlayerId PlayerOne = new("player-1");
    private static readonly PlayerId PlayerTwo = new("player-2");
    private static readonly SnakeColor Red = new("red");
    private static readonly SnakeColor Blue = new("blue");

    [Fact]
    public void Late_input_for_an_already_simulated_tick_rolls_back_and_resimulates()
    {
        var session = CreateSession();
        Advance(session, 3);

        var result = session.SubmitInput(PlayerOne, new GameTick(1), Direction.Up);

        Assert.Equal(InputSchedulingStatus.Scheduled, result.Status);
        Assert.True(result.RolledBack);
        Assert.Equal(new GameTick(1), result.RollbackFromTick);
        Assert.NotEmpty(result.Corrections);
        Assert.Equal(new GameTick(3), session.CurrentTick);
        Assert.Equal(new Cell(2, 3), session.CurrentState.GetSnake(PlayerOne).Head);
    }

    [Fact]
    public void Late_input_that_does_not_change_canonical_inputs_does_not_trigger_correction()
    {
        var session = CreateSession();
        Advance(session, 3);

        var result = session.SubmitInput(PlayerOne, new GameTick(1), Direction.Right);

        Assert.Equal(InputSchedulingStatus.Duplicate, result.Status);
        Assert.False(result.RolledBack);
        Assert.Empty(result.Corrections);
        Assert.Equal(new Cell(4, 1), session.CurrentState.GetSnake(PlayerOne).Head);
    }

    [Fact]
    public void Inputs_older_than_the_rollback_window_are_rejected()
    {
        var session = CreateSession(Rules(rollbackWindowTicks: 2));
        Advance(session, 5);

        var result = session.SubmitInput(PlayerOne, new GameTick(2), Direction.Up);

        Assert.Equal(InputSchedulingStatus.RejectedStale, result.Status);
        Assert.False(result.RolledBack);
    }

    [Fact]
    public void Inputs_too_far_in_the_future_are_rejected()
    {
        var session = CreateSession(Rules(inputFutureBufferTicks: 2));

        var result = session.SubmitInput(PlayerOne, new GameTick(3), Direction.Up);

        Assert.Equal(InputSchedulingStatus.RejectedFuture, result.Status);
        Assert.False(result.RolledBack);
    }

    [Fact]
    public void Resimulation_from_the_same_snapshot_and_inputs_is_deterministic()
    {
        var first = CreateSession();
        var second = CreateSession();
        Advance(first, 3);
        Advance(second, 3);

        first.SubmitInput(PlayerOne, new GameTick(1), Direction.Up);
        second.SubmitInput(PlayerOne, new GameTick(1), Direction.Up);

        Assert.Equal(
            SnakeGameEngine.ComputeStateHash(first.CurrentState),
            SnakeGameEngine.ComputeStateHash(second.CurrentState));
    }

    [Fact]
    public void Multiple_late_inputs_are_applied_in_deterministic_order()
    {
        var session = CreateSession(
            Rules(),
            Snake(PlayerOne, Red, Direction.Up, new Cell(4, 4)),
            Snake(PlayerTwo, Blue, Direction.Left, new Cell(10, 10)));
        Advance(session, 4);

        var left = session.SubmitInput(PlayerOne, new GameTick(1), Direction.Left);
        var down = session.SubmitInput(PlayerOne, new GameTick(1), Direction.Down);

        Assert.Equal(new GameTick(1), left.ScheduledTick);
        Assert.Equal(new GameTick(2), down.ScheduledTick);
        Assert.True(left.RolledBack);
        Assert.True(down.RolledBack);
        Assert.Equal(new Cell(3, 3), session.CurrentState.GetSnake(PlayerOne).Head);
    }

    [Fact]
    public void Rollback_stops_when_late_input_finishes_the_game_earlier_than_the_current_tick()
    {
        var session = CreateSession(
            Rules(),
            Snake(
                PlayerOne,
                Red,
                Direction.Right,
                new Cell(1, 1),
                new Cell(1, 2),
                new Cell(2, 2),
                new Cell(3, 2),
                new Cell(4, 2)),
            Snake(PlayerTwo, Blue, Direction.Left, new Cell(10, 10)));
        Advance(session, 3);

        var result = session.SubmitInput(PlayerOne, new GameTick(1), Direction.Up);

        Assert.True(result.RolledBack);
        Assert.Equal(GameStatus.Finished, session.CurrentState.Status);
        Assert.Equal(GameOverReason.SelfCollision, session.CurrentState.GameOverReason);
        Assert.Equal(new GameTick(2), session.CurrentTick);
        Assert.Contains(result.Corrections, frame => frame.Tick == new GameTick(2));
    }

    private static void Advance(RollbackGameSession session, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            session.AdvanceOneTick();
        }
    }

    private static RollbackGameSession CreateSession(GameRules? rules = null) =>
        CreateSession(
            rules ?? Rules(),
            Snake(PlayerOne, Red, Direction.Right, new Cell(1, 1)),
            Snake(PlayerTwo, Blue, Direction.Left, new Cell(10, 10)));

    private static RollbackGameSession CreateSession(GameRules rules, SnakeSpawn first, SnakeSpawn second)
    {
        var state = SnakeGameEngine.CreateInitialState(
            rules,
            new[] { first, second },
            new[] { Food(PlayerOne, new Cell(0, 0)), Food(PlayerTwo, new Cell(11, 11)) });
        return new RollbackGameSession(state);
    }

    private static GameRules Rules(int rollbackWindowTicks = 8, int inputFutureBufferTicks = 4) =>
        new(
            BoardWidth: 12,
            BoardHeight: 12,
            TilesPerSecond: 2,
            RollbackWindowTicks: rollbackWindowTicks,
            WallWrapping: true,
            FoodSeed: 2468,
            InputFutureBufferTicks: inputFutureBufferTicks);

    private static SnakeSpawn Snake(
        PlayerId playerId,
        SnakeColor color,
        Direction direction,
        params Cell[] body) =>
        new(playerId, color, direction, body);

    private static FoodItem Food(PlayerId playerId, Cell cell) =>
        playerId == PlayerOne
            ? new FoodItem(PlayerOne, Red, cell)
            : new FoodItem(PlayerTwo, Blue, cell);
}
