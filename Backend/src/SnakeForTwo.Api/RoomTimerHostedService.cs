using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Api;

internal sealed class RoomTimerHostedService : BackgroundService
{
    private static readonly TimeSpan TimerInterval = TimeSpan.FromMilliseconds(100);

    private readonly IRoomCoordinator _roomCoordinator;
    private readonly WebSocketConnectionRegistry _connections;
    private readonly ILogger<RoomTimerHostedService> _logger;

    public RoomTimerHostedService(
        IRoomCoordinator roomCoordinator,
        WebSocketConnectionRegistry connections,
        ILogger<RoomTimerHostedService> logger)
    {
        _roomCoordinator = roomCoordinator;
        _connections = connections;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimerInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var result = _roomCoordinator.ProcessTimers();
                await _connections.DispatchAsync(result, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Room timer stopped unexpectedly.");
            throw;
        }
    }
}
