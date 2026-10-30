namespace Pizarra.Core;

public enum PointerInputSource
{
    Mouse,
    PromotedPenMouse,
    WpfPen,
    WinTab
}

/// <summary>Locks one contact to one input stream until its matching lift or cancellation.</summary>
public sealed class PointerInputCoordinator
{
    private PointerInputSource? _contactSource;

    public bool HasActiveContact => _contactSource is not null;
    public PointerInputSource? ContactSource => _contactSource;

    public bool Accept(PointerInputSource source, bool tipDown)
    {
        if (tipDown)
        {
            if (_contactSource is null) _contactSource = source;
            return _contactSource == source;
        }

        if (_contactSource != source) return false;
        _contactSource = null;
        return true;
    }

    public void Cancel() => _contactSource = null;
}
