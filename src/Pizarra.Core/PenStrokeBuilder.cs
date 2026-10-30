namespace Pizarra.Core;

/// <summary>Maintains explicit pen-down to pen-up stroke boundaries.</summary>
public sealed class PenStrokeBuilder
{
    private List<InkPoint>? _points;

    public bool IsActive => _points is not null;
    public IReadOnlyList<InkPoint> Points => _points is null ? Array.Empty<InkPoint>() : _points;

    public void Begin(InkPoint firstPoint)
    {
        if (IsActive) throw new InvalidOperationException("El trazo anterior debe terminar antes de iniciar otro.");
        Validate(firstPoint);
        _points = [firstPoint];
    }

    public void Append(InkPoint point)
    {
        if (_points is null) return;
        Validate(point);
        _points.Add(point);
    }

    public InkStroke End()
    {
        var points = _points ?? throw new InvalidOperationException("No hay un trazo activo.");
        _points = null;
        return new InkStroke { Points = points };
    }

    public void Cancel() => _points = null;

    private static void Validate(InkPoint point)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Pressure) || point.Pressure is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(point), "El punto y la presión deben ser finitos y válidos.");
    }
}

/// <summary>Deduplicates stylus-button down notifications until a matching up event.</summary>
public sealed class StylusButtonPressTracker
{
    private readonly HashSet<Guid> _pressed = [];
    public bool TryPress(Guid buttonId) => _pressed.Add(buttonId);
    public void Release(Guid buttonId) => _pressed.Remove(buttonId);
    public void Clear() => _pressed.Clear();
}
