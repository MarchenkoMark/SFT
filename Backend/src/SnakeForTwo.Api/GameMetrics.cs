using System.Diagnostics.Metrics;
using System.Threading;
using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Api;

internal sealed class GameMetrics : IGameMetrics, IDisposable
{
    public const string MeterName = "SnakeForTwo.Game";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _disconnects;
    private readonly Histogram<double> _inputLatencyMs;
    private readonly Counter<long> _staleInputs;
    private readonly Histogram<long> _rollbackDepthTicks;
    private readonly Counter<long> _corrections;
    private readonly Histogram<double> _tickDurationMs;
    private readonly Counter<long> _tickOverruns;
    private readonly Counter<long> _malformedMessages;
    private readonly Counter<long> _rateLimitedMessages;
    private int _roomCount;
    private int _activeMatchCount;

    public GameMetrics()
    {
        _meter.CreateObservableGauge(
            "snakefortwo.room.count",
            () => Volatile.Read(ref _roomCount),
            unit: "{room}",
            description: "Current reserved room count.");
        _meter.CreateObservableGauge(
            "snakefortwo.match.active",
            () => Volatile.Read(ref _activeMatchCount),
            unit: "{match}",
            description: "Current in-game match count.");
        _disconnects = _meter.CreateCounter<long>(
            "snakefortwo.disconnects",
            unit: "{disconnect}",
            description: "Player WebSocket disconnect count.");
        _inputLatencyMs = _meter.CreateHistogram<double>(
            "snakefortwo.input.latency",
            unit: "ms",
            description: "Estimated client input latency.");
        _staleInputs = _meter.CreateCounter<long>(
            "snakefortwo.input.stale",
            unit: "{input}",
            description: "Inputs rejected for being outside the rollback window.");
        _rollbackDepthTicks = _meter.CreateHistogram<long>(
            "snakefortwo.rollback.depth",
            unit: "{tick}",
            description: "Rollback depth for accepted late inputs.");
        _corrections = _meter.CreateCounter<long>(
            "snakefortwo.corrections",
            unit: "{correction}",
            description: "Authoritative correction frame count.");
        _tickDurationMs = _meter.CreateHistogram<double>(
            "snakefortwo.tick.duration",
            unit: "ms",
            description: "Server authoritative tick simulation duration.");
        _tickOverruns = _meter.CreateCounter<long>(
            "snakefortwo.tick.overruns",
            unit: "{tick}",
            description: "Server ticks whose simulation duration exceeded the configured tick duration.");
        _malformedMessages = _meter.CreateCounter<long>(
            "snakefortwo.websocket.malformed_messages",
            unit: "{message}",
            description: "Malformed WebSocket client message count.");
        _rateLimitedMessages = _meter.CreateCounter<long>(
            "snakefortwo.websocket.rate_limited_messages",
            unit: "{message}",
            description: "WebSocket client messages rejected by rate limits.");
    }

    public void ObserveRoomInventory(int roomCount, int activeMatchCount)
    {
        Volatile.Write(ref _roomCount, roomCount);
        Volatile.Write(ref _activeMatchCount, activeMatchCount);
    }

    public void RecordDisconnect() => _disconnects.Add(1);

    public void RecordInputLatency(long latencyMs) => _inputLatencyMs.Record(Math.Max(0, latencyMs));

    public void RecordStaleInput() => _staleInputs.Add(1);

    public void RecordRollbackDepth(long rollbackDepthTicks) =>
        _rollbackDepthTicks.Record(Math.Max(0, rollbackDepthTicks));

    public void RecordCorrections(int correctionCount)
    {
        if (correctionCount > 0)
        {
            _corrections.Add(correctionCount);
        }
    }

    public void RecordTickDuration(double durationMs, bool overran)
    {
        _tickDurationMs.Record(Math.Max(0, durationMs));
        if (overran)
        {
            _tickOverruns.Add(1);
        }
    }

    public void RecordMalformedMessage() => _malformedMessages.Add(1);

    public void RecordRateLimitedMessage() => _rateLimitedMessages.Add(1);

    public void Dispose() => _meter.Dispose();
}
