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

public sealed record WinTabInputMapping(WindowSystemMapping Cursor, WindowSystemMapping Packets)
{
    public static WinTabInputMapping Create(RectD bounds, bool reverseCursorX, bool reverseCursorY, bool reversePacketX, bool reversePacketY) =>
        new(WindowSystemMapping.Create(bounds, reverseCursorX, reverseCursorY),
            WindowSystemMapping.Create(bounds, reversePacketX, reversePacketY));
}
