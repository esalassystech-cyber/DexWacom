using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Pizarra.Core;

namespace Pizarra.App;

public enum BoardTool { Draw, Select, Connect }

/// <summary>Board renderer and input surface. StylusDown/Move/Up are kept as distinct events so lifted strokes never join.</summary>
public sealed class BoardSurface : FrameworkElement
{
    private readonly DispatcherTimer _saveTimer;
    private readonly BoardRepository _repository = new();
    private readonly StylusButtonPressTracker _buttonPresses = new();
    private readonly Stack<BoardDocument> _undo = new();
    private readonly Stack<BoardDocument> _redo = new();
    private BoardDocument _document = new();
    private CanvasTransform _transform = new(1, 0, 0);
    private readonly ICursorClipper _clipper = new Win32CursorClipper();
    private readonly CursorConfinementController _confinement;
    private readonly PenStrokeBuilder _strokeBuilder = new();
    private readonly PointerInputCoordinator _pointerInput = new();
    private string? _filePath;
    private Guid? _selected;
    private Guid? _activeNoteId;
    private Point? _lastPointer;
    private Point? _lastPenPoint;
    private bool _pointerDragging;
    private bool _isPanning;
    private bool _wintabInputObserved;
    private bool _penTipDown;
    private uint _lastPenButtons;
    private readonly List<Guid> _pendingConnector = [];

    public BoardSurface()
    {
        Focusable = true;
        _confinement = new CursorConfinementController(_clipper);
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); _ = SaveAsync(); };

        StylusDown += OnStylusDown;
        StylusMove += OnStylusMove;
        StylusUp += OnStylusUp;
        StylusButtonDown += OnStylusButtonDown;
        StylusButtonUp += OnStylusButtonUp;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        SizeChanged += (_, _) => UpdateClipBounds();
        PreviewMouseWheel += (_, e) =>
        {
            var point = e.GetPosition(this);
            var oldZoom = _transform.Zoom;
            var nextZoom = Math.Clamp(oldZoom * (e.Delta > 0 ? 1.12 : 1 / 1.12), .25, 4);
            _transform = new CanvasTransform(nextZoom,
                point.X - (point.X - _transform.PanX) * nextZoom / oldZoom,
                point.Y - (point.Y - _transform.PanY) * nextZoom / oldZoom);
            InvalidateVisual();
            e.Handled = true;
        };
        Loaded += (_, _) => UpdateClipBounds();
        Unloaded += (_, _) => ReleaseInput(CursorReleaseReason.Closed);
    }

    public BoardDocument Document => _document;
    public Guid? SelectedElementId => _selected;
    public string? FilePath => _filePath;
    public BoardTool Tool { get; set; } = BoardTool.Draw;
    public event EventHandler? Changed;

    public async Task LoadAsync(BoardDocument document, string? filePath = null)
    {
        await SaveAsync();
        FinishStroke();
        _document = document;
        _filePath = filePath;
        _selected = null;
        _undo.Clear();
        _redo.Clear();
        InvalidateVisual();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void AddNote(string text = "Escribe tu idea aquí")
    {
        var center = ToWorld(new Point(ActualWidth / 2, ActualHeight / 2));
        AddNoteAt(center, text);
    }

    public void AddShape(WorkflowShapeKind kind)
    {
        var center = ToWorld(new Point(ActualWidth / 2, ActualHeight / 2));
        var offset = _document.Elements.Count * 22;
        Add(new WorkflowShape
        {
            Kind = kind,
            Label = kind == WorkflowShapeKind.Process ? "Nuevo proceso" : "¿Decisión?",
            X = center.X - 95 + offset,
            Y = center.Y - 52 + offset,
            Width = 190,
            Height = 105
        });
    }

    public void Undo()
    {
        FinishStroke();
        if (_undo.Count == 0) return;
        _redo.Push(Clone(_document));
        _document = _undo.Pop();
        RefreshAfterEdit();
    }

    public void Redo()
    {
        FinishStroke();
        if (_redo.Count == 0) return;
        _undo.Push(Clone(_document));
        _document = _redo.Pop();
        RefreshAfterEdit();
    }

    public void DeleteSelected()
    {
        if (_selected is not Guid id) return;
        FinishStroke();
        Checkpoint();
        _document = _document with
        {
            Elements = _document.Elements.Where(x => x.Id != id).ToList(),
            Connectors = _document.Connectors.Where(x => x.FromElementId != id && x.ToElementId != id).ToList()
        };
        _selected = null;
        RefreshAfterEdit();
    }

    public void EditSelected(string text)
    {
        if (_selected is not Guid id) return;
        Checkpoint();
        _document = _document with
        {
            Elements = _document.Elements.Select(x => x.Id != id ? x : x switch
            {
                StickyNote note => note with { Text = text },
                WorkflowShape shape => shape with { Label = text },
                _ => x
            }).ToList()
        };
        RefreshAfterEdit();
    }

    public void SetBoardViewActive(bool active)
    {
        if (!active) ReleaseInput(CursorReleaseReason.Deactivated);
    }

    public void SetMappedInputActive(bool active)
    {
        if (!active) ReleaseInput(CursorReleaseReason.TabletDisconnected);
        if (!active) _wintabInputObserved = false;
    }

    public void SetWintabInputObserved(bool observed) => _wintabInputObserved = observed;

    public void ActivateConfinement(bool active)
    {
        if (!active) ReleaseInput(CursorReleaseReason.Deactivated);
    }

    public void ReleaseInput(CursorReleaseReason reason)
    {
        FinishStroke();
        _buttonPresses.Clear();
        _pointerDragging = false;
        _isPanning = false;
        _penTipDown = false;
        _lastPenButtons = 0;
        _lastPenPoint = null;
        _pointerInput.Cancel();
        _lastPointer = null;
        Stylus.Capture(null);
        ReleaseMouseCapture();
        _confinement.Release(reason);
    }

    public async Task SaveAsync()
    {
        FinishStroke();
        _saveTimer.Stop();
        if (string.IsNullOrWhiteSpace(_filePath)) return;
        try { await _repository.SaveAsync(_document, _filePath); }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), $"No se pudo guardar el tablero.\n\n{ex.Message}", "Error al guardar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(14, 21, 38)), null, new Rect(0, 0, ActualWidth, ActualHeight));
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(31, 43, 67)), 1);
        for (double x = 0; x < ActualWidth; x += 28) dc.DrawLine(gridPen, new Point(x, 0), new Point(x, ActualHeight));
        for (double y = 0; y < ActualHeight; y += 28) dc.DrawLine(gridPen, new Point(0, y), new Point(ActualWidth, y));

        dc.PushTransform(WorldTransform());
        foreach (var stroke in _document.FreehandStrokes) DrawFreeStroke(dc, stroke);
        if (_activeNoteId is null && _strokeBuilder.IsActive) DrawFreeStroke(dc, new InkStroke { Points = [.. _strokeBuilder.Points] });

        foreach (var connector in _document.Connectors)
        {
            var from = _document.Elements.FirstOrDefault(x => x.Id == connector.FromElementId);
            var to = _document.Elements.FirstOrDefault(x => x.Id == connector.ToElementId);
            if (from is null || to is null) continue;
            var arrowPen = new Pen(new SolidColorBrush(Color.FromRgb(139, 156, 255)), 2.5) { EndLineCap = PenLineCap.Triangle };
            dc.DrawLine(arrowPen, Center(from), Center(to));
        }

        foreach (var element in _document.Elements)
        {
            var rect = new Rect(element.X, element.Y, element.Width, element.Height);
            var selected = element.Id == _selected;
            if (element is StickyNote note)
            {
                dc.PushClip(new RectangleGeometry(rect));
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(248, 216, 113)),
                    new Pen(selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(222, 184, 84)), selected ? 2.5 : 1), rect, 8, 8);
                DrawText(dc, note.Title, 13, Color.FromRgb(77, 62, 28), new Point(rect.X + 12, rect.Y + 10), true);
                DrawText(dc, note.Text, 14, Color.FromRgb(52, 46, 35), new Point(rect.X + 12, rect.Y + 37), false, rect.Width - 24, rect.Height - 48);
                foreach (var stroke in note.Strokes) DrawNoteStroke(dc, stroke, rect);
                if (_activeNoteId == note.Id && _strokeBuilder.IsActive)
                    DrawNoteStroke(dc, new InkStroke { Points = [.. _strokeBuilder.Points] }, rect);
                dc.Pop();
            }
            else if (element is WorkflowShape shape)
            {
                var fill = new SolidColorBrush(Color.FromRgb(33, 47, 75));
                var outline = new Pen(selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(129, 148, 197)), selected ? 2.5 : 1.5);
                if (shape.Kind == WorkflowShapeKind.Decision)
                {
                    var geometry = new StreamGeometry();
                    using (var context = geometry.Open())
                    {
                        context.BeginFigure(new Point(rect.X + rect.Width / 2, rect.Y), true, true);
                        context.LineTo(new Point(rect.Right, rect.Y + rect.Height / 2), true, false);
                        context.LineTo(new Point(rect.X + rect.Width / 2, rect.Bottom), true, false);
                        context.LineTo(new Point(rect.X, rect.Y + rect.Height / 2), true, false);
                    }
                    geometry.Freeze();
                    dc.DrawGeometry(fill, outline, geometry);
                }
                else dc.DrawRoundedRectangle(fill, outline, rect, 12, 12);
                DrawText(dc, shape.Label, 14, Colors.White, new Point(rect.X + 10, rect.Y + rect.Height / 2 - 12), true, rect.Width - 20, 50, TextAlignment.Center);
            }
        }
        dc.Pop();
        dc.Pop();
    }

    private void OnStylusDown(object sender, StylusDownEventArgs e)
    {
        var samples = e.GetStylusPoints(this);
        var accepted = false;
        if (samples.Count == 0)
            accepted = HandlePointerSample(e.GetPosition(this), .5, tipDown: true, buttons: 0, PointerInputSource.WpfPen);
        else
        {
            for (var i = 0; i < samples.Count; i++)
                accepted |= HandlePointerSample(new Point(samples[i].X, samples[i].Y), samples[i].PressureFactor, tipDown: true, buttons: 0, PointerInputSource.WpfPen);
        }
        if (accepted) Stylus.Capture(this, CaptureMode.Element);
        e.Handled = true;
        InvalidateVisual();
    }

    private void OnStylusMove(object sender, StylusEventArgs e)
    {
        var samples = e.GetStylusPoints(this);
        foreach (StylusPoint sample in samples)
            HandlePointerSample(new Point(sample.X, sample.Y), sample.PressureFactor, tipDown: !e.StylusDevice.InAir, buttons: 0, PointerInputSource.WpfPen);
        e.Handled = true;
        InvalidateVisual();
    }

    private void OnStylusUp(object sender, StylusEventArgs e)
    {
        HandlePointerSample(e.GetPosition(this), 0, tipDown: false, buttons: 0, PointerInputSource.WpfPen);
        Stylus.Capture(null);
        e.Handled = true;
    }

    private void OnStylusButtonDown(object sender, StylusButtonEventArgs e)
    {
        if (_wintabInputObserved) { e.Handled = true; return; }
        if (string.IsNullOrWhiteSpace(_filePath) || e.StylusButton.Guid == StylusPointProperties.TipButton.Id || !_buttonPresses.TryPress(e.StylusButton.Guid)) return;
        var point = e.GetPosition(this);
        if (ContainsSurfacePoint(point)) AddNoteAt(ToWorld(point), "");
        e.Handled = true;
    }

    private void OnStylusButtonUp(object sender, StylusButtonEventArgs e)
    {
        _buttonPresses.Release(e.StylusButton.Guid);
        e.Handled = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_filePath)) return;
        Focus();
        var screenPoint = e.GetPosition(this);
        _lastPointer = screenPoint;

        if (e.ChangedButton is MouseButton.Middle or MouseButton.Right)
        {
            _isPanning = true;
            CaptureMouse();
            return;
        }

        if (HandlePointerSample(screenPoint, .55, tipDown: true, buttons: 0, GetMouseSource(e))) CaptureMouse();
        InvalidateVisual();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning && _lastPointer is not null &&
            (e.MiddleButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed))
        {
            var p = e.GetPosition(this);
            _transform = new CanvasTransform(_transform.Zoom, _transform.PanX + p.X - _lastPointer!.Value.X, _transform.PanY + p.Y - _lastPointer.Value.Y);
            _lastPointer = p;
            InvalidateVisual();
            return;
        }
        if (e.LeftButton == MouseButtonState.Pressed && (_strokeBuilder.IsActive || _pointerDragging))
            HandlePointerSample(e.GetPosition(this), .55, tipDown: true, buttons: 0, GetMouseSequenceSource(e));
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning) { _isPanning = false; _lastPointer = null; ReleaseMouseCapture(); return; }
        if (_penTipDown) HandlePointerSample(e.GetPosition(this), 0, tipDown: false, buttons: 0, GetMouseSequenceSource(e));
        else FinishStroke();
        ReleaseMouseCapture();
    }

    private void BeginStroke(Point world, double pressure)
    {
        FinishStroke();
        Checkpoint();
        var note = HitNote(world);
        _activeNoteId = note?.Id;
        if (note is not null) _selected = note.Id;
        _strokeBuilder.Begin(ToStrokePoint(world, pressure, note));
        InvalidateVisual();
    }

    /// <summary>Single pointer path used by mapped WinTab packets and simulated pen input.</summary>
    public void HandlePenSample(Point surfacePoint, double pressure, bool tipDown, uint buttons)
        => HandlePointerSample(surfacePoint, pressure, tipDown, buttons, PointerInputSource.WinTab);

    /// <summary>Shared down/move/up path for native pen, mapped WinTab, mouse, and input tests.</summary>
    public bool HandlePointerSample(Point surfacePoint, double pressure, bool tipDown, uint buttons,
        PointerInputSource source = PointerInputSource.Mouse)
    {
        if (string.IsNullOrWhiteSpace(_filePath)) return false;
        _lastPenPoint = surfacePoint;
        var inside = ContainsSurfacePoint(surfacePoint);
        var point = ClampToSurface(surfacePoint);
        var sideButtonDown = (buttons & ~1U & ~_lastPenButtons) != 0;
        if (inside && sideButtonDown) AddNoteAt(ToWorld(point), "");
        var accepted = _pointerInput.Accept(source, tipDown);
        _lastPenButtons = buttons;
        if (!accepted) return false;

        if (tipDown && !_penTipDown)
        {
            if (inside)
            {
                Focus();
                var world = ToWorld(point);
                if (Tool == BoardTool.Draw) BeginStroke(world, pressure);
                else if (Tool == BoardTool.Select) SelectAndStartMove(world);
                else if (Tool == BoardTool.Connect) ConnectAt(world);
            }
        }
        else if (tipDown && _penTipDown)
        {
            if (Tool == BoardTool.Draw && _strokeBuilder.IsActive)
                _strokeBuilder.Append(ToStrokePoint(ToWorld(point), pressure, ActiveNote()));
            else if (Tool == BoardTool.Select && _pointerDragging) MovePointer(point);
            _saveTimer.Stop(); _saveTimer.Start();
        }
        else if (!tipDown && _penTipDown)
        {
            if (Tool == BoardTool.Draw && _strokeBuilder.IsActive)
                _strokeBuilder.Append(ToStrokePoint(ToWorld(point), 0, ActiveNote()));
            else if (Tool == BoardTool.Select && _pointerDragging) MovePointer(point);
            FinishStroke();
            _pointerDragging = false;
            _lastPointer = null;
        }

        _penTipDown = tipDown;
        InvalidateVisual();
        return true;
    }

    public void ReleaseMappedPen()
    {
        if (_penTipDown && _lastPenPoint is Point point)
            HandlePointerSample(point, 0, tipDown: false, buttons: 0, PointerInputSource.WinTab);
        else
        {
            FinishStroke();
            _pointerDragging = false;
            _penTipDown = false;
            _lastPenButtons = 0;
            _lastPenPoint = null;
        }
    }

    private static PointerInputSource GetMouseSource(MouseEventArgs e) =>
        e.StylusDevice?.TabletDevice?.Type == TabletDeviceType.Stylus
            ? PointerInputSource.PromotedPenMouse
            : PointerInputSource.Mouse;

    private PointerInputSource GetMouseSequenceSource(MouseEventArgs e)
    {
        var source = GetMouseSource(e);
        return source == PointerInputSource.Mouse && _pointerInput.ContactSource == PointerInputSource.PromotedPenMouse
            ? PointerInputSource.PromotedPenMouse
            : source;
    }

    private InkPoint ToStrokePoint(Point world, double pressure, StickyNote? note = null)
    {
        var x = world.X;
        var y = world.Y;
        if (note is not null)
        {
            x = (world.X - note.X) / note.Width;
            y = (world.Y - note.Y) / note.Height;
        }
        return new InkPoint(x, y, Math.Clamp(pressure, 0, 1));
    }

    private void FinishStroke()
    {
        if (!_strokeBuilder.IsActive) { _activeNoteId = null; return; }
        var stroke = _strokeBuilder.End();
        if (_activeNoteId is Guid noteId)
        {
            _document = BoardInkOperations.AppendStroke(_document, stroke, noteId);
        }
        else _document = BoardInkOperations.AppendStroke(_document, stroke, null);
        _activeNoteId = null;
        Changed?.Invoke(this, EventArgs.Empty);
        _saveTimer.Stop(); _saveTimer.Start();
        InvalidateVisual();
    }

    private StickyNote? ActiveNote() => _activeNoteId is Guid id ? _document.Elements.OfType<StickyNote>().FirstOrDefault(n => n.Id == id) : null;
    private StickyNote? HitNote(Point point) => _document.Elements.OfType<StickyNote>().LastOrDefault(note => new Rect(note.X, note.Y, note.Width, note.Height).Contains(point));

    private void AddNoteAt(Point world, string text)
    {
        var note = new StickyNote
        {
            Title = "Nota adhesiva",
            Text = text,
            X = world.X - 125,
            Y = world.Y - 95,
            Width = 250,
            Height = 190
        };
        Add(note);
    }

    private void SelectAndStartMove(Point world)
    {
        var hit = Hit(world);
        _selected = hit?.Id;
        _pointerDragging = hit is not null;
        if (_pointerDragging) Checkpoint();
        _lastPointer = new Point(world.X * _transform.Zoom + _transform.PanX, world.Y * _transform.Zoom + _transform.PanY);
    }

    private void MovePointer(Point screenPoint)
    {
        if (_lastPointer is not Point last) return;
        var dx = (screenPoint.X - last.X) / _transform.Zoom;
        var dy = (screenPoint.Y - last.Y) / _transform.Zoom;
        if (_selected is Guid id)
        {
            _document = _document with
            {
                Elements = _document.Elements.Select(x => x.Id == id ? x with { X = x.X + dx, Y = x.Y + dy } : x).ToList()
            };
        }
        _lastPointer = screenPoint;
        InvalidateVisual();
        _saveTimer.Stop(); _saveTimer.Start();
    }

    private void ConnectAt(Point world)
    {
        var hit = Hit(world);
        if (hit is null) return;
        _pendingConnector.Add(hit.Id);
        if (_pendingConnector.Count == 2)
        {
            Checkpoint();
            _document = _document with
            {
                Connectors = [.. _document.Connectors, new WorkflowConnector { FromElementId = _pendingConnector[0], ToElementId = _pendingConnector[1] }]
            };
            _pendingConnector.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
            _saveTimer.Stop(); _saveTimer.Start();
        }
        InvalidateVisual();
    }

    private BoardElement? Hit(Point point) => _document.Elements.LastOrDefault(el => new Rect(el.X, el.Y, el.Width, el.Height).Contains(point));

    private void Add(BoardElement element)
    {
        Checkpoint();
        _document = _document with { Elements = [.. _document.Elements, element] };
        _selected = element.Id;
        RefreshAfterEdit();
    }

    private void Checkpoint() { _undo.Push(Clone(_document)); _redo.Clear(); }
    private static BoardDocument Clone(BoardDocument value) => System.Text.Json.JsonSerializer.Deserialize<BoardDocument>(System.Text.Json.JsonSerializer.Serialize(value))!;
    private void RefreshAfterEdit() { Changed?.Invoke(this, EventArgs.Empty); InvalidateVisual(); _saveTimer.Stop(); _saveTimer.Start(); }
    private Point ToWorld(Point point) => new((point.X - _transform.PanX) / _transform.Zoom, (point.Y - _transform.PanY) / _transform.Zoom);
    private MatrixTransform WorldTransform() => new(new Matrix(_transform.Zoom, 0, 0, _transform.Zoom, _transform.PanX, _transform.PanY));
    private bool ContainsSurfacePoint(Point point) => point.X >= 0 && point.Y >= 0 && point.X <= ActualWidth && point.Y <= ActualHeight;
    private Point ClampToSurface(Point point) => new(Math.Clamp(point.X, 0, Math.Max(0, ActualWidth)), Math.Clamp(point.Y, 0, Math.Max(0, ActualHeight)));
    private static Point Center(BoardElement element) => new(element.X + element.Width / 2, element.Y + element.Height / 2);

    private static void DrawText(DrawingContext dc, string text, double size, Color color, Point point, bool bold = false, double width = 700, double height = 100, TextAlignment alignment = TextAlignment.Left)
    {
        var formatted = new FormattedText(text ?? "", System.Globalization.CultureInfo.GetCultureInfo("es"), FlowDirection.LeftToRight,
            new Typeface(bold ? "Segoe UI Semibold" : "Segoe UI"), size, new SolidColorBrush(color),
            VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, width), MaxTextHeight = Math.Max(1, height), TextAlignment = alignment, Trimming = TextTrimming.CharacterEllipsis
        };
        dc.DrawText(formatted, point);
    }

    private static void DrawNoteStroke(DrawingContext dc, InkStroke stroke, Rect bounds)
    {
        DrawStrokeLines(dc, stroke, point => new Point(bounds.X + point.X * bounds.Width, bounds.Y + point.Y * bounds.Height), Brushes.Black);
    }

    private static void DrawFreeStroke(DrawingContext dc, InkStroke stroke) => DrawStrokeLines(dc, stroke,
        point => new Point(point.X, point.Y), new SolidColorBrush(Color.FromRgb(232, 237, 247)));

    private static void DrawStrokeLines(DrawingContext dc, InkStroke stroke, Func<InkPoint, Point> map, Brush? brush = null)
    {
        brush ??= Brushes.Black;
        for (var i = 1; i < stroke.Points.Count; i++)
        {
            var current = stroke.Points[i];
            var pen = new Pen(brush, 1.2 + current.Pressure * 2.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            dc.DrawLine(pen, map(stroke.Points[i - 1]), map(current));
        }
        if (stroke.Points.Count == 1)
        {
            var point = map(stroke.Points[0]);
            dc.DrawEllipse(brush, null, point, 1.2, 1.2);
        }
    }

    private RectD WindowScreenBounds()
    {
        var window = Window.GetWindow(this);
        if (window is null) return new RectD(0, 0, 0, 0);
        var origin = window.PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(window);
        return new RectD(origin.X, origin.Y, window.ActualWidth * dpi.DpiScaleX, window.ActualHeight * dpi.DpiScaleY);
    }

    private void UpdateClipBounds() { if (_confinement.IsConfined) _confinement.UpdateBoardBounds(WindowScreenBounds()); }
}
