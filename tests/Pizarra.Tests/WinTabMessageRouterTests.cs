using Pizarra.Core;

namespace Pizarra.Tests;

public sealed class WinTabMessageRouterTests
{
    [Fact]
    public void RoutesPacketOnlyForItsOwningContextAndIgnoresStaleMessages()
    {
        var router = new WinTabMessageRouter(0x7FF0, new nint(123));

        Assert.Equal(WinTabMessageKind.Packet, router.Route(0x7FF0, new nint(8), new nint(123)));
        Assert.Equal(WinTabMessageKind.Ignore, router.Route(0x7FF0, new nint(9), new nint(456)));
        Assert.Equal(WinTabMessageKind.Ignore, router.Route(0x7FF1, new nint(9), new nint(123)));
        Assert.Equal(WinTabMessageKind.Proximity, router.Route(0x7FF5, nint.Zero, nint.Zero));
        Assert.Equal(WinTabMessageKind.InfoChange, router.Route(0x7FF6, nint.Zero, nint.Zero));
    }

    [Fact]
    public void UsesTheMessageRangeReturnedByTheDriver()
    {
        var router = new WinTabMessageRouter(0x9000, new nint(5));

        Assert.Equal(WinTabMessageKind.Packet, router.Route(0x9000, new nint(1), new nint(5)));
        Assert.Equal(WinTabMessageKind.ContextClose, router.Route(0x9002, new nint(5), nint.Zero));
        Assert.Equal(WinTabMessageKind.Proximity, router.Route(0x9005, nint.Zero, nint.Zero));
        Assert.Equal(WinTabMessageKind.InfoChange, router.Route(0x9006, nint.Zero, nint.Zero));
    }

    [Fact]
    public void MapsPacketAndSystemCoordinatesIndependentlyToWindowBounds()
    {
        var mapping = WinTabInputMapping.Create(new RectD(100, 50, 800, 600),
            reverseCursorX: false, reverseCursorY: true,
            reversePacketX: true, reversePacketY: false);

        Assert.Equal(new WindowSystemMapping(100, 800, 650, -600), mapping.Cursor);
        Assert.Equal(new WindowSystemMapping(900, -800, 50, 600), mapping.Packets);
    }
}
