using System.Collections.Concurrent;
using System.Net.WebSockets;
using SnakeForTwo.Contracts;
using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Api;

internal sealed class WebSocketConnectionRegistry
{
    private readonly ConcurrentDictionary<string, WebSocketClientConnection> _connections = new(StringComparer.Ordinal);

    public WebSocketClientConnection Register(WebSocket socket)
    {
        var connection = new WebSocketClientConnection(Guid.NewGuid().ToString("N"), socket);
        _connections[connection.ConnectionId] = connection;
        return connection;
    }

    public void Remove(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var connection))
        {
            connection.Complete();
        }
    }

    public ValueTask SendAsync(string connectionId, ServerMessage message)
    {
        if (_connections.TryGetValue(connectionId, out var connection))
        {
            connection.TrySend(message);
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask DispatchAsync(RoomCommandResult result, CancellationToken cancellationToken)
    {
        foreach (var outbound in result.Messages)
        {
            foreach (var connectionId in outbound.ConnectionIds)
            {
                await SendAsync(connectionId, outbound.Message);
            }
        }

        foreach (var closure in result.ConnectionsToClose)
        {
            await CloseAsync(
                closure.ConnectionId,
                WebSocketCloseStatus.PolicyViolation,
                closure.Reason,
                cancellationToken);
        }
    }

    public async ValueTask CloseAsync(
        string connectionId,
        WebSocketCloseStatus status,
        string reason,
        CancellationToken cancellationToken)
    {
        if (_connections.TryRemove(connectionId, out var connection))
        {
            connection.Complete();
            try
            {
                await connection.CloseAsync(status, reason, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (WebSocketException)
            {
            }
        }
    }
}
