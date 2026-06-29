namespace SnakeForTwo.Game.Domain;

public enum InputSchedulingStatus
{
    Scheduled,
    Duplicate,
    RejectedReversal,
    RejectedQueueFull,
    RejectedStale,
    RejectedFuture,
    RejectedGameFinished,
    UnknownPlayer
}

public sealed record InputScheduleResult(
    InputSchedulingStatus Status,
    GameTick? ScheduledTick,
    bool Changed)
{
    public static InputScheduleResult ScheduledAt(GameTick tick) =>
        new(InputSchedulingStatus.Scheduled, tick, Changed: true);

    public static InputScheduleResult Duplicate(GameTick tick) =>
        new(InputSchedulingStatus.Duplicate, tick, Changed: false);

    public static InputScheduleResult Rejected(InputSchedulingStatus status) =>
        new(status, ScheduledTick: null, Changed: false);
}

public sealed class InputTimeline
{
    private readonly Direction _initialDirection;
    private readonly int _queueWindowTicks;
    private readonly SortedDictionary<long, Direction> _scheduled = [];

    public InputTimeline(Direction initialDirection, int queueWindowTicks)
    {
        if (queueWindowTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(queueWindowTicks), "Queue window must be positive.");
        }

        _initialDirection = initialDirection;
        _queueWindowTicks = queueWindowTicks;
    }

    public IReadOnlyDictionary<long, Direction> ScheduledInputs => _scheduled;

    public InputScheduleResult Schedule(GameTick targetTick, Direction direction)
    {
        var sawEmptyTick = false;
        var sawIllegalReversal = false;

        for (var offset = 0; offset < _queueWindowTicks; offset++)
        {
            var candidateTick = targetTick.Value + offset;
            if (_scheduled.ContainsKey(candidateTick))
            {
                continue;
            }

            sawEmptyTick = true;
            var previousDirection = DirectionBefore(candidateTick);
            if (direction == previousDirection)
            {
                return InputScheduleResult.Duplicate(new GameTick(candidateTick));
            }

            if (direction.IsOpposite(previousDirection) || WouldMakeNextInputIllegal(candidateTick, direction))
            {
                sawIllegalReversal = true;
                continue;
            }

            _scheduled.Add(candidateTick, direction);
            return InputScheduleResult.ScheduledAt(new GameTick(candidateTick));
        }

        return InputScheduleResult.Rejected(
            sawEmptyTick && sawIllegalReversal
                ? InputSchedulingStatus.RejectedReversal
                : InputSchedulingStatus.RejectedQueueFull);
    }

    public Direction DirectionAt(GameTick tick)
    {
        var direction = _initialDirection;
        foreach (var scheduled in _scheduled)
        {
            if (scheduled.Key > tick.Value)
            {
                break;
            }

            direction = scheduled.Value;
        }

        return direction;
    }

    private Direction DirectionBefore(long tick)
    {
        var direction = _initialDirection;
        foreach (var scheduled in _scheduled)
        {
            if (scheduled.Key >= tick)
            {
                break;
            }

            direction = scheduled.Value;
        }

        return direction;
    }

    private bool WouldMakeNextInputIllegal(long tick, Direction direction)
    {
        foreach (var scheduled in _scheduled)
        {
            if (scheduled.Key <= tick)
            {
                continue;
            }

            return scheduled.Value.IsOpposite(direction);
        }

        return false;
    }
}
