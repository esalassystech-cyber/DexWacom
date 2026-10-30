using System.Runtime.InteropServices;

namespace Pizarra.App;

/// <summary>
/// Recognizes mouse messages that Windows synthesizes from the pen. WPF only marks them through
/// StylusDevice when Windows Ink is on; the extra-info signature is present either way.
/// </summary>
internal static class PenMouseMessages
{
    private const long SignatureMask = 0xFFFFFF00;
    private const long PenSignature = 0xFF515700;

    /// <summary>Valid only while the mouse message being handled is the one being dispatched.</summary>
    public static bool CurrentMessageIsFromPen() => (GetMessageExtraInfo().ToInt64() & SignatureMask) == PenSignature;

    [DllImport("user32.dll")] private static extern IntPtr GetMessageExtraInfo();
}
