using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Game.Application.Tests;

public sealed class ApplicationPlaceholderTests
{
    [Fact]
    public async Task Game_event_publisher_port_can_be_implemented_in_memory()
    {
        var publisher = new CapturingPublisher();
        var started = new MatchStartedEvent("match-1", "room-1", DateTimeOffset.UnixEpoch);

        await publisher.PublishAsync(started, CancellationToken.None);

        Assert.Equal(started, Assert.Single(publisher.Events));
    }

    private sealed class CapturingPublisher : IGameEventPublisher
    {
        public List<GameEvent> Events { get; } = [];

        public ValueTask PublishAsync(GameEvent gameEvent, CancellationToken cancellationToken)
        {
            Events.Add(gameEvent);
            return ValueTask.CompletedTask;
        }
    }
}
