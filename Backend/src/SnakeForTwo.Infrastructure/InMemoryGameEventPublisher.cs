using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Infrastructure;

public sealed class InMemoryGameEventPublisher : IGameEventPublisher
{
    private readonly List<GameEvent> _events = [];

    public IReadOnlyList<GameEvent> Events => _events;

    public ValueTask PublishAsync(GameEvent gameEvent, CancellationToken cancellationToken)
    {
        _events.Add(gameEvent);
        return ValueTask.CompletedTask;
    }
}
