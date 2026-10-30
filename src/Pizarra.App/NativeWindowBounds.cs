using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Pizarra.Core;

namespace Pizarra.App;

/// <summary>
/// Rectangles and points in physical desktop pixels, the space WinTab and ClipCursor use. Relies on the
/// PerMonitorV2 manifest. Avoids PointToScreen/PointFromScreen, which round every point to a whole pixel
/// (same approach as WinPenKit.Wpf.WpfCoordinates).
/// </summary>
internal static class NativeWindowBounds
{
    /// <summary>The window's client area: everything the app draws, without frame, title bar or the invisible resize borders.</summary>
    public static Rect ClientArea(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !GetClientRect(handle, out var client)) return Rect.Empty;
        var origin = new POINT();
        if (!ClientToScreen(handle, ref origin)) return Rect.Empty;
        var width = client.Right - client.Left;
        var height = client.Bottom - client.Top;
        return width < 1 || height < 1 ? Rect.Empty : new Rect(origin.X, origin.Y, width, height);
    }

    /// <summary>The element's on-screen rectangle.</summary>
    public static Rect ElementArea(FrameworkElement element)
    {
        if (ElementTransform(element) is not { } t || element.ActualWidth < 1 || element.ActualHeight < 1) return Rect.Empty;
        return new Rect(t.Origin.X, t.Origin.Y, element.ActualWidth * t.ScaleX, element.ActualHeight * t.ScaleY);
    }

    /// <summary>Converts a desktop pixel (a WinTab packet) to the element's own DIPs, keeping the fraction.</summary>
    public static Point? ScreenToElement(Visual element, Point screen)
    {
        if (ElementTransform(element) is not { } t) return null;
        var local = PenCoordinateMapper.ToCanvasWorld(
            new PenScreenPoint(new PointD(screen.X, screen.Y), new PointD(t.Origin.X, t.Origin.Y), t.ScaleX, t.ScaleY),
            new RectD(0, 0, 0, 0), new CanvasTransform(1, 0, 0));
        return new Point(local.X, local.Y);
    }

    public static Point? CursorPosition() => GetCursorPos(out var point) ? new Point(point.X, point.Y) : null;

    private static (Point Origin, double ScaleX, double ScaleY)? ElementTransform(Visual element)
    {
        if (PresentationSource.FromVisual(element) is not HwndSource source || source.RootVisual is null) return null;
        var clientOrigin = new POINT();
        if (!ClientToScreen(source.Handle, ref clientOrigin)) return null;
        Point offset;
        try { offset = element.TransformToAncestor(source.RootVisual).Transform(new Point(0, 0)); }
        catch (InvalidOperationException) { return null; }
        var dpi = VisualTreeHelper.GetDpi(element);
        var scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1;
        var scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1;
        return (new Point(clientOrigin.X + offset.X * scaleX, clientOrigin.Y + offset.Y * scaleY), scaleX, scaleY);
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out POINT point);
}
