using Pizarra.Core;

namespace Pizarra.Tests;

public sealed class CanvasTransformTests
{
    [Fact]
    public void ConvertsScreenPixelsIntoWorldCoordinatesAndBack()
    {
        var viewport = new RectD(100, 50, 800, 600);
        var transform = new CanvasTransform(2, 20, 30);
        var screen = new PointD(440, 240);

        var world = transform.ScreenToWorld(screen, viewport);
        var roundTrip = transform.WorldToScreen(world, viewport);

        Assert.Equal(160, world.X, 8);
        Assert.Equal(80, world.Y, 8);
        Assert.Equal(screen.X, roundTrip.X, 8);
        Assert.Equal(screen.Y, roundTrip.Y, 8);
    }

    [Fact]
    public void RejectsNonPositiveZoom()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CanvasTransform(0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CanvasTransform(-1, 0, 0));
    }

    [Fact]
    public void MapsPhysicalStylusPixelsThroughDpiIntoCanvasWorldCoordinates()
    {
        var point = new PenScreenPoint(new PointD(1200, 900), new PointD(200, 100), 2, 2);
        var transform = new CanvasTransform(2, 20, 30);
        var viewport = new RectD(100, 50, 800, 600);

        var world = PenCoordinateMapper.ToCanvasWorld(point, viewport, transform);

        Assert.Equal(190, world.X, 8);
        Assert.Equal(160, world.Y, 8);
    }
}
