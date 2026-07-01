namespace SnakeForTwo.Api;

internal sealed class WebSocketRateLimitOptions
{
    public const string SectionName = "Realtime";

    public int MaxMessagesPerSecond { get; init; } = 60;

    public int MaxInputsPerSecond { get; init; } = 20;
}
