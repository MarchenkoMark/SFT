using System.Threading.Channels;
using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Api;

internal sealed class BackgroundGameEventPublisher : IGameEventPublisher
{
    private readonly Channel<GameEvent> _events = Channel.CreateUnbounded<GameEvent>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelReader<GameEvent> Reader => _events.Reader;

    public ValueTask PublishAsync(GameEvent gameEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);

        if (!_events.Writer.TryWrite(gameEvent))
        {
            throw new InvalidOperationException("Unable to enqueue a game event for persistence.");
        }

        return ValueTask.CompletedTask;
    }
}
