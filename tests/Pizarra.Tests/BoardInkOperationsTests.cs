using Pizarra.Core;

namespace Pizarra.Tests;

public sealed class BoardInkOperationsTests
{
    [Fact]
    public void FreehandStrokeOnBackgroundDoesNotCreateStickyNote()
    {
        var board = new BoardDocument { Name = "Fondo" };
        var stroke = new InkStroke { Points = [new InkPoint(120, 88, .3), new InkPoint(140, 96, .9)] };

        var updated = BoardInkOperations.AppendStroke(board, stroke, stickyNoteId: null);

        Assert.Empty(updated.Elements);
        Assert.Empty(updated.Connectors);
        Assert.Same(stroke, Assert.Single(updated.FreehandStrokes));
    }

    [Fact]
    public void StrokeInsideStickyNoteStaysInThatNoteAndDoesNotEnterFreehandLayer()
    {
        var note = new StickyNote { Title = "Prueba", X = 50, Y = 60, Width = 200, Height = 140 };
        var board = new BoardDocument { Name = "Nota", Elements = [note] };
        var stroke = new InkStroke { Points = [new InkPoint(.2, .3, .4), new InkPoint(.6, .7, .8)] };

        var updated = BoardInkOperations.AppendStroke(board, stroke, note.Id);

        Assert.Empty(updated.FreehandStrokes);
        var updatedNote = Assert.IsType<StickyNote>(Assert.Single(updated.Elements));
        Assert.Same(stroke, Assert.Single(updatedNote.Strokes));
        Assert.Equal(note.Id, updatedNote.Id);
    }

    [Fact]
    public void EraserRemovesOnlyTheFreeStrokeItTouches()
    {
        var touched = new InkStroke { Points = [new InkPoint(0, 0, .5), new InkPoint(100, 0, .5)] };
        var far = new InkStroke { Points = [new InkPoint(0, 300, .5), new InkPoint(100, 300, .5)] };
        var board = new BoardDocument { Name = "Goma", FreehandStrokes = [touched, far] };

        var updated = BoardInkOperations.EraseAt(board, new PointD(50, 6), radius: 8);

        Assert.Same(far, Assert.Single(updated.FreehandStrokes));
    }

    [Fact]
    public void EraserReachesStrokesInsideAStickyNote()
    {
        var stroke = new InkStroke { Points = [new InkPoint(.1, .5, .5), new InkPoint(.9, .5, .5)] };
        var note = new StickyNote { X = 100, Y = 100, Width = 200, Height = 100, Strokes = [stroke] };
        var board = new BoardDocument { Name = "Nota", Elements = [note] };

        var updated = BoardInkOperations.EraseAt(board, new PointD(200, 150), radius: 8);

        Assert.Empty(Assert.IsType<StickyNote>(Assert.Single(updated.Elements)).Strokes);
    }

    [Fact]
    public void EraserMissReturnsTheSameBoard()
    {
        var board = new BoardDocument { Name = "Nada", FreehandStrokes = [new InkStroke { Points = [new InkPoint(0, 0, .5)] }] };

        Assert.Same(board, BoardInkOperations.EraseAt(board, new PointD(500, 500), radius: 8));
    }
}
