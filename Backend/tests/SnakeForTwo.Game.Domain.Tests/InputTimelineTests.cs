using SnakeForTwo.Game.Domain;

namespace SnakeForTwo.Game.Domain.Tests;

public sealed class InputTimelineTests
{
    [Fact]
    public void Direct_180_degree_input_is_rejected()
    {
        var timeline = new InputTimeline(Direction.Up, queueWindowTicks: 4);

        var result = timeline.Schedule(new GameTick(42), Direction.Down);

        Assert.Equal(InputSchedulingStatus.RejectedReversal, result.Status);
        Assert.False(result.Changed);
        Assert.Empty(timeline.ScheduledInputs);
    }

    [Fact]
    public void Same_tick_fast_turns_are_carried_to_following_ticks()
    {
        var timeline = new InputTimeline(Direction.Up, queueWindowTicks: 4);

        var left = timeline.Schedule(new GameTick(42), Direction.Left);
        var down = timeline.Schedule(new GameTick(42), Direction.Down);

        Assert.Equal(InputSchedulingStatus.Scheduled, left.Status);
        Assert.Equal(new GameTick(42), left.ScheduledTick);
        Assert.Equal(InputSchedulingStatus.Scheduled, down.Status);
        Assert.Equal(new GameTick(43), down.ScheduledTick);
        Assert.Equal(Direction.Left, timeline.DirectionAt(new GameTick(42)));
        Assert.Equal(Direction.Down, timeline.DirectionAt(new GameTick(43)));
    }

    [Fact]
    public void Duplicate_consecutive_direction_does_not_change_the_timeline()
    {
        var timeline = new InputTimeline(Direction.Right, queueWindowTicks: 4);

        var result = timeline.Schedule(new GameTick(3), Direction.Right);

        Assert.Equal(InputSchedulingStatus.Duplicate, result.Status);
        Assert.False(result.Changed);
        Assert.Empty(timeline.ScheduledInputs);
    }

    [Fact]
    public void Inserting_an_earlier_input_cannot_make_the_next_input_a_direct_reversal()
    {
        var timeline = new InputTimeline(Direction.Up, queueWindowTicks: 4);
        timeline.Schedule(new GameTick(10), Direction.Left);

        var result = timeline.Schedule(new GameTick(9), Direction.Right);

        Assert.Equal(InputSchedulingStatus.RejectedReversal, result.Status);
        Assert.Single(timeline.ScheduledInputs);
        Assert.Equal(Direction.Left, timeline.DirectionAt(new GameTick(10)));
    }
}
