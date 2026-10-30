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
}
