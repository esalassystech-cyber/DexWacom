using Pizarra.Core;

namespace Pizarra.Tests;

public sealed class PointerInputCoordinatorTests
{
    [Fact]
    public void PromotedMouseContinuesWhenNoStylusStreamWasDelivered()
    {
        var input = new PointerInputCoordinator();

        Assert.True(input.Accept(PointerInputSource.PromotedPenMouse, tipDown: true));
        Assert.True(input.Accept(PointerInputSource.PromotedPenMouse, tipDown: true));
        Assert.True(input.Accept(PointerInputSource.PromotedPenMouse, tipDown: false));
        Assert.False(input.HasActiveContact);
    }

    [Fact]
    public void CompetingStylusAndWinTabStreamsProduceOnlyOneContact()
    {
        var input = new PointerInputCoordinator();

        Assert.True(input.Accept(PointerInputSource.WpfPen, tipDown: true));
        Assert.False(input.Accept(PointerInputSource.WinTab, tipDown: true));
        Assert.False(input.Accept(PointerInputSource.WinTab, tipDown: false));
        Assert.True(input.Accept(PointerInputSource.WpfPen, tipDown: false));
        Assert.True(input.Accept(PointerInputSource.WinTab, tipDown: true));
    }
}
