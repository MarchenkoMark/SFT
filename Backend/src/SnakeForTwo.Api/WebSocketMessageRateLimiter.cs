namespace SnakeForTwo.Api;

internal sealed class WebSocketMessageRateLimiter(
    WebSocketRateLimitOptions options,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(1);

    private readonly Queue<DateTimeOffset> _messageTimestamps = new();
    private readonly Queue<DateTimeOffset> _inputTimestamps = new();

    public RateLimitDecision Check(string messageType)
    {
        var now = timeProvider.GetUtcNow();

        if (!TryConsume(_messageTimestamps, options.MaxMessagesPerSecond, now))
        {
            return RateLimitDecision.Rejected(
                $"Too many client messages. Limit is {NormalizeLimit(options.MaxMessagesPerSecond)} per second.");
        }

        if (string.Equals(messageType, "input", StringComparison.Ordinal) &&
            !TryConsume(_inputTimestamps, options.MaxInputsPerSecond, now))
        {
            return RateLimitDecision.Rejected(
                $"Too many input messages. Limit is {NormalizeLimit(options.MaxInputsPerSecond)} per second.");
        }

        return RateLimitDecision.Allowed;
    }

    private static bool TryConsume(
        Queue<DateTimeOffset> timestamps,
        int configuredLimit,
        DateTimeOffset now)
    {
        var limit = NormalizeLimit(configuredLimit);
        while (timestamps.Count > 0 && timestamps.Peek() + Window <= now)
        {
            timestamps.Dequeue();
        }

        if (timestamps.Count >= limit)
        {
            return false;
        }

        timestamps.Enqueue(now);
        return true;
    }

    private static int NormalizeLimit(int configuredLimit) => Math.Max(1, configuredLimit);
}

internal readonly record struct RateLimitDecision(bool IsAllowed, string? Reason)
{
    public static RateLimitDecision Allowed { get; } = new(true, null);

    public static RateLimitDecision Rejected(string reason) => new(false, reason);
}
