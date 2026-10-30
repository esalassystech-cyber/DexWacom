using System.Windows;
using Pizarra.App;
using Pizarra.Core;

namespace Pizarra.App.Tests;

public sealed class BoardSurfaceInputTests
{
    [Fact]
    public void PenDrawsOnLoadedBoardInNormalWindowAndAfterLeavingFullscreen()
    {
        OnSta(() =>
        {
            var surface = CreateSurface();
            surface.SetBoardViewActive(false);
            surface.HandlePenSample(new Point(30, 40), .2, tipDown: true, buttons: 0);
            surface.HandlePenSample(new Point(70, 80), .9, tipDown: true, buttons: 0);
            surface.HandlePenSample(new Point(90, 100), 0, tipDown: false, buttons: 0);

            var stroke = Assert.Single(surface.Document.FreehandStrokes);
            Assert.Equal(3, stroke.Points.Count);
            Assert.Equal(90, stroke.Points[^1].X);
            Assert.Equal(100, stroke.Points[^1].Y);
        });
    }

    [Fact]
    public void WpfPenRemainsAvailableAfterWinTabContextIsOpened()
    {
        OnSta(() =>
        {
            var surface = CreateSurface();
            surface.SetMappedInputActive(true);

            Assert.True(surface.HandlePointerSample(new Point(20, 25), .4, true, 0, PointerInputSource.WpfPen));
            Assert.True(surface.HandlePointerSample(new Point(65, 70), .8, true, 0, PointerInputSource.WpfPen));
            Assert.True(surface.HandlePointerSample(new Point(65, 70), 0, false, 0, PointerInputSource.WpfPen));

            var stroke = Assert.Single(surface.Document.FreehandStrokes);
            Assert.Equal(3, stroke.Points.Count);
            Assert.Empty(surface.Document.Elements);
        });
    }

    [Fact]
    public void PromotedPenMouseCanDrawWhenNoStylusSequenceWasDelivered()
    {
        OnSta(() =>
        {
            var surface = CreateSurface();
            Assert.True(surface.HandlePointerSample(new Point(20, 25), .5, true, 0, PointerInputSource.PromotedPenMouse));
            Assert.True(surface.HandlePointerSample(new Point(80, 75), .5, true, 0, PointerInputSource.PromotedPenMouse));
            Assert.True(surface.HandlePointerSample(new Point(80, 75), 0, false, 0, PointerInputSource.PromotedPenMouse));

            Assert.Single(surface.Document.FreehandStrokes);
            Assert.Empty(surface.Document.Elements);
        });
    }

    [Fact]
    public void WinTabAndWpfPacketsForSameStrokeDoNotDuplicateInk()
    {
        OnSta(() =>
        {
            var surface = CreateSurface();
            surface.HandlePointerSample(new Point(20, 25), .5, true, 0, PointerInputSource.WinTab);
            surface.HandlePointerSample(new Point(22, 28), .5, true, 0, PointerInputSource.WpfPen);
            surface.HandlePointerSample(new Point(75, 85), .5, true, 0, PointerInputSource.WinTab);
            surface.HandlePointerSample(new Point(75, 85), 0, false, 0, PointerInputSource.WinTab);

            Assert.Single(surface.Document.FreehandStrokes);
            Assert.Equal(3, surface.Document.FreehandStrokes[0].Points.Count);
        });
    }

    [Fact]
    public void TipNeverCreatesNotesAndEachLiftEndsTheStroke()
    {
        OnSta(() =>
        {
            var surface = CreateSurface();
            surface.HandlePenSample(new Point(15, 20), .4, true, 0);
            surface.HandlePenSample(new Point(18, 26), 0, false, 0);
            surface.HandlePenSample(new Point(45, 60), .5, true, 0);
            surface.HandlePenSample(new Point(50, 65), 0, false, 0);

            Assert.Empty(surface.Document.Elements);
            Assert.Equal(2, surface.Document.FreehandStrokes.Count);
        });
    }

    [Fact]
    public void SelectToolMovesElementFromMappedPenInputInWindowMode()
    {
        OnSta(() =>
        {
            var process = new WorkflowShape { X = 20, Y = 30, Width = 100, Height = 70 };
            var surface = CreateSurface(new BoardDocument { Elements = [process] });
            surface.Tool = BoardTool.Select;
            surface.HandlePenSample(new Point(40, 50), .7, true, 0);
            surface.HandlePenSample(new Point(90, 110), .7, true, 0);
            surface.HandlePenSample(new Point(90, 110), 0, false, 0);

            Assert.Equal(process.Id, surface.SelectedElementId);
            var moved = Assert.IsType<WorkflowShape>(Assert.Single(surface.Document.Elements));
            Assert.Equal(70, moved.X);
            Assert.Equal(90, moved.Y);
        });
    }

    [Fact]
    public void PenStrokeInsideNoteUsesOneCoordinateSpaceForEverySample()
    {
        OnSta(() =>
        {
            var note = new StickyNote { X = 20, Y = 20, Width = 100, Height = 80 };
            var surface = CreateSurface(new BoardDocument { Elements = [note] });
            surface.HandlePenSample(new Point(30, 30), .4, true, 0);
            surface.HandlePenSample(new Point(90, 70), .8, true, 0);
            surface.HandlePenSample(new Point(90, 70), 0, false, 0);

            var updated = Assert.IsType<StickyNote>(Assert.Single(surface.Document.Elements));
            var stroke = Assert.Single(updated.Strokes);
            Assert.Equal(.1, stroke.Points[0].X, 4);
            Assert.Equal(.7, stroke.Points[1].X, 4);
            Assert.All(stroke.Points, point => Assert.InRange(point.X, 0, 1));
        });
    }

    [Fact]
    public void MouseStrokeInsideNoteUsesOneCoordinateSpaceForEverySample()
    {
        OnSta(() =>
        {
            var note = new StickyNote { X = 20, Y = 20, Width = 100, Height = 80 };
            var surface = CreateSurface(new BoardDocument { Elements = [note] });
            surface.HandlePointerSample(new Point(30, 30), .55, true, 0);
            surface.HandlePointerSample(new Point(90, 70), .55, true, 0);
            surface.HandlePointerSample(new Point(90, 70), 0, false, 0);

            var updated = Assert.IsType<StickyNote>(Assert.Single(surface.Document.Elements));
            var stroke = Assert.Single(updated.Strokes);
            Assert.Equal(.1, stroke.Points[0].X, 4);
            Assert.Equal(.7, stroke.Points[1].X, 4);
            Assert.All(stroke.Points, point => Assert.InRange(point.X, 0, 1));
        });
    }

    [Fact]
    public void SideButtonCreatesOneNotePerPress()
    {
        OnSta(() =>
        {
            var surface = CreateSurface();
            surface.HandlePenSample(new Point(40, 45), 0, false, 2);
            surface.HandlePenSample(new Point(40, 45), 0, false, 2);
            surface.HandlePenSample(new Point(40, 45), 0, false, 0);
            surface.HandlePenSample(new Point(40, 45), 0, false, 2);

            Assert.Equal(2, surface.Document.Elements.OfType<StickyNote>().Count());
            Assert.Empty(surface.Document.FreehandStrokes);
        });
    }

    [Fact]
    public void PenSamplesOutsideCanvasAreClampedAtCanvasBoundary()
    {
        OnSta(() =>
        {
            var surface = CreateSurface();
            surface.HandlePenSample(new Point(300, 200), .5, true, 0);
            surface.HandlePenSample(new Point(500, 400), .5, true, 0);
            surface.HandlePenSample(new Point(500, 400), 0, false, 0);

            var stroke = Assert.Single(surface.Document.FreehandStrokes);
            Assert.Equal(400, stroke.Points[^1].X);
            Assert.Equal(300, stroke.Points[^1].Y);
        });
    }

    private static BoardSurface CreateSurface(BoardDocument? document = null)
    {
        var surface = new BoardSurface();
        surface.Measure(new Size(400, 300));
        surface.Arrange(new Rect(0, 0, 400, 300));
        surface.LoadAsync(document ?? new BoardDocument(), "test.flowboard").GetAwaiter().GetResult();
        return surface;
    }

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }
}
