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
    private readonly Counter<long> _renderDiagnosticsBatches;
    private readonly Histogram<long> _renderDiagnosticsBatchFrames;
    private readonly Histogram<long> _renderDiagnosticsRenderTickDelta;
    private readonly Histogram<double> _renderDiagnosticsFrameServerLeadMs;
    private readonly Histogram<double> _renderDiagnosticsReceivedFrameLeadMs;
    private readonly Histogram<double> _renderDiagnosticsEstimatedServerOffsetMs;
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
        _renderDiagnosticsBatches = _meter.CreateCounter<long>(
            "snakefortwo.render_diagnostics.batches",
            unit: "{batch}",
            description: "Client render diagnostics batches received.");
        _renderDiagnosticsBatchFrames = _meter.CreateHistogram<long>(
            "snakefortwo.render_diagnostics.batch_frames",
            unit: "{frame}",
            description: "Frames included in client render diagnostics batches.");
        _renderDiagnosticsRenderTickDelta = _meter.CreateHistogram<long>(
            "snakefortwo.render_diagnostics.render_tick_delta",
            unit: "{tick}",
            description: "Client render tick minus latest authoritative frame tick.");
        _renderDiagnosticsFrameServerLeadMs = _meter.CreateHistogram<double>(
            "snakefortwo.render_diagnostics.frame_server_lead",
            unit: "ms",
            description: "Latest frame server time minus client estimated server time.");
        _renderDiagnosticsReceivedFrameLeadMs = _meter.CreateHistogram<double>(
            "snakefortwo.render_diagnostics.received_frame_lead",
            unit: "ms",
            description: "Latest frame server time minus client frame receipt time.");
        _renderDiagnosticsEstimatedServerOffsetMs = _meter.CreateHistogram<double>(
            "snakefortwo.render_diagnostics.estimated_server_offset",
            unit: "ms",
            description: "Client estimated server time minus client wall-clock capture time.");
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

    public void RecordRenderDiagnosticsBatch(int frameCount, string reason)
    {
        var tags = new KeyValuePair<string, object?>("reason", reason);

        _renderDiagnosticsBatches.Add(1, tags);
        _renderDiagnosticsBatchFrames.Record(Math.Max(0, frameCount), tags);
    }

    public void RecordRenderDiagnosticsFrame(
        long renderTickDelta,
        double frameServerLeadMs,
        double receivedFrameLeadMs,
        double estimatedServerOffsetMs)
    {
        _renderDiagnosticsRenderTickDelta.Record(renderTickDelta);
        _renderDiagnosticsFrameServerLeadMs.Record(frameServerLeadMs);
        _renderDiagnosticsReceivedFrameLeadMs.Record(receivedFrameLeadMs);
        _renderDiagnosticsEstimatedServerOffsetMs.Record(estimatedServerOffsetMs);
    }

    public void Dispose() => _meter.Dispose();
}
