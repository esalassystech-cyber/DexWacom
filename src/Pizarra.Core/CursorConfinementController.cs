namespace Pizarra.Core;

public enum CursorReleaseReason { Escape, Deactivated, Minimized, Closed, TabletDisconnected }

public interface ICursorClipper
{
    bool TryClip(RectD screenBounds);
    void Release();
}

public sealed class CursorConfinementController(ICursorClipper clipper)
{
    public bool IsConfined { get; private set; }

    public void ActivateBoard(RectD screenBounds, bool wacomDetected)
    {
        IsConfined = false;
        if (!wacomDetected || !IsValid(screenBounds)) { clipper.Release(); return; }
        try { IsConfined = clipper.TryClip(screenBounds); }
        catch { IsConfined = false; }
        if (!IsConfined) clipper.Release();
    }

    public void UpdateBoardBounds(RectD screenBounds)
    {
        if (!IsConfined) return;
        if (!IsValid(screenBounds)) { Release(CursorReleaseReason.Deactivated); return; }
        try { IsConfined = clipper.TryClip(screenBounds); }
        catch { IsConfined = false; }
        if (!IsConfined) clipper.Release();
    }

    public void Release(CursorReleaseReason reason)
    {
        _ = reason;
        IsConfined = false;
        clipper.Release();
    }

    private static bool IsValid(RectD rect) =>
        double.IsFinite(rect.X) && double.IsFinite(rect.Y) && double.IsFinite(rect.Width) && double.IsFinite(rect.Height) && rect.Width > 0 && rect.Height > 0;
}
