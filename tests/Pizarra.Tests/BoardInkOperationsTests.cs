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
}
