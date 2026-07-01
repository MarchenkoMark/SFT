namespace SnakeForTwo.Game.Domain;

public sealed record GameFrame(
    GameTick Tick,
    GameState State,
    string StateHash,
    IReadOnlyList<FoodEatenEvent> FoodEaten);

public sealed record GameInputResult(
    InputSchedulingStatus Status,
    GameTick? ScheduledTick,
    bool RolledBack,
    GameTick? RollbackFromTick,
    IReadOnlyList<GameFrame> Corrections);

public sealed class RollbackGameSession
{
    private readonly Dictionary<PlayerId, InputTimeline> _inputTimelines;
    private readonly Dictionary<long, GameSnapshot> _snapshots = [];
    private readonly Dictionary<long, string> _stateHashes = [];
    private readonly Dictionary<long, IReadOnlyList<FoodEatenEvent>> _foodEatenEventsByTick = [];

    public RollbackGameSession(GameState initialState)
    {
        ArgumentNullException.ThrowIfNull(initialState);

        Rules = initialState.Rules;
        CurrentState = initialState.Copy();
        _inputTimelines = CurrentState.Snakes.ToDictionary(
            snake => snake.PlayerId,
            snake => new InputTimeline(snake.Direction, Rules.MaxQueuedInputsPerPlayer));

        StoreSnapshot(CurrentState);
    }

    public GameRules Rules { get; }

    public GameState CurrentState { get; private set; }

    public GameTick CurrentTick => CurrentState.Tick;

    public IReadOnlyList<FoodEatenEvent> FoodEatenEvents =>
        _foodEatenEventsByTick
            .OrderBy(pair => pair.Key)
            .SelectMany(pair => pair.Value)
            .ToArray();

    public IReadOnlyDictionary<PlayerId, Direction> OutgoingDirectionsAt(GameTick tick) =>
        _inputTimelines.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.DirectionAt(tick));

    public GameFrame AdvanceOneTick() => AdvanceOneTick(trimHistory: true);

    public GameInputResult SubmitInput(PlayerId playerId, GameTick targetTick, Direction direction)
    {
        if (!_inputTimelines.TryGetValue(playerId, out var timeline))
        {
            return Rejected(InputSchedulingStatus.UnknownPlayer);
        }

        if (CurrentState.Status != GameStatus.Running)
        {
            return Rejected(InputSchedulingStatus.RejectedGameFinished);
        }

        var minimumTick = CurrentTick.Value - Rules.RollbackWindowTicks;
        if (targetTick.Value < minimumTick)
        {
            return Rejected(InputSchedulingStatus.RejectedStale);
        }

        if (targetTick.Value > CurrentTick.Value + Rules.InputFutureBufferTicks)
        {
            return Rejected(InputSchedulingStatus.RejectedFuture);
        }

        var schedule = timeline.Schedule(targetTick, direction);
        if (!schedule.Changed || schedule.ScheduledTick is null)
        {
            return new GameInputResult(
                schedule.Status,
                schedule.ScheduledTick,
                RolledBack: false,
                RollbackFromTick: null,
                Corrections: Array.Empty<GameFrame>());
        }

        if (schedule.ScheduledTick.Value.Value < CurrentTick.Value)
        {
            return RollBackAndResimulate(schedule);
        }

        return new GameInputResult(
            schedule.Status,
            schedule.ScheduledTick,
            RolledBack: false,
            RollbackFromTick: null,
            Corrections: Array.Empty<GameFrame>());
    }

    private GameFrame AdvanceOneTick(bool trimHistory)
    {
        IReadOnlyList<FoodEatenEvent> foodEaten = Array.Empty<FoodEatenEvent>();

        if (CurrentState.Status == GameStatus.Running)
        {
            var inputs = BuildInputs(CurrentState.Tick);
            var advance = SnakeGameEngine.AdvanceDetailed(CurrentState, inputs, Rules);
            CurrentState = advance.State;
            foodEaten = advance.FoodEaten;
            _foodEatenEventsByTick[CurrentState.Tick.Value] = advance.FoodEaten;
            StoreSnapshot(CurrentState);

            if (trimHistory)
            {
                TrimHistory();
            }
        }

        return CreateFrame(CurrentState, foodEaten);
    }

    private GameInputResult RollBackAndResimulate(InputScheduleResult schedule)
    {
        var rollbackTick = schedule.ScheduledTick!.Value;
        if (!_snapshots.TryGetValue(rollbackTick.Value, out var snapshot))
        {
            return Rejected(InputSchedulingStatus.RejectedStale);
        }

        var finalTick = CurrentTick;
        var previousHashes = _stateHashes
            .Where(pair => pair.Key > rollbackTick.Value && pair.Key <= finalTick.Value)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        CurrentState = snapshot.Restore();
        RemoveHistoryAfter(rollbackTick);

        var corrections = new List<GameFrame>();
        while (CurrentTick.Value < finalTick.Value && CurrentState.Status == GameStatus.Running)
        {
            var frame = AdvanceOneTick(trimHistory: false);
            if (previousHashes.TryGetValue(frame.Tick.Value, out var previousHash) &&
                !string.Equals(previousHash, frame.StateHash, StringComparison.Ordinal))
            {
                corrections.Add(frame);
            }
        }

        TrimHistory();

        return new GameInputResult(
            schedule.Status,
            schedule.ScheduledTick,
            RolledBack: true,
            RollbackFromTick: rollbackTick,
            Corrections: corrections);
    }

    private TickInputs BuildInputs(GameTick tick) =>
        new(CurrentState.Snakes.Select(snake =>
            KeyValuePair.Create(snake.PlayerId, _inputTimelines[snake.PlayerId].DirectionAt(tick))));

    private void StoreSnapshot(GameState state)
    {
        _snapshots[state.Tick.Value] = GameSnapshot.Capture(state);
        _stateHashes[state.Tick.Value] = SnakeGameEngine.ComputeStateHash(state);
    }

    private void RemoveHistoryAfter(GameTick tick)
    {
        foreach (var key in _snapshots.Keys.Where(key => key > tick.Value).ToArray())
        {
            _snapshots.Remove(key);
        }

        foreach (var key in _stateHashes.Keys.Where(key => key > tick.Value).ToArray())
        {
            _stateHashes.Remove(key);
        }

        foreach (var key in _foodEatenEventsByTick.Keys.Where(key => key > tick.Value).ToArray())
        {
            _foodEatenEventsByTick.Remove(key);
        }
    }

    private void TrimHistory()
    {
        var minimumTick = CurrentTick.Value - Rules.RollbackWindowTicks;
        foreach (var key in _snapshots.Keys.Where(key => key < minimumTick).ToArray())
        {
            _snapshots.Remove(key);
        }

        foreach (var key in _stateHashes.Keys.Where(key => key < minimumTick).ToArray())
        {
            _stateHashes.Remove(key);
        }
    }

    private static GameFrame CreateFrame(
        GameState state,
        IReadOnlyList<FoodEatenEvent>? foodEaten = null) =>
        new(
            state.Tick,
            state.Copy(),
            SnakeGameEngine.ComputeStateHash(state),
            foodEaten ?? Array.Empty<FoodEatenEvent>());

    private static GameInputResult Rejected(InputSchedulingStatus status) =>
        new(
            status,
            ScheduledTick: null,
            RolledBack: false,
            RollbackFromTick: null,
            Corrections: Array.Empty<GameFrame>());
}
