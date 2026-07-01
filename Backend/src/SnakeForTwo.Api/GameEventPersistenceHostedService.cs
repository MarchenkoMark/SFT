using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Api;

internal sealed class GameEventPersistenceHostedService(
    BackgroundGameEventPublisher publisher,
    IServiceScopeFactory scopeFactory,
    ILogger<GameEventPersistenceHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var gameEvent in publisher.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await PersistAsync(gameEvent, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to persist game event {EventType} for match {MatchId}.",
                    gameEvent.GetType().Name,
                    gameEvent.MatchId);
            }
        }
    }

    private async Task PersistAsync(GameEvent gameEvent, CancellationToken cancellationToken)
    {
        if (gameEvent is not MatchFinishedEvent finished)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var writer = scope.ServiceProvider.GetRequiredService<IMatchSummaryWriter>();
        await writer.SaveAsync(finished.Summary, cancellationToken);
    }
}
