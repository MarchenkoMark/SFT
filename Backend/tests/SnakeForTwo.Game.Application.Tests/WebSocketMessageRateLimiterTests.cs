using SnakeForTwo.Api;

namespace SnakeForTwo.Game.Application.Tests;

public sealed class WebSocketMessageRateLimiterTests
{
    [Fact]
    public void Rejects_total_messages_after_configured_limit_within_one_second()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var limiter = new WebSocketMessageRateLimiter(
            new WebSocketRateLimitOptions
            {
                MaxMessagesPerSecond = 2,
                MaxInputsPerSecond = 10
            },
            clock);

        Assert.True(limiter.Check("ping").IsAllowed);
        Assert.True(limiter.Check("ready").IsAllowed);

        var rejected = limiter.Check("unready");

        Assert.False(rejected.IsAllowed);
        Assert.Contains("Too many client messages", rejected.Reason);

        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.True(limiter.Check("ready").IsAllowed);
    }

    [Fact]
    public void Rejects_inputs_independently_from_general_message_limit()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var limiter = new WebSocketMessageRateLimiter(
            new WebSocketRateLimitOptions
            {
                MaxMessagesPerSecond = 10,
                MaxInputsPerSecond = 1
            },
            clock);

        Assert.True(limiter.Check("input").IsAllowed);

        var rejected = limiter.Check("input");

        Assert.False(rejected.IsAllowed);
        Assert.Contains("Too many input messages", rejected.Reason);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
