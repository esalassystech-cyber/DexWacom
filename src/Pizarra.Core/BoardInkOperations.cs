namespace Pizarra.Core;

public static class BoardInkOperations
{
    public static BoardDocument AppendStroke(BoardDocument board, InkStroke stroke, Guid? stickyNoteId)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(stroke);
        if (stickyNoteId is null)
            return board with { FreehandStrokes = [.. board.FreehandStrokes, stroke] };

        var found = false;
        var elements = board.Elements.Select(element =>
        {
            if (element is not StickyNote note || note.Id != stickyNoteId.Value) return element;
            found = true;
            return note with { Strokes = [.. note.Strokes, stroke] };
        }).ToList();
        return found ? board with { Elements = elements } : board;
    }
}
