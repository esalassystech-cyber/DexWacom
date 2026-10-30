namespace Pizarra.Core;

/// <summary>
/// WinTab target rectangle. The driver maps linearly, value = origin + t × extent, and clamps to the
/// rectangle, so a reversed axis needs its origin on the far edge (measured on the CTL-4100WL: origin on
/// the top edge with a negative extent pinned every packet to that edge).
/// </summary>
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

    /// <summary>
    /// Value the driver reports for a pen at normalized tablet coordinates: X grows to the right and
    /// <paramref name="tabletUpY"/> grows up from the tablet's bottom edge, as WinTab measures it.
    /// </summary>
    public PointD Map(double tabletX, double tabletUpY) => new(OriginX + tabletX * ExtentX, OriginY + tabletUpY * ExtentY);
}
