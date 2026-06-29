using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using SnakeForTwo.Contracts;

namespace SnakeForTwo.Api;

internal sealed class WebSocketClientConnection
{
    private readonly Channel<ServerMessage> _sendQueue = Channel.CreateBounded<ServerMessage>(
        new BoundedChannelOptions(capacity: 128)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public WebSocketClientConnection(string connectionId, WebSocket socket)
    {
        ConnectionId = connectionId;
        Socket = socket;
    }

    public string ConnectionId { get; }

    public WebSocket Socket { get; }

    public bool TrySend(ServerMessage message) => _sendQueue.Writer.TryWrite(message);

    public void Complete() => _sendQueue.Writer.TryComplete();

    public async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in _sendQueue.Reader.ReadAllAsync(cancellationToken))
            {
                if (Socket.State != WebSocketState.Open)
                {
                    break;
                }

                var bytes = JsonSerializer.SerializeToUtf8Bytes(
                    message,
                    message.GetType(),
                    RealtimeJson.Options);

                await Socket.SendAsync(
                    bytes,
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
    }

    public async ValueTask CloseAsync(
        WebSocketCloseStatus status,
        string reason,
        CancellationToken cancellationToken)
    {
        if (Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await Socket.CloseAsync(status, reason, cancellationToken);
        }
    }
}
