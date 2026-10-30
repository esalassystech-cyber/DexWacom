using Pizarra.Core;

namespace Pizarra.Tests;

public sealed class WindowSystemMappingTests
{
    [Fact]
    public void MapsTabletExtentsToAllFourCornersOfTheWindow()
    {
        var mapping = WindowSystemMapping.Create(new RectD(120, 80, 1000, 700), reverseX: false, reverseY: true);

        Assert.Equal(new PointD(120, 780), mapping.Map(0, 0));
        Assert.Equal(new PointD(1120, 780), mapping.Map(1, 0));
        Assert.Equal(new PointD(120, 80), mapping.Map(0, 1));
        Assert.Equal(new PointD(1120, 80), mapping.Map(1, 1));
    }

    [Fact]
    public void ReversedYStartsOnTheBottomEdgeOfTheArea()
    {
        // Diagnostic window on the 4K monitor at 150 %, as logged on 2026-10-30.
        var mapping = WindowSystemMapping.Create(new RectD(-1603, 687, 1286, 778), reverseX: false, reverseY: true);

        Assert.Equal(new WindowSystemMapping(-1603, 1286, 1465, -778), mapping);
    }
}
