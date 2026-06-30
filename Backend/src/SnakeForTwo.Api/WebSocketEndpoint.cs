using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SnakeForTwo.Contracts;
using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Api;

internal sealed class WebSocketEndpoint
{
    private const int MaxMessageBytes = 16 * 1024;
    private const int MaxMalformedMessages = 3;

    private readonly IRoomCoordinator _roomCoordinator;
    private readonly WebSocketConnectionRegistry _connections;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WebSocketEndpoint> _logger;

    public WebSocketEndpoint(
        IRoomCoordinator roomCoordinator,
        WebSocketConnectionRegistry connections,
        TimeProvider timeProvider,
        ILogger<WebSocketEndpoint> logger)
    {
        _roomCoordinator = roomCoordinator;
        _connections = connections;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var connection = _connections.Register(socket);
        var sendLoop = connection.SendLoopAsync(context.RequestAborted);

        try
        {
            await ReceiveLoopAsync(connection, context.RequestAborted);
        }
        finally
        {
            var disconnectResult = _roomCoordinator.MarkDisconnected(connection.ConnectionId);
            await _connections.DispatchAsync(disconnectResult, CancellationToken.None);

            if (socket.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing.",
                        CancellationToken.None);
                }
                catch (WebSocketException)
                {
                }
            }

            _connections.Remove(connection.ConnectionId);
            connection.Complete();

            try
            {
                await sendLoop;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task ReceiveLoopAsync(
        WebSocketClientConnection connection,
        CancellationToken cancellationToken)
    {
        var malformedMessages = 0;

        while (connection.Socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            string? message;
            try
            {
                message = await ReceiveTextMessageAsync(connection.Socket, cancellationToken);
            }
            catch (ClientProtocolException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Closing WebSocket connection {ConnectionId} after protocol violation.",
                    connection.ConnectionId);

                await _connections.SendAsync(
                    connection.ConnectionId,
                    new ErrorMessage("invalidPayload", exception.Message));
                await _connections.CloseAsync(
                    connection.ConnectionId,
                    WebSocketCloseStatus.InvalidPayloadData,
                    exception.Message,
                    CancellationToken.None);
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (WebSocketException)
            {
                return;
            }

            if (message is null)
            {
                return;
            }

            var outcome = await HandleClientMessageAsync(connection.ConnectionId, message, CancellationToken.None);
            if (outcome == ClientMessageOutcome.Malformed)
            {
                malformedMessages++;
                if (malformedMessages >= MaxMalformedMessages)
                {
                    await _connections.CloseAsync(
                        connection.ConnectionId,
                        WebSocketCloseStatus.InvalidPayloadData,
                        "Too many malformed messages.",
                        CancellationToken.None);
                    return;
                }
            }
            else
            {
                malformedMessages = 0;
            }
        }
    }

    private async ValueTask<ClientMessageOutcome> HandleClientMessageAsync(
        string connectionId,
        string message,
        CancellationToken cancellationToken)
    {
        JsonDocument document;
        try
        {
            document = await ParseJsonAsync(connectionId, message);
        }
        catch (MalformedClientMessageException)
        {
            return ClientMessageOutcome.Malformed;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                await _connections.SendAsync(
                    connectionId,
                    new ErrorMessage("malformedMessage", "Client message must be a JSON object."));
                return ClientMessageOutcome.Malformed;
            }

            if (!TryGetString(document.RootElement, "type", out var type))
            {
                await _connections.SendAsync(
                    connectionId,
                    new ErrorMessage("malformedMessage", "Client message is missing a string type."));
                return ClientMessageOutcome.Malformed;
            }

            RoomCommandResult result;
            switch (type)
            {
                case "createRoom":
                    result = _roomCoordinator.CreateRoom(connectionId);
                    break;

                case "joinRoom":
                    if (!TryGetString(document.RootElement, "roomId", out var joinRoomId))
                    {
                        return await SendMalformedAsync(connectionId, "joinRoom requires roomId.");
                    }

                    result = _roomCoordinator.JoinRoom(connectionId, joinRoomId);
                    break;

                case "resumeRoom":
                    if (!TryGetString(document.RootElement, "roomId", out var resumeRoomId) ||
                        !TryGetString(document.RootElement, "playerSessionToken", out var playerSessionToken))
                    {
                        return await SendMalformedAsync(
                            connectionId,
                            "resumeRoom requires roomId and playerSessionToken.");
                    }

                    result = _roomCoordinator.ResumeRoom(connectionId, resumeRoomId, playerSessionToken);
                    break;

                case "ready":
                    if (!TryGetString(document.RootElement, "roomId", out var readyRoomId))
                    {
                        return await SendMalformedAsync(connectionId, "ready requires roomId.");
                    }

                    result = _roomCoordinator.SetReady(connectionId, readyRoomId, isReady: true);
                    break;

                case "unready":
                    if (!TryGetString(document.RootElement, "roomId", out var unreadyRoomId))
                    {
                        return await SendMalformedAsync(connectionId, "unready requires roomId.");
                    }

                    result = _roomCoordinator.SetReady(connectionId, unreadyRoomId, isReady: false);
                    break;

                case "input":
                    if (!TryGetString(document.RootElement, "roomId", out var inputRoomId) ||
                        !TryGetDirection(document.RootElement, out var direction) ||
                        !TryGetInt64(document.RootElement, "clientTime", out var clientTime))
                    {
                        return await SendMalformedAsync(
                            connectionId,
                            "input requires roomId, direction, and clientTime.");
                    }

                    var clientSequence = TryGetInt32(
                        document.RootElement,
                        "clientSequence",
                        out var parsedClientSequence)
                        ? parsedClientSequence
                        : (int?)null;
                    result = _roomCoordinator.SubmitInput(
                        connectionId,
                        new ClientInputMessage(inputRoomId, direction, clientTime, clientSequence),
                        _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
                    break;

                case "leaveRoom":
                    if (!TryGetString(document.RootElement, "roomId", out var leaveRoomId))
                    {
                        return await SendMalformedAsync(connectionId, "leaveRoom requires roomId.");
                    }

                    result = _roomCoordinator.LeaveRoom(connectionId, leaveRoomId);
                    break;

                case "ping":
                    if (!TryGetInt64(document.RootElement, "clientTime", out var pingClientTime) ||
                        !TryGetString(document.RootElement, "sampleId", out var sampleId))
                    {
                        return await SendMalformedAsync(connectionId, "ping requires clientTime and sampleId.");
                    }

                    await _connections.SendAsync(
                        connectionId,
                        new PongMessage(
                            pingClientTime,
                            _timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
                            sampleId));
                    return ClientMessageOutcome.Handled;

                default:
                    await _connections.SendAsync(
                        connectionId,
                        new ErrorMessage("unknownMessageType", $"Unknown client message type '{type}'."));
                    return ClientMessageOutcome.Handled;
            }

            await _connections.DispatchAsync(result, cancellationToken);
            return ClientMessageOutcome.Handled;
        }
    }

    private async ValueTask<JsonDocument> ParseJsonAsync(string connectionId, string message)
    {
        try
        {
            return JsonDocument.Parse(message);
        }
        catch (JsonException)
        {
            await _connections.SendAsync(
                connectionId,
                new ErrorMessage("malformedJson", "Client message is not valid JSON."));
            throw new MalformedClientMessageException();
        }
    }

    private static async ValueTask<string?> ReceiveTextMessageAsync(
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4096);

        try
        {
            using var stream = new MemoryStream();

            while (true)
            {
                var result = await socket.ReceiveAsync(
                    buffer.AsMemory(0, buffer.Length),
                    cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    throw new ClientProtocolException("Only text JSON WebSocket messages are supported.");
                }

                if (stream.Length + result.Count > MaxMessageBytes)
                {
                    throw new ClientProtocolException("Client message exceeded the maximum size.");
                }

                stream.Write(buffer, 0, result.Count);

                if (result.EndOfMessage)
                {
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = "";
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? "";
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetInt64(JsonElement root, string propertyName, out long value)
    {
        value = default;
        return root.TryGetProperty(propertyName, out var property) &&
            property.TryGetInt64(out value);
    }

    private static bool TryGetInt32(JsonElement root, string propertyName, out int value)
    {
        value = default;
        return root.TryGetProperty(propertyName, out var property) &&
            property.TryGetInt32(out value);
    }

    private static bool TryGetDirection(JsonElement root, out DirectionDto direction)
    {
        direction = default;
        if (!TryGetString(root, "direction", out var value))
        {
            return false;
        }

        return Enum.TryParse(value, ignoreCase: true, out direction);
    }

    private async ValueTask<ClientMessageOutcome> SendMalformedAsync(
        string connectionId,
        string message)
    {
        await _connections.SendAsync(
            connectionId,
            new ErrorMessage("malformedMessage", message));
        return ClientMessageOutcome.Malformed;
    }

    private enum ClientMessageOutcome
    {
        Handled,
        Malformed
    }

    private sealed class ClientProtocolException : Exception
    {
        public ClientProtocolException(string message) : base(message)
        {
        }
    }

    private sealed class MalformedClientMessageException : Exception
    {
    }
}
