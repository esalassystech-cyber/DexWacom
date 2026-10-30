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

    /// <summary>
    /// Removes every stroke that passes within <paramref name="radius"/> of a board point: free strokes in board
    /// coordinates and, inside a sticky note, the note's strokes (stored relative to the note). Returns the same
    /// instance when nothing was touched.
    /// </summary>
    public static BoardDocument EraseAt(BoardDocument board, PointD point, double radius)
    {
        ArgumentNullException.ThrowIfNull(board);
        var free = board.FreehandStrokes.Where(stroke => !Touches(stroke, point, radius, p => p)).ToList();
        var notesChanged = false;
        var elements = board.Elements.Select(element =>
        {
            if (element is not StickyNote note || note.Strokes.Count == 0
                || point.X < note.X || point.Y < note.Y || point.X > note.X + note.Width || point.Y > note.Y + note.Height)
                return element;
            var kept = note.Strokes.Where(stroke => !Touches(stroke, point, radius,
                p => new PointD(note.X + p.X * note.Width, note.Y + p.Y * note.Height))).ToList();
            if (kept.Count == note.Strokes.Count) return element;
            notesChanged = true;
            return note with { Strokes = kept };
        }).ToList();

        if (free.Count == board.FreehandStrokes.Count && !notesChanged) return board;
        return board with { FreehandStrokes = free, Elements = notesChanged ? elements : board.Elements };
    }

    private static bool Touches(InkStroke stroke, PointD point, double radius, Func<PointD, PointD> toBoard)
    {
        var points = stroke.Points.Select(p => toBoard(new PointD(p.X, p.Y))).ToList();
        if (points.Count == 1) return Distance(points[0], point) <= radius;
        for (var i = 1; i < points.Count; i++)
            if (SegmentDistance(point, points[i - 1], points[i]) <= radius) return true;
        return false;
    }

    private static double Distance(PointD a, PointD b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static double SegmentDistance(PointD p, PointD a, PointD b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0) return Distance(p, a);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0, 1);
        return Distance(p, new PointD(a.X + t * dx, a.Y + t * dy));
    }
}
