namespace Pizarra.Core;

/// <summary>Wintab system-coordinate target rectangle, with axis direction preserved from driver defaults.</summary>
public sealed record WindowSystemMapping(int OriginX, int ExtentX, int OriginY, int ExtentY)
{
    public static WindowSystemMapping Create(RectD bounds, bool reverseX, bool reverseY)
    {
        if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) || !double.IsFinite(bounds.Width)
            || !double.IsFinite(bounds.Height) || bounds.Width < 1 || bounds.Height < 1)
            throw new ArgumentOutOfRangeException(nameof(bounds), "La ventana debe tener límites válidos y tamaño positivo.");

        var x = (int)Math.Round(bounds.X);
        var y = (int)Math.Round(bounds.Y);
        var width = Math.Max(1, (int)Math.Round(bounds.Width));
        var height = Math.Max(1, (int)Math.Round(bounds.Height));
        return new WindowSystemMapping(
            reverseX ? x + width : x,
            reverseX ? -width : width,
            reverseY ? y + height : y,
            reverseY ? -height : height);
    }

    /// <summary>Projects normalized tablet coordinates to this mapping's screen rectangle.</summary>
    public PointD Map(double tabletX, double tabletY) => new(OriginX + tabletX * ExtentX, OriginY + tabletY * ExtentY);
}
