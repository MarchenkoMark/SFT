namespace SnakeForTwo.Game.Domain;

public enum Direction
{
    Up,
    Right,
    Down,
    Left
}

public readonly record struct GameTick(long Value)
{
    public static GameTick Zero { get; } = new(0);

    public GameTick Next() => new(Value + 1);
}

public readonly record struct PlayerId(string Value);

public readonly record struct Cell(int X, int Y);

public sealed record GameRules(
    int BoardWidth,
    int BoardHeight,
    double TilesPerSecond,
    int RollbackWindowTicks)
{
    public double TicksPerSecond => TilesPerSecond;
}
