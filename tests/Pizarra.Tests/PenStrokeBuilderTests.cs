using Pizarra.Core;

namespace Pizarra.Tests;

public sealed class PenStrokeBuilderTests
{
    [Fact]
    public void LiftEndsStrokeAndNextTouchStartsIndependentStroke()
    {
        var builder = new PenStrokeBuilder();
        builder.Begin(new InkPoint(10, 20, .25));
        builder.Append(new InkPoint(11, 21, .5));
        var first = builder.End();

        builder.Begin(new InkPoint(30, 40, .6));
        builder.Append(new InkPoint(31, 42, .9));
        var second = builder.End();

        Assert.Equal([new InkPoint(10, 20, .25), new InkPoint(11, 21, .5)], first.Points);
        Assert.Equal([new InkPoint(30, 40, .6), new InkPoint(31, 42, .9)], second.Points);
        Assert.False(builder.IsActive);
    }

    [Fact]
    public void SideButtonCreatesOnlyOnceUntilReleased()
    {
        var tracker = new StylusButtonPressTracker();
        var button = Guid.NewGuid();

        Assert.True(tracker.TryPress(button));
        Assert.False(tracker.TryPress(button));
        tracker.Release(button);
        Assert.True(tracker.TryPress(button));
    }
}
