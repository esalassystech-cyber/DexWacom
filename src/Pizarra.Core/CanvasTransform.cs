namespace Pizarra.Core;

public sealed record CanvasTransform
{
    public CanvasTransform(double zoom, double panX, double panY)
    {
        if (!double.IsFinite(zoom) || zoom <= 0)
            throw new ArgumentOutOfRangeException(nameof(zoom), "El zoom debe ser positivo y finito.");
        if (!double.IsFinite(panX) || !double.IsFinite(panY))
            throw new ArgumentOutOfRangeException(nameof(panX), "El desplazamiento debe ser finito.");

        Zoom = zoom;
        PanX = panX;
        PanY = panY;
    }

    public double Zoom { get; }
    public double PanX { get; }
    public double PanY { get; }

    public PointD ScreenToWorld(PointD screenPoint, RectD viewport) => new(
        (screenPoint.X - viewport.X - PanX) / Zoom,
        (screenPoint.Y - viewport.Y - PanY) / Zoom);

    public PointD WorldToScreen(PointD worldPoint, RectD viewport) => new(
        viewport.X + PanX + worldPoint.X * Zoom,
        viewport.Y + PanY + worldPoint.Y * Zoom);
}

public sealed record PenScreenPoint(PointD DesktopPixels, PointD ElementOriginPixels, double ScaleX, double ScaleY);

public static class PenCoordinateMapper
{
    public static PointD ToCanvasWorld(PenScreenPoint point, RectD viewport, CanvasTransform transform)
    {
        if (!double.IsFinite(point.ScaleX) || point.ScaleX <= 0 || !double.IsFinite(point.ScaleY) || point.ScaleY <= 0)
            throw new ArgumentOutOfRangeException(nameof(point), "La escala DPI debe ser positiva y finita.");
        var localDip = new PointD(
            (point.DesktopPixels.X - point.ElementOriginPixels.X) / point.ScaleX,
            (point.DesktopPixels.Y - point.ElementOriginPixels.Y) / point.ScaleY);
        return transform.ScreenToWorld(localDip, viewport);
    }
}
