namespace SnakeForTwo.Game.Application;

public sealed class GameRuntimeOptions
{
    public const string SectionName = "Game";

    public double TilesPerSecond { get; init; } = 2;

    public int AnimationFramesPerTile { get; init; } = 5;

    public int RollbackHistorySeconds { get; init; } = 5;

    public int InputFutureBufferTicks { get; init; } = 4;

    public int DisconnectGracePeriodSeconds { get; init; } = 10;

    public int DisconnectedPlayerRetentionSeconds { get; init; } = 300;

    public int StartCountdownSeconds { get; init; } = 3;

    public int BoardWidth { get; init; } = 32;

    public int BoardHeight { get; init; } = 24;

    public int InputGraceMs { get; init; } = 0;

    public double TicksPerSecond => Math.Max(TilesPerSecond, 0.001);

    public TimeSpan TickDuration => TimeSpan.FromSeconds(1 / TicksPerSecond);

    public TimeSpan AnimationFrameDuration => TimeSpan.FromSeconds(
        1 / (TicksPerSecond * Math.Max(AnimationFramesPerTile, 1)));

    public TimeSpan DisconnectedPlayerRetention => TimeSpan.FromSeconds(
        Math.Max(DisconnectGracePeriodSeconds, DisconnectedPlayerRetentionSeconds));

    public TimeSpan InputGrace => TimeSpan.FromMilliseconds(InputGraceMs > 0
        ? InputGraceMs
        : AnimationFrameDuration.TotalMilliseconds);
}
