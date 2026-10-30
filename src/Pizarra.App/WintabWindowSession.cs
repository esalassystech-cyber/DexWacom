using System.Runtime.InteropServices;
using System.Windows;
using Pizarra.Core;

namespace Pizarra.App;

/// <summary>Private WinTab system context whose cursor and packet coordinates target this app window.</summary>
internal sealed class WintabWindowSession : IDisposable
{
    private const uint DefaultSystemContext = 4;
    private const uint CxoSystem = 0x1;
    private const uint CxoMessages = 0x4;
    private const uint PkStatus = 0x2;
    private const uint PkCursor = 0x20;
    private const uint PkButtons = 0x40;
    private const uint PkX = 0x80;
    private const uint PkY = 0x100;
    private const uint PkNormalPressure = 0x400;
    private const uint PkData = PkStatus | PkCursor | PkButtons | PkX | PkY | PkNormalPressure;
    private const uint DeviceCategory = 100;
    private const uint DeviceNormalPressure = 15;
    private const uint CursorCategory = 1000;
    private const uint CursorButtonMap = 7;
    private const uint CursorNormalPressureButton = 9;
    private readonly IntPtr _windowHandle;
    private IntPtr _contextHandle;
    private uint _pressureMinimum;
    private uint _pressureRange = 2047;
    private uint _tipButtonMask;
    private WinTabMessageRouter? _messageRouter;
    private WinTabInputMapping? _activeMapping;
    private readonly PacketYOrientation _yOrientation = new();
    private bool _disposed;
    private bool _hasReportedValidPacket;

    public WintabWindowSession(IntPtr windowHandle) => _windowHandle = windowHandle;

    public bool IsActive => _contextHandle != IntPtr.Zero;
    public bool PacketYMirrored => _yOrientation.Mirrored;
    public bool PacketYOrientationDecided => _yOrientation.Decided;
    public event Action<Point, double, bool, uint>? SampleReceived;
    public event Action<int, int, uint, uint, uint, uint>? RawPacketReceived;
    public event Action<bool>? ProximityChanged;
    public event Action<string>? StatusChanged;

    public bool Activate(Rect bounds)
    {
        CloseContext();
        _hasReportedValidPacket = false;
        if (_disposed || !Valid(bounds)) return SetFailure("Vinculación Wacom no disponible · ventana sin tamaño válido");
        try
        {
            if (WTInfoA(DefaultSystemContext, 0, out LogContext context) == 0)
                return SetFailure("WinTab no está disponible · entrada WPF y mouse activos");

            context.lcName = "Pizarra Workflows · ventana";
            context.lcMsgBase = 0x7FF0;
            context.lcOptions |= CxoSystem | CxoMessages;
            context.lcPktData = PkData;
            context.lcPktMode = 0;
            context.lcMoveMask = PkStatus | PkX | PkY | PkNormalPressure;
            context.lcBtnDnMask = uint.MaxValue;
            context.lcBtnUpMask = uint.MaxValue;
            _activeMapping = SetWindowMapping(ref context, bounds);

            _contextHandle = WTOpenA(_windowHandle, ref context, true);
            if (_contextHandle == IntPtr.Zero)
                return SetFailure("El driver Wacom rechazó la vinculación · entrada WPF y mouse activos");

            _messageRouter = new WinTabMessageRouter(context.lcMsgBase, _contextHandle);
            if (!WTGetA(_contextHandle, out var accepted) || !MappingMatches(accepted, _activeMapping))
                return SetFailure("El driver no aceptó el mapeo completo de ventana · entrada WPF y mouse activos");
            BringToTop();

            (_pressureMinimum, _pressureRange) = ReadPressureRange(context.lcDevice);
            StatusChanged?.Invoke("WinTab disponible · esperando lápiz (entrada básica activa)");
            return true;
        }
        catch (DllNotFoundException)
        {
            return SetFailure("WinTab no está instalado · entrada WPF y mouse activos");
        }
        catch (EntryPointNotFoundException)
        {
            return SetFailure("El driver Wacom no ofrece WinTab compatible · entrada WPF y mouse activos");
        }
        catch (Exception ex)
        {
            return SetFailure($"No se pudo vincular Wacom · {ex.Message}");
        }
    }

    public bool UpdateBounds(Rect bounds)
    {
        if (!IsActive) return false;
        if (!Valid(bounds)) return false;
        try
        {
            if (!WTGetA(_contextHandle, out var context)) return Activate(bounds);
            var mapping = SetWindowMapping(ref context, bounds);
            if (WTSetA(_contextHandle, ref context) && WTGetA(_contextHandle, out var accepted) && MappingMatches(accepted, mapping))
            {
                _activeMapping = mapping;
                BringToTop();
                return true;
            }
            return Activate(bounds);
        }
        catch (Exception ex)
        {
            CloseContext();
            SetFailure($"Se liberó la vinculación Wacom · {ex.Message}");
            return false;
        }
    }

    /// <summary>Processes WinTab messages forwarded by the owning window's native message hook.</summary>
    public bool ProcessMessage(int message, IntPtr wParam, IntPtr lParam)
    {
        if (_contextHandle == IntPtr.Zero || _messageRouter is null) return false;
        switch (_messageRouter.Route(message, wParam, lParam))
        {
        case WinTabMessageKind.Packet:
            var packet = new Packet();
            if (!WTPacket(_contextHandle, unchecked((uint)wParam.ToInt64()), ref packet))
                return true; // delayed packet notifications can arrive after a cursor/context transition

            _tipButtonMask = ReadNormalPressureButtonMask(packet.Cursor);
            var rawButtons = packet.Buttons;
            RawPacketReceived?.Invoke(packet.X, packet.Y, packet.Pressure, rawButtons, packet.Cursor, _tipButtonMask);
            var tipDown = packet.Pressure > _pressureMinimum || (rawButtons & _tipButtonMask) != 0;
            var pressure = Math.Clamp(Math.Max(0L, (long)packet.Pressure - _pressureMinimum) / (double)_pressureRange, 0, 1);
            var normalizedButtons = rawButtons & ~_tipButtonMask;
            if (tipDown) normalizedButtons |= 1;
            var area = _activeMapping!.Packets;
            if (!tipDown && NativeWindowBounds.CursorPosition() is Point cursor)
                _yOrientation.Observe(packet.Y, cursor.Y, area.OriginY, area.ExtentY);
            var screenY = _yOrientation.ToScreen(packet.Y, area.OriginY, area.ExtentY);
            SampleReceived?.Invoke(new Point(packet.X, screenY), pressure, tipDown, normalizedButtons);
            if (!_hasReportedValidPacket)
            {
                _hasReportedValidPacket = true;
                StatusChanged?.Invoke("Wacom vinculada · entrada WinTab válida");
            }
            return true;
        case WinTabMessageKind.ContextClose:
            Deactivate();
            StatusChanged?.Invoke("WinTab perdió el contexto · entrada básica activa");
            return true;
        case WinTabMessageKind.Proximity:
            var inProximity = (unchecked((uint)lParam.ToInt64()) & 0xFFFF) != 0;
            ProximityChanged?.Invoke(inProximity);
            return true;
        case WinTabMessageKind.InfoChange:
            var bounds = ReadCurrentWindowBounds?.Invoke() ?? Rect.Empty;
            Activate(bounds);
            return true;
        default:
            return false;
        }
    }

    /// <summary>The owner supplies current client bounds for driver information-change recovery.</summary>
    public Func<Rect>? ReadCurrentWindowBounds { get; set; }

    public void Deactivate()
    {
        CloseContext();
        ProximityChanged?.Invoke(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseContext();
    }

    /// <summary>The driver applies the cursor rectangle (lcSys) of the topmost system context, so keep ours on top.</summary>
    private void BringToTop()
    {
        try { WTOverlap(_contextHandle, true); }
        catch (EntryPointNotFoundException) { }
    }

    private bool SetFailure(string message)
    {
        CloseContext();
        StatusChanged?.Invoke(message);
        return false;
    }

    private void CloseContext()
    {
        if (_contextHandle == IntPtr.Zero) return;
        try { WTClose(_contextHandle); }
        catch { }
        _contextHandle = IntPtr.Zero;
        _messageRouter = null;
        _activeMapping = null;
    }

    private static (uint Minimum, uint Range) ReadPressureRange(uint device)
    {
        try
        {
            if (WTInfoA(DeviceCategory + device, DeviceNormalPressure, out Axis axis) != 0 && axis.Maximum > axis.Minimum)
                return ((uint)Math.Max(0, axis.Minimum), (uint)(axis.Maximum - axis.Minimum));
        }
        catch { }
        return (0, 2047);
    }

    private static WinTabInputMapping SetWindowMapping(ref LogContext context, Rect bounds)
    {
        var mapping = WinTabInputMapping.Create(new RectD(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            context.lcSysExtX < 0, context.lcSysExtY < 0);
        context.lcSysOrgX = mapping.Cursor.OriginX;
        context.lcSysExtX = mapping.Cursor.ExtentX;
        context.lcSysOrgY = mapping.Cursor.OriginY;
        context.lcSysExtY = mapping.Cursor.ExtentY;
        context.lcSysMode = 0;
        context.lcOutOrgX = mapping.Packets.OriginX;
        context.lcOutExtX = mapping.Packets.ExtentX;
        context.lcOutOrgY = mapping.Packets.OriginY;
        context.lcOutExtY = mapping.Packets.ExtentY;
        return mapping;
    }

    private static bool MappingMatches(LogContext context, WinTabInputMapping? mapping) => mapping is not null
        && context.lcSysOrgX == mapping.Cursor.OriginX && context.lcSysExtX == mapping.Cursor.ExtentX
        && context.lcSysOrgY == mapping.Cursor.OriginY && context.lcSysExtY == mapping.Cursor.ExtentY
        && context.lcOutOrgX == mapping.Packets.OriginX && context.lcOutExtX == mapping.Packets.ExtentX
        && context.lcOutOrgY == mapping.Packets.OriginY && context.lcOutExtY == mapping.Packets.ExtentY;

    private static uint ReadNormalPressureButtonMask(uint cursor)
    {
        try
        {
            if (WTInfoA(CursorCategory + cursor, CursorNormalPressureButton, out byte physical) == 0 || physical is 0 or > 32)
                return 0;
            var map = new byte[32];
            var pin = GCHandle.Alloc(map, GCHandleType.Pinned);
            try
            {
                if (WTInfoPointer(CursorCategory + cursor, CursorButtonMap, pin.AddrOfPinnedObject()) == 0) return 0;
                var logical = map[physical - 1];
                return logical is 0 or > 32 ? 0 : 1u << (int)(logical - 1);
            }
            finally { pin.Free(); }
        }
        catch { return 0; }
    }

    private static bool Valid(Rect rect) => !rect.IsEmpty && double.IsFinite(rect.X) && double.IsFinite(rect.Y)
        && double.IsFinite(rect.Width) && double.IsFinite(rect.Height) && rect.Width >= 1 && rect.Height >= 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct LogContext
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)] public string lcName;
        public uint lcOptions, lcStatus, lcLocks, lcMsgBase, lcDevice, lcPktRate, lcPktData, lcPktMode, lcMoveMask, lcBtnDnMask, lcBtnUpMask;
        public int lcInOrgX, lcInOrgY, lcInOrgZ, lcInExtX, lcInExtY, lcInExtZ, lcOutOrgX, lcOutOrgY, lcOutOrgZ, lcOutExtX, lcOutExtY, lcOutExtZ;
        public uint lcSensX, lcSensY, lcSensZ;
        public int lcSysMode, lcSysOrgX, lcSysOrgY, lcSysExtX, lcSysExtY;
        public uint lcSysSensX, lcSysSensY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Packet { public uint Status, Cursor, Buttons; public int X, Y; public uint Pressure; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Axis { public int Minimum, Maximum; public uint Units, Resolution; }

    [DllImport("Wintab32.dll", EntryPoint = "WTInfoA", CharSet = CharSet.Ansi)]
    private static extern uint WTInfoA(uint category, uint index, out LogContext context);
    [DllImport("Wintab32.dll", EntryPoint = "WTInfoA")]
    private static extern uint WTInfoA(uint category, uint index, out Axis axis);
    [DllImport("Wintab32.dll", EntryPoint = "WTInfoA")]
    private static extern uint WTInfoA(uint category, uint index, out byte value);
    [DllImport("Wintab32.dll", EntryPoint = "WTInfoA")]
    private static extern uint WTInfoPointer(uint category, uint index, IntPtr value);
    [DllImport("Wintab32.dll", EntryPoint = "WTOpenA", CharSet = CharSet.Ansi)]
    private static extern IntPtr WTOpenA(IntPtr hwnd, ref LogContext context, [MarshalAs(UnmanagedType.Bool)] bool enable);
    [DllImport("Wintab32.dll", EntryPoint = "WTGetA", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTGetA(IntPtr contextHandle, out LogContext context);
    [DllImport("Wintab32.dll", EntryPoint = "WTSetA", CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTSetA(IntPtr contextHandle, ref LogContext context);
    [DllImport("Wintab32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTClose(IntPtr contextHandle);
    [DllImport("Wintab32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTOverlap(IntPtr contextHandle, [MarshalAs(UnmanagedType.Bool)] bool toTop);
    [DllImport("Wintab32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTPacket(IntPtr contextHandle, uint serialNumber, ref Packet packet);
}
