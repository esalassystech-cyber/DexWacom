namespace Pizarra.Core;

public enum WinTabMessageKind { Ignore, Packet, ContextClose, Proximity, InfoChange }

/// <summary>Decodes the WinTab message range for one live context and rejects stale packets.</summary>
public sealed class WinTabMessageRouter(uint messageBase, nint contextHandle)
{
    public WinTabMessageKind Route(int message, nint wParam, nint lParam)
    {
        if (message == unchecked((int)(messageBase + 0)) && lParam == contextHandle)
            return WinTabMessageKind.Packet;
        if (message == unchecked((int)(messageBase + 2)) && wParam == contextHandle)
            return WinTabMessageKind.ContextClose;
        if (message == unchecked((int)(messageBase + 5))) return WinTabMessageKind.Proximity;
        if (message == unchecked((int)(messageBase + 6))) return WinTabMessageKind.InfoChange;
        return WinTabMessageKind.Ignore;
    }
}

/// <summary>
/// Cursor keeps the axis directions of the driver's system rectangle. Packets use positive extents only:
/// the CTL-4100WL driver pins every packet to the origin when an output extent is negative (logged with the
/// origin on the top edge and on the bottom edge), so Y is mirrored in the app by <see cref="PacketYOrientation"/>.
/// </summary>
public sealed record WinTabInputMapping(WindowSystemMapping Cursor, WindowSystemMapping Packets)
{
    public static WinTabInputMapping Create(RectD bounds, bool reverseCursorX, bool reverseCursorY) =>
        new(WindowSystemMapping.Create(bounds, reverseCursorX, reverseCursorY),
            WindowSystemMapping.Create(bounds, reverseX: false, reverseY: false));
}

/// <summary>
/// Turns packet Y into screen Y inside the packet area. WinTab's positive extent grows upward, so packets start
/// mirrored; hover packets are then compared with the system cursor, which the driver already maps onto the same
/// area, to confirm or correct that before relying on it.
/// </summary>
public sealed class PacketYOrientation
{
    private const int VotesNeeded = 5;
    private int _mirroredVotes;
    private int _directVotes;

    public bool Mirrored { get; private set; } = true;
    public bool Decided { get; private set; }

    public double ToScreen(double packetY, double top, double height) => Mirrored ? 2 * top + height - packetY : packetY;

    public void Observe(double packetY, double cursorY, double top, double height)
    {
        if (Decided || height < 1) return;
        var direct = Math.Abs(packetY - cursorY);
        var mirrored = Math.Abs(2 * top + height - packetY - cursorY);
        if (Math.Abs(direct - mirrored) < height * .2) return; // near the middle both readings agree
        if (mirrored < direct) _mirroredVotes++; else _directVotes++;
        if (_mirroredVotes + _directVotes < VotesNeeded) return;
        Mirrored = _mirroredVotes >= _directVotes;
        Decided = true;
    }
}
