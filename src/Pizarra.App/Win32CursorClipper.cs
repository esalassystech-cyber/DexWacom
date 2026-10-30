using System.Runtime.InteropServices;
using Pizarra.Core;

namespace Pizarra.App;

public sealed class Win32CursorClipper : ICursorClipper
{
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ClipCursor(ref RECT rect);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ClipCursor(IntPtr rect);

    public bool TryClip(RectD screenBounds)
    {
        if (!OperatingSystem.IsWindows() || screenBounds.Width <= 0 || screenBounds.Height <= 0) return false;
        var rect = new RECT { Left = (int)Math.Floor(screenBounds.X), Top = (int)Math.Floor(screenBounds.Y), Right = (int)Math.Ceiling(screenBounds.X + screenBounds.Width), Bottom = (int)Math.Ceiling(screenBounds.Y + screenBounds.Height) };
        return ClipCursor(ref rect);
    }

    public void Release() { if (OperatingSystem.IsWindows()) _ = ClipCursor(IntPtr.Zero); }
}
