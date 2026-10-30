using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Pizarra.Core;

namespace Pizarra.App;

/// <summary>A separate pen test surface. It never loads or stores board contents.</summary>
internal sealed class WacomDiagnosticsWindow : Window
{
    private readonly DiagnosticSurface _surface = new();
    private readonly TextBlock _status = new();
    private readonly string _logPath;
    private StreamWriter? _log;
    private HwndSource? _hwndSource;
    private WintabWindowSession? _wintab;

    public WacomDiagnosticsWindow()
    {
        Title = "Diagnóstico Wacom · Pizarra Workflows";
        Width = 900; Height = 650; MinWidth = 600; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = BrushFromRgb(14, 21, 38); Foreground = Brushes.White;
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PizarraWorkflows", "diagnostics");
        Directory.CreateDirectory(folder);
        _logPath = Path.Combine(folder, $"wacom-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl");
        _log = new StreamWriter(new FileStream(_logPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        _surface.SampleObserved += (source, kind, point, pressure, buttons, accepted) =>
        {
            _status.Text = $"{source} · {kind} · X {point.X:0.0}, Y {point.Y:0.0} · presión {pressure:0.000} · botones 0x{buttons:X8}" + (accepted ? "" : " · fuente duplicada" );
            WriteLog("sample", source.ToString(), kind, point.X, point.Y, pressure, buttons, accepted);
        };

        var grid = new Grid { Margin = new Thickness(14) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var openLog = new Button { Content = "Abrir carpeta del registro", Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(8, 0, 0, 0) };
        openLog.Click += (_, _) => Process.Start(new ProcessStartInfo(Path.GetDirectoryName(_logPath)!) { UseShellExecute = true });
        DockPanel.SetDock(openLog, Dock.Right);
        header.Children.Add(openLog);
        header.Children.Add(new TextBlock { Text = "Toca, dibuja y levanta el lápiz dentro del área. Prueba las cuatro esquinas y el botón lateral.", VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        Grid.SetRow(header, 0); grid.Children.Add(header);

        _surface.Background = BrushFromRgb(12, 18, 32);
        _surface.BorderBrush = BrushFromRgb(80, 102, 150); _surface.BorderThickness = new Thickness(2);
        _surface.Focusable = true;
        Grid.SetRow(_surface, 1); grid.Children.Add(_surface);
        _status.Text = "Esperando entrada · la prueba comienza al tocar el área";
        _status.Margin = new Thickness(2, 10, 2, 0); _status.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(_status, 2); grid.Children.Add(_status);
        Content = grid;

        Loaded += OnLoaded;
        Activated += (_, _) =>
        {
            WriteLog("activation", "window", "active", 0, 0, 0, 0, true);
            if (IsLoaded) Dispatcher.BeginInvoke(ActivateWintab, System.Windows.Threading.DispatcherPriority.Loaded);
        };
        Deactivated += (_, _) => { _wintab?.Deactivate(); _surface.CancelContact(); WriteLog("activation", "window", "inactive", 0, 0, 0, 0, true); };
        LocationChanged += (_, _) => UpdateMapping();
        SizeChanged += (_, _) => UpdateMapping();
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) _wintab?.Deactivate(); else UpdateMapping(); };
        Closed += (_, _) => { _wintab?.Dispose(); _log?.Dispose(); };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        if (_hwndSource is null) { _status.Text = "No se pudo iniciar WinTab · comprueba también la entrada Windows Ink"; return; }
        _wintab = new WintabWindowSession(hwnd) { ReadCurrentWindowBounds = SurfaceScreenBounds };
        _wintab.SampleReceived += (screenPoint, pressure, tipDown, buttons) =>
        {
            if (NativeWindowBounds.ScreenToElement(_surface, screenPoint) is Point point)
                _surface.HandleWinTab(point, pressure, tipDown, buttons);
        };
        // Packet next to the system cursor and the target area, all in physical pixels: if the cursor follows the
        // packet the driver applies our window mapping; if it does not, the driver ignores lcSys.
        _wintab.RawPacketReceived += (x, y, pressure, buttons, cursor, tipMask) =>
        {
            var systemCursor = NativeWindowBounds.CursorPosition();
            var area = SurfaceScreenBounds();
            var dpi = VisualTreeHelper.GetDpi(this);
            try
            {
                _log?.WriteLine(JsonSerializer.Serialize(new
                {
                    timestamp = DateTimeOffset.Now, category = "raw-packet", source = "WinTab", x, y,
                    cursorX = systemCursor?.X, cursorY = systemCursor?.Y,
                    areaX = area.IsEmpty ? (double?)null : area.X, areaY = area.IsEmpty ? (double?)null : area.Y,
                    areaWidth = area.IsEmpty ? (double?)null : area.Width, areaHeight = area.IsEmpty ? (double?)null : area.Height,
                    packetInsideArea = !area.IsEmpty && area.Contains(x, y),
                    cursorInsideArea = systemCursor is Point c && !area.IsEmpty && area.Contains(c),
                    dpiScale = dpi.DpiScaleX,
                    packetYMirrored = _wintab?.PacketYMirrored, packetYOrientationDecided = _wintab?.PacketYOrientationDecided,
                    rawPressure = pressure, rawButtons = buttons, cursor, tipButtonMask = tipMask, focused = IsActive
                }));
            }
            catch { }
        };
        _wintab.StatusChanged += message => { _status.Text = message; WriteLog("status", "WinTab", message, 0, 0, 0, 0, true); };
        _hwndSource.AddHook(WndProc);
        ActivateWintab();
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_wintab?.ProcessMessage(message, wParam, lParam) == true) handled = true;
        if (message is 0x0003 or 0x0005 or 0x02E0) Dispatcher.BeginInvoke(UpdateMapping, System.Windows.Threading.DispatcherPriority.Loaded);
        return IntPtr.Zero;
    }

    private void ActivateWintab()
    {
        if (_wintab is null || !IsActive || WindowState == WindowState.Minimized) return;
        var bounds = SurfaceScreenBounds();
        if (_wintab.IsActive) _wintab.UpdateBounds(bounds); else _wintab.Activate(bounds);
    }

    private void UpdateMapping()
    {
        if (!IsLoaded || !IsActive || WindowState == WindowState.Minimized) return;
        if (_wintab?.IsActive == true && !_wintab.UpdateBounds(SurfaceScreenBounds()))
            _status.Text = "WinTab no pudo actualizar el área · sigue activa la entrada Windows Ink";
    }

    private Rect SurfaceScreenBounds()
    {
        if (!_surface.IsLoaded) return Rect.Empty;
        return NativeWindowBounds.ElementArea(_surface);
    }

    private void WriteLog(string category, string source, string kind, double x, double y, double pressure, uint buttons, bool accepted)
    {
        try { _log?.WriteLine(JsonSerializer.Serialize(new { timestamp = DateTimeOffset.Now, category, source, @event = kind, x, y, pressure, buttons, accepted, focused = IsActive })); }
        catch { }
    }

    private static SolidColorBrush BrushFromRgb(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

    private sealed class DiagnosticSurface : Border
    {
        private readonly Canvas _ink = new();
        private readonly PointerInputCoordinator _input = new();
        private System.Windows.Shapes.Polyline? _stroke;
        private Point? _last;
        private PointerInputSource _mouseSource;
        private bool _wintabSeen;
        public event Action<PointerInputSource, string, Point, double, uint, bool>? SampleObserved;

        public DiagnosticSurface()
        {
            Child = _ink;
            StylusDown += StylusDownHandler; StylusMove += StylusMoveHandler; StylusUp += StylusUpHandler;
            MouseDown += MouseDownHandler; MouseMove += MouseMoveHandler; MouseUp += MouseUpHandler;
        }

        public void HandleWinTab(Point point, double pressure, bool tipDown, uint buttons)
        {
            _wintabSeen = true;
            Handle(PointerInputSource.WinTab, tipDown ? "down/move" : "up", point, pressure, buttons, tipDown);
        }

        public void CancelContact()
        {
            _input.Cancel(); _stroke = null; _last = null; Stylus.Capture(null); ReleaseMouseCapture();
        }

        private void StylusDownHandler(object sender, StylusDownEventArgs e)
        {
            var samples = e.GetStylusPoints(this);
            if (samples.Count == 0) Handle(PointerInputSource.WpfPen, "down", e.GetPosition(this), .5, 0, true);
            else foreach (StylusPoint sample in samples) Handle(PointerInputSource.WpfPen, "down", new Point(sample.X, sample.Y), sample.PressureFactor, 0, true);
            Stylus.Capture(this); e.Handled = true;
        }

        private void StylusMoveHandler(object sender, StylusEventArgs e)
        {
            var down = !e.StylusDevice.InAir;
            foreach (StylusPoint sample in e.GetStylusPoints(this)) Handle(PointerInputSource.WpfPen, down ? "move" : "hover", new Point(sample.X, sample.Y), sample.PressureFactor, 0, down);
            e.Handled = true;
        }

        private void StylusUpHandler(object sender, StylusEventArgs e)
        {
            Handle(PointerInputSource.WpfPen, "up", e.GetPosition(this), 0, 0, false); Stylus.Capture(null); e.Handled = true;
        }

        private void MouseDownHandler(object sender, MouseButtonEventArgs e)
        {
            _mouseSource = e.StylusDevice?.TabletDevice?.Type == TabletDeviceType.Stylus || PenMouseMessages.CurrentMessageIsFromPen()
                ? PointerInputSource.PromotedPenMouse : PointerInputSource.Mouse;
            Handle(_mouseSource, "down", e.GetPosition(this), .5, (uint)e.ChangedButton, true); CaptureMouse();
        }

        private void MouseMoveHandler(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) Handle(_mouseSource, "move", e.GetPosition(this), .5, 0, true);
        }

        private void MouseUpHandler(object sender, MouseButtonEventArgs e)
        {
            Handle(_mouseSource, "up", e.GetPosition(this), 0, 0, false); ReleaseMouseCapture();
        }

        private void Handle(PointerInputSource source, string kind, Point point, double pressure, uint buttons, bool tipDown)
        {
            // Same rule as the board: once WinTab delivers packets, Windows Ink and pen-made mouse events are only logged.
            var accepted = !(_wintabSeen && source is PointerInputSource.WpfPen or PointerInputSource.PromotedPenMouse)
                && _input.Accept(source, tipDown);
            SampleObserved?.Invoke(source, kind, point, pressure, buttons, accepted);
            if (!accepted) return;
            if (tipDown)
            {
                if (_stroke is null)
                {
                    _stroke = new System.Windows.Shapes.Polyline { Stroke = Brushes.White, StrokeThickness = Math.Max(1.5, 1 + pressure * 3), StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
                    _ink.Children.Add(_stroke);
                }
                if (_last is null || (_last.Value - point).Length > .25) _stroke.Points.Add(point);
                _last = point;
            }
            else { _stroke = null; _last = null; }
        }
    }
}
