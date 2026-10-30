using Pizarra.Core;

namespace Pizarra.Tests;

public sealed class CursorConfinementControllerTests
{
    [Fact]
    public void BoardWithoutWacomDeviceLeavesCursorFree()
    {
        var clipper = new FakeCursorClipper();
        var controller = new CursorConfinementController(clipper);

        controller.ActivateBoard(new RectD(0, 0, 1000, 700), wacomDetected: false);

        Assert.False(controller.IsConfined);
        Assert.Empty(clipper.ClippedTo);
        Assert.Equal(1, clipper.ReleaseCount);
    }

    [Fact]
    public void ActiveWacomConfinesAndTracksBoardBounds()
    {
        var clipper = new FakeCursorClipper();
        var controller = new CursorConfinementController(clipper);
        var initial = new RectD(0, 0, 1440, 900);
        var resized = new RectD(0, 0, 2560, 1440);

        controller.ActivateBoard(initial, wacomDetected: true);
        controller.UpdateBoardBounds(resized);

        Assert.True(controller.IsConfined);
        Assert.Equal([initial, resized], clipper.ClippedTo);
    }

    [Theory]
    [InlineData(CursorReleaseReason.Escape)]
    [InlineData(CursorReleaseReason.Deactivated)]
    [InlineData(CursorReleaseReason.Minimized)]
    [InlineData(CursorReleaseReason.Closed)]
    [InlineData(CursorReleaseReason.TabletDisconnected)]
    public void EveryExitReasonReleasesCursor(CursorReleaseReason reason)
    {
        var clipper = new FakeCursorClipper();
        var controller = new CursorConfinementController(clipper);
        controller.ActivateBoard(new RectD(0, 0, 1000, 700), wacomDetected: true);

        controller.Release(reason);

        Assert.False(controller.IsConfined);
        Assert.Equal(1, clipper.ReleaseCount);
    }

    [Fact]
    public void FailedNativeClipKeepsCursorFree()
    {
        var clipper = new FakeCursorClipper { ShouldClip = false };
        var controller = new CursorConfinementController(clipper);

        controller.ActivateBoard(new RectD(0, 0, 1000, 700), wacomDetected: true);

        Assert.False(controller.IsConfined);
        Assert.Equal(1, clipper.ReleaseCount);
    }

    private sealed class FakeCursorClipper : ICursorClipper
    {
        public List<RectD> ClippedTo { get; } = [];
        public int ReleaseCount { get; private set; }
        public bool ShouldClip { get; init; } = true;

        public bool TryClip(RectD screenBounds)
        {
            ClippedTo.Add(screenBounds);
            return ShouldClip;
        }

        public void Release() => ReleaseCount++;
    }
}
