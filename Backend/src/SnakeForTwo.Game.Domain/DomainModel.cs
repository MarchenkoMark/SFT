namespace SnakeForTwo.Game.Domain;

public enum Direction
{
    Up,
    Right,
    Down,
    Left
}

public enum GameStatus
{
    Running,
    Finished
}

public enum GameOverReason
{
    None,
    WallCollision,
    SelfCollision,
    SnakeCollision,
    LengthDepleted
}

public readonly record struct GameTick(long Value)
{
    public static GameTick Zero { get; } = new(0);

    public GameTick Next() => new(Value + 1);
}

public readonly record struct PlayerId(string Value);

public readonly record struct SnakeColor(string Value);

public readonly record struct Cell(int X, int Y);

public sealed record GameRules
{
    public GameRules(
        int BoardWidth,
        int BoardHeight,
        double TilesPerSecond,
        int RollbackWindowTicks,
        bool WallWrapping = true,
        int FoodSeed = 0,
        int InputFutureBufferTicks = 4,
        int MaxQueuedInputsPerPlayer = 4)
    {
        if (BoardWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BoardWidth), "Board width must be positive.");
        }

        if (BoardHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BoardHeight), "Board height must be positive.");
        }

        if (TilesPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TilesPerSecond), "Tiles per second must be positive.");
        }

        if (RollbackWindowTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RollbackWindowTicks), "Rollback window cannot be negative.");
        }

        if (InputFutureBufferTicks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(InputFutureBufferTicks), "Future input buffer cannot be negative.");
        }

        if (MaxQueuedInputsPerPlayer <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxQueuedInputsPerPlayer),
                "Queued inputs per player must be positive.");
        }

        this.BoardWidth = BoardWidth;
        this.BoardHeight = BoardHeight;
        this.TilesPerSecond = TilesPerSecond;
        this.RollbackWindowTicks = RollbackWindowTicks;
        this.WallWrapping = WallWrapping;
        this.FoodSeed = FoodSeed;
        this.InputFutureBufferTicks = InputFutureBufferTicks;
        this.MaxQueuedInputsPerPlayer = MaxQueuedInputsPerPlayer;
    }

    public int BoardWidth { get; init; }

    public int BoardHeight { get; init; }

    public double TilesPerSecond { get; init; }

    public int RollbackWindowTicks { get; init; }

    public bool WallWrapping { get; init; }

    public int FoodSeed { get; init; }

    public int InputFutureBufferTicks { get; init; }

    public int MaxQueuedInputsPerPlayer { get; init; }

    public double TicksPerSecond => TilesPerSecond;
}

public sealed class SnakeSpawn
{
    public SnakeSpawn(PlayerId playerId, SnakeColor color, Direction direction, IEnumerable<Cell> body)
    {
        PlayerId = playerId;
        Color = color;
        Direction = direction;
        Body = body.ToArray();

        if (Body.Count == 0)
        {
            throw new ArgumentException("A snake spawn must contain at least one body cell.", nameof(body));
        }
    }

    public PlayerId PlayerId { get; }

    public SnakeColor Color { get; }

    public Direction Direction { get; }

    public IReadOnlyList<Cell> Body { get; }
}

public sealed class Snake
{
    public Snake(PlayerId playerId, SnakeColor color, Direction direction, IEnumerable<Cell> body)
    {
        PlayerId = playerId;
        Color = color;
        Direction = direction;
        Body = body.ToArray();
    }

    public PlayerId PlayerId { get; }

    public SnakeColor Color { get; }

    public Direction Direction { get; }

    public IReadOnlyList<Cell> Body { get; }

    public bool Alive => Body.Count > 0;

    public Cell Head => Body.Count > 0
        ? Body[0]
        : throw new InvalidOperationException("A zero-length snake does not have a head.");

    public Snake With(Direction? direction = null, IEnumerable<Cell>? body = null) =>
        new(PlayerId, Color, direction ?? Direction, body ?? Body);
}

public sealed record FoodItem(PlayerId OwnerPlayerId, SnakeColor Color, Cell Cell);

public sealed record FoodEatenEvent(
    GameTick Tick,
    PlayerId EaterPlayerId,
    PlayerId OwnerPlayerId,
    Cell Cell);

public sealed class GameState
{
    public GameState(
        GameTick tick,
        GameRules rules,
        GameStatus status,
        IEnumerable<Snake> snakes,
        IEnumerable<FoodItem> food,
        GameOverReason gameOverReason = GameOverReason.None,
        int foodSpawnCursor = 0)
    {
        Tick = tick;
        Rules = rules;
        Status = status;
        Snakes = snakes.Select(snake => snake.With()).ToArray();
        Food = food.ToArray();
        GameOverReason = gameOverReason;
        FoodSpawnCursor = foodSpawnCursor;
    }

    public GameTick Tick { get; }

    public GameRules Rules { get; }

    public GameStatus Status { get; }

    public IReadOnlyList<Snake> Snakes { get; }

    public IReadOnlyList<FoodItem> Food { get; }

    public GameOverReason GameOverReason { get; }

    public int FoodSpawnCursor { get; }

    public Snake GetSnake(PlayerId playerId) =>
        Snakes.Single(snake => snake.PlayerId == playerId);

    public GameState With(
        GameTick? tick = null,
        GameStatus? status = null,
        IEnumerable<Snake>? snakes = null,
        IEnumerable<FoodItem>? food = null,
        GameOverReason? gameOverReason = null,
        int? foodSpawnCursor = null) =>
        new(
            tick ?? Tick,
            Rules,
            status ?? Status,
            snakes ?? Snakes,
            food ?? Food,
            gameOverReason ?? GameOverReason,
            foodSpawnCursor ?? FoodSpawnCursor);

    public GameState Copy() => With();
}

public sealed class TickInputs
{
    private readonly IReadOnlyDictionary<PlayerId, Direction> _directions;

    public TickInputs(IEnumerable<KeyValuePair<PlayerId, Direction>> directions)
    {
        _directions = directions.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public static TickInputs Empty { get; } = new(Array.Empty<KeyValuePair<PlayerId, Direction>>());

    public IReadOnlyDictionary<PlayerId, Direction> Directions => _directions;

    public static TickInputs ForPlayer(PlayerId playerId, Direction direction) =>
        new(new[] { KeyValuePair.Create(playerId, direction) });

    public Direction DirectionFor(PlayerId playerId, Direction fallback) =>
        _directions.TryGetValue(playerId, out var direction)
            ? direction
            : fallback;
}

public sealed class GameSnapshot
{
    private GameSnapshot(GameTick tick, GameState state)
    {
        Tick = tick;
        State = state.Copy();
    }

    public GameTick Tick { get; }

    public GameState State { get; }

    public static GameSnapshot Capture(GameState state) => new(state.Tick, state);

    public GameState Restore() => State.Copy();
}

public static class DirectionExtensions
{
    public static bool IsOpposite(this Direction direction, Direction other) =>
        direction switch
        {
            Direction.Up => other == Direction.Down,
            Direction.Right => other == Direction.Left,
            Direction.Down => other == Direction.Up,
            Direction.Left => other == Direction.Right,
            _ => false
        };
}
