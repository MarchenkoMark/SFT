using SnakeForTwo.Game.Domain;

namespace SnakeForTwo.Game.Domain.Tests;

public sealed class SnakeGameEngineTests
{
    private static readonly PlayerId PlayerOne = new("player-1");
    private static readonly PlayerId PlayerTwo = new("player-2");
    private static readonly SnakeColor Red = new("red");
    private static readonly SnakeColor Blue = new("blue");

    [Fact]
    public void Advance_moves_each_snake_one_cell_per_tick()
    {
        var state = CreateState(
            Snake(PlayerOne, Red, Direction.Right, new Cell(1, 1)),
            Snake(PlayerTwo, Blue, Direction.Left, new Cell(6, 6)));

        var next = SnakeGameEngine.Advance(state, TickInputs.Empty);

        Assert.Equal(new GameTick(1), next.Tick);
        Assert.Equal(new Cell(2, 1), next.GetSnake(PlayerOne).Head);
        Assert.Equal(new Cell(5, 6), next.GetSnake(PlayerTwo).Head);
        Assert.Equal(GameStatus.Running, next.Status);
    }

    [Fact]
    public void Advance_applies_direction_changes_on_the_current_tick()
    {
        var state = CreateState(
            Snake(PlayerOne, Red, Direction.Right, new Cell(1, 1)),
            Snake(PlayerTwo, Blue, Direction.Left, new Cell(6, 6)));

        var next = SnakeGameEngine.Advance(state, TickInputs.ForPlayer(PlayerOne, Direction.Up));

        Assert.Equal(Direction.Up, next.GetSnake(PlayerOne).Direction);
        Assert.Equal(new Cell(1, 2), next.GetSnake(PlayerOne).Head);
    }

    [Fact]
    public void Own_food_grows_the_snake_by_one_and_respawns_owned_food()
    {
        var state = CreateState(
            Snake(PlayerOne, Red, Direction.Right, new Cell(1, 1), new Cell(0, 1)),
            Snake(PlayerTwo, Blue, Direction.Left, new Cell(6, 6)),
            Food(PlayerOne, new Cell(2, 1)),
            Food(PlayerTwo, new Cell(7, 7)));

        var next = SnakeGameEngine.Advance(state, TickInputs.Empty);

        Assert.Equal(3, next.GetSnake(PlayerOne).Body.Count);
        Assert.Contains(next.Food, food => food.OwnerPlayerId == PlayerOne);
        Assert.DoesNotContain(next.Food, food => food.Cell == new Cell(2, 1));
    }

    [Fact]
    public void Someone_elses_food_shrinks_the_snake_by_one()
    {
        var state = CreateState(
            Snake(PlayerOne, Red, Direction.Right, new Cell(1, 1), new Cell(0, 1)),
            Snake(PlayerTwo, Blue, Direction.Left, new Cell(6, 6)),
            Food(PlayerOne, new Cell(0, 0)),
            Food(PlayerTwo, new Cell(2, 1)));

        var next = SnakeGameEngine.Advance(state, TickInputs.Empty);

        Assert.Single(next.GetSnake(PlayerOne).Body);
        Assert.Equal(GameStatus.Running, next.Status);
    }

    [Fact]
    public void Eating_someone_elses_food_at_length_one_finishes_the_game()
    {
        var state = CreateState(
            Snake(PlayerOne, Red, Direction.Right, new Cell(1, 1)),
            Snake(PlayerTwo, Blue, Direction.Left, new Cell(6, 6)),
            Food(PlayerOne, new Cell(0, 0)),
            Food(PlayerTwo, new Cell(2, 1)));

        var next = SnakeGameEngine.Advance(state, TickInputs.Empty);

        Assert.Empty(next.GetSnake(PlayerOne).Body);
        Assert.Equal(GameStatus.Finished, next.Status);
        Assert.Equal(GameOverReason.LengthDepleted, next.GameOverReason);
    }

    [Fact]
    public void Spawned_food_is_deterministic_for_the_seed_and_records_owner()
    {
        var rules = Rules(foodSeed: 1234);
        var snakes = new[]
        {
            Snake(PlayerOne, Red, Direction.Right, new Cell(1, 1)),
            Snake(PlayerTwo, Blue, Direction.Left, new Cell(6, 6))
        };

        var first = SnakeGameEngine.CreateInitialState(rules, snakes);
        var second = SnakeGameEngine.CreateInitialState(rules, snakes);

        Assert.Equal(first.Food, second.Food);
        Assert.Contains(first.Food, food => food.OwnerPlayerId == PlayerOne && food.Color == Red);
        Assert.Contains(first.Food, food => food.OwnerPlayerId == PlayerTwo && food.Color == Blue);
        Assert.DoesNotContain(first.Food, food => first.Snakes.SelectMany(snake => snake.Body).Contains(food.Cell));
    }

    [Fact]
    public void Walls_wrap_when_enabled()
    {
        var state = CreateState(
            Snake(PlayerOne, Red, Direction.Right, new Cell(7, 3)),
            Snake(PlayerTwo, Blue, Direction.Left, new Cell(6, 6)));

        var next = SnakeGameEngine.Advance(state, TickInputs.Empty);

        Assert.Equal(new Cell(0, 3), next.GetSnake(PlayerOne).Head);
        Assert.Equal(GameStatus.Running, next.Status);
    }

    [Fact]
    public void Walls_finish_the_game_when_wrapping_is_disabled()
    {
        var state = CreateState(
            Snake(PlayerOne, Red, Direction.Right, new Cell(7, 3)),
            Snake(PlayerTwo, Blue, Direction.Left, new Cell(6, 6)),
            Food(PlayerOne, new Cell(0, 0)),
            Food(PlayerTwo, new Cell(7, 7)),
            Rules(wallWrapping: false));

        var next = SnakeGameEngine.Advance(state, TickInputs.Empty);

        Assert.Equal(GameStatus.Finished, next.Status);
        Assert.Equal(GameOverReason.WallCollision, next.GameOverReason);
    }

    [Fact]
    public void Self_collision_finishes_the_game()
    {
        var state = CreateState(
            Snake(
                PlayerOne,
                Red,
                Direction.Left,
                new Cell(2, 2),
                new Cell(2, 1),
                new Cell(1, 1),
                new Cell(1, 2),
                new Cell(0, 2)),
            Snake(PlayerTwo, Blue, Direction.Left, new Cell(6, 6)));

        var next = SnakeGameEngine.Advance(state, TickInputs.Empty);

        Assert.Equal(GameStatus.Finished, next.Status);
        Assert.Equal(GameOverReason.SelfCollision, next.GameOverReason);
    }

    [Fact]
    public void Snake_to_snake_collision_finishes_the_game()
    {
        var state = CreateState(
            Snake(PlayerOne, Red, Direction.Right, new Cell(1, 1)),
            Snake(PlayerTwo, Blue, Direction.Up, new Cell(2, 2), new Cell(2, 1), new Cell(2, 0)));

        var next = SnakeGameEngine.Advance(state, TickInputs.Empty);

        Assert.Equal(GameStatus.Finished, next.Status);
        Assert.Equal(GameOverReason.SnakeCollision, next.GameOverReason);
    }

    [Fact]
    public void Snapshot_restore_returns_an_independent_state_copy()
    {
        var state = CreateState(
            Snake(PlayerOne, Red, Direction.Right, new Cell(1, 1)),
            Snake(PlayerTwo, Blue, Direction.Left, new Cell(6, 6)));
        var snapshot = GameSnapshot.Capture(state);

        var restored = snapshot.Restore();
        var advanced = SnakeGameEngine.Advance(restored, TickInputs.Empty);
        var restoredAgain = snapshot.Restore();

        Assert.Equal(new Cell(1, 1), restoredAgain.GetSnake(PlayerOne).Head);
        Assert.Equal(new Cell(2, 1), advanced.GetSnake(PlayerOne).Head);
    }

    private static GameState CreateState(SnakeSpawn first, SnakeSpawn second) =>
        CreateState(first, second, Food(PlayerOne, new Cell(0, 0)), Food(PlayerTwo, new Cell(7, 7)));

    private static GameState CreateState(
        SnakeSpawn first,
        SnakeSpawn second,
        FoodItem firstFood,
        FoodItem secondFood,
        GameRules? rules = null) =>
        SnakeGameEngine.CreateInitialState(rules ?? Rules(), new[] { first, second }, new[] { firstFood, secondFood });

    private static GameRules Rules(bool wallWrapping = true, int foodSeed = 8675309) =>
        new(
            BoardWidth: 8,
            BoardHeight: 8,
            TilesPerSecond: 2,
            RollbackWindowTicks: 8,
            WallWrapping: wallWrapping,
            FoodSeed: foodSeed);

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
