using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using Microsoft.Win32;
using Pizarra.Core;

namespace Pizarra.App;

public partial class MainWindow : Window
{
    private readonly string _library = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Wacom Workboard");
    private bool _fullScreen;
    private WindowState _previousState;
    private WindowStyle _previousStyle;
    private string? _selectedProject;
    private WintabWindowSession? _wintab;
    private HwndSource? _hwndSource;
    private bool _bindingSuspended;

    public MainWindow()
    {
        InitializeComponent();
        UpdateToolButtons();
        Directory.CreateDirectory(_library);
        Board.Changed += (_, _) => { BoardTitle.Text = Board.Document.Name; StatusText.Text = "Cambios guardados automáticamente"; };
        SourceInitialized += (_, _) => InitializeWintab();
        Loaded += async (_, _) => await RefreshLibraryAsync();
        Loaded += (_, _) => { if (!_bindingSuspended) ActivateWintab(); };
        Activated += (_, _) => { Board.ActivateConfinement(_fullScreen); if (!_bindingSuspended) ActivateWintab(); };
        Deactivated += (_, _) => { DeactivateWintab(); Board.ActivateConfinement(false); };
        LocationChanged += (_, _) => UpdateWintabBounds();
        SizeChanged += (_, _) => UpdateWintabBounds();
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) { DeactivateWintab(); Board.ActivateConfinement(false); } };
        PreviewKeyDown += async (_, e) =>
        {
            if (e.Key == Key.Escape) { _bindingSuspended = true; DeactivateWintab(); Board.ActivateConfinement(false); WacomStatusText.Text = "Vinculación suspendida · pulsa Vincular Wacom para reanudar"; e.Handled = true; return; }
            var command = BoardShortcutResolver.Resolve(e.Key.ToString(),
                control: (Keyboard.Modifiers & ModifierKeys.Control) != 0,
                textInputFocused: Keyboard.FocusedElement is TextBoxBase);
            switch (command)
            {
                case BoardShortcutCommand.Save: await Board.SaveAsync(); e.Handled = true; break;
                case BoardShortcutCommand.Undo: Board.Undo(); e.Handled = true; break;
                case BoardShortcutCommand.Redo: Board.Redo(); e.Handled = true; break;
                case BoardShortcutCommand.Delete: Board.DeleteSelected(); e.Handled = true; break;
            }
        };
        Closed += async (_, _) => { DeactivateWintab(); _wintab?.Dispose(); Board.ActivateConfinement(false); await Board.SaveAsync(); };
    }

    private async Task RefreshLibraryAsync()
    {
        BoardList.Items.Clear();
        foreach (var dir in Directory.GetDirectories(_library).OrderBy(x => x))
        {
            BoardList.Items.Add(new LibraryEntry("📁  " + Path.GetFileName(dir), null, dir));
            foreach (var file in Directory.GetFiles(dir, "*.flowboard").OrderBy(x => x))
            {
                try { var doc = await new BoardRepository().LoadAsync(file); BoardList.Items.Add(new LibraryEntry("     ▫  " + doc.Name, file, dir)); }
                catch { BoardList.Items.Add(new LibraryEntry("     ⚠  " + Path.GetFileNameWithoutExtension(file) + " (no se pudo abrir)", file, dir)); }
            }
        }
    }

    private async void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        var name = Prompt("Nuevo proyecto", "Nombre del proyecto:"); if (string.IsNullOrWhiteSpace(name)) return;
        await Board.SaveAsync();
        var dir = Path.Combine(_library, SafeName(name)); Directory.CreateDirectory(dir);
        var doc = new BoardDocument { Name = "Primer tablero" };
        var path = Path.Combine(dir, SafeName(doc.Name) + ".flowboard"); await new BoardRepository().SaveAsync(doc, path); await Board.LoadAsync(doc, path); BoardTitle.Text = doc.Name;
        await RefreshLibraryAsync();
    }

    private async void BoardList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BoardList.SelectedItem is not LibraryEntry entry) return;
        _selectedProject = entry.ProjectPath;
        if (entry.Path is null) return;
        try { await Board.SaveAsync(); var doc = await new BoardRepository().LoadAsync(entry.Path); await Board.LoadAsync(doc, entry.Path); BoardTitle.Text = doc.Name; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "No se pudo abrir el tablero", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void NewBoard_Click(object sender, RoutedEventArgs e)
    {
        var project = _selectedProject ?? Directory.GetDirectories(_library).FirstOrDefault();
        if (project is null) { MessageBox.Show(this, "Crea primero un proyecto.", "Biblioteca"); return; }
        var name = Prompt("Nuevo tablero", "Nombre del tablero:", "Nuevo tablero"); if (string.IsNullOrWhiteSpace(name)) return;
        await Board.SaveAsync();
        var doc = new BoardDocument { Name = name.Trim() };
        var path = Path.Combine(project, SafeName(doc.Name) + ".flowboard");
        await new BoardRepository().SaveAsync(doc, path); await Board.LoadAsync(doc, path); BoardTitle.Text = doc.Name;
        await RefreshLibraryAsync();
    }

    private async void AddNote_Click(object sender, RoutedEventArgs e)
    {
        if (Board.FilePath is null) { MessageBox.Show(this, "Crea primero un proyecto para guardar este tablero.", "Biblioteca"); return; }
        var text = Prompt("Nueva nota adhesiva", "Escribe una idea:") ?? "";
        Board.AddNote(text); await Board.SaveAsync();
    }
    private async void AddProcess_Click(object sender, RoutedEventArgs e) { Board.AddShape(WorkflowShapeKind.Process); await Board.SaveAsync(); }
    private async void AddDecision_Click(object sender, RoutedEventArgs e) { Board.AddShape(WorkflowShapeKind.Decision); await Board.SaveAsync(); }
    private void Connect_Click(object sender, RoutedEventArgs e) => SetTool(BoardTool.Connect, "Conectar: selecciona dos bloques por orden");
    private void Select_Click(object sender, RoutedEventArgs e) => SetTool(BoardTool.Select, "Seleccionar y mover bloques");
    private void Draw_Click(object sender, RoutedEventArgs e) => SetTool(BoardTool.Draw, "Dibujar tinta libre o dentro de una nota");
    private void SetTool(BoardTool tool, string status) { Board.Tool = tool; StatusText.Text = status; UpdateToolButtons(); }
    private void UpdateToolButtons()
    {
        var active = new System.Windows.Media.SolidColorBrush(Color.FromRgb(76, 94, 177));
        var idle = new System.Windows.Media.SolidColorBrush(Color.FromRgb(27, 37, 64));
        if (DrawToolButton is not null) DrawToolButton.Background = Board.Tool == BoardTool.Draw ? active : idle;
        if (SelectToolButton is not null) SelectToolButton.Background = Board.Tool == BoardTool.Select ? active : idle;
        if (ConnectToolButton is not null) ConnectToolButton.Background = Board.Tool == BoardTool.Connect ? active : idle;
    }
    private void Edit_Click(object sender, RoutedEventArgs e) => EditSelection();
    private void Delete_Click(object sender, RoutedEventArgs e) => Board.DeleteSelected();
    private void EditSelection()
    {
        var element = Board.Document.Elements.FirstOrDefault(x => x.Id == Board.SelectedElementId);
        if (element is null) return;
        var value = element is StickyNote note ? note.Text : ((WorkflowShape)element).Label;
        var edited = Prompt("Editar bloque", "Texto del bloque:", value);
        if (edited is not null) Board.EditSelected(edited);
    }
    private void Fullscreen_Click(object sender, RoutedEventArgs e) { if (_fullScreen) ExitFullscreen(); else EnterFullscreen(); }
    private void LinkWacom_Click(object sender, RoutedEventArgs e)
    {
        _bindingSuspended = false;
        ActivateWintab();
    }

    private void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        var diagnostics = new WacomDiagnosticsWindow { Owner = this };
        diagnostics.ShowDialog();
        if (!_bindingSuspended && IsActive) ActivateWintab();
    }

    private void InitializeWintab()
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(handle);
        if (_hwndSource is null) { StatusText.Text = "No se pudo preparar la entrada Wacom · WPF y mouse activos"; return; }
        _wintab = new WintabWindowSession(handle) { ReadCurrentWindowBounds = WindowScreenBounds };
        _wintab.SampleReceived += (screenPoint, pressure, tipDown, buttons) =>
        {
            if (WindowState == WindowState.Minimized || !IsActive) return;
            Board.SetWintabInputObserved(true);
            Board.HandlePointerSample(Board.PointFromScreen(screenPoint), pressure, tipDown, buttons, PointerInputSource.WinTab);
        };
        _wintab.ProximityChanged += inProximity => { if (!inProximity) Board.ReleaseMappedPen(); };
        _wintab.StatusChanged += message => { WacomStatusText.Text = message; Board.SetMappedInputActive(_wintab.IsActive); };
        _hwndSource.AddHook(WndProc);
        if (IsActive && !_bindingSuspended) ActivateWintab();
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_wintab?.ProcessMessage(message, wParam, lParam) == true)
        {
            handled = true;
            return IntPtr.Zero;
        }
        if (message == 0x0003 || message == 0x0005 || message == 0x02E0)
            Dispatcher.BeginInvoke(UpdateWintabBounds, System.Windows.Threading.DispatcherPriority.Loaded);
        return IntPtr.Zero;
    }

    private void ActivateWintab()
    {
        if (_wintab is null || !IsActive || WindowState == WindowState.Minimized || _bindingSuspended) return;
        var bounds = WindowScreenBounds();
        if (_wintab.IsActive)
        {
            var stillActive = _wintab.UpdateBounds(bounds);
            Board.SetMappedInputActive(stillActive);
            return;
        }
        var active = _wintab.Activate(bounds);
        Board.SetMappedInputActive(active);
    }

    private void DeactivateWintab()
    {
        _wintab?.Deactivate();
        Board.SetMappedInputActive(false);
        if (!_bindingSuspended) WacomStatusText.Text = "Wacom liberada · se vinculará al volver a la app";
    }

    private void UpdateWintabBounds()
    {
        if (_wintab is null || !IsActive || WindowState == WindowState.Minimized) return;
        if (!_wintab.UpdateBounds(WindowScreenBounds()) && !_bindingSuspended) Board.SetMappedInputActive(false);
    }

    private Rect WindowScreenBounds()
    {
        if (!IsInitialized || WindowState == WindowState.Minimized) return Rect.Empty;
        var origin = PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(this);
        return new Rect(origin.X, origin.Y, ActualWidth * dpi.DpiScaleX, ActualHeight * dpi.DpiScaleY);
    }

    private void EnterFullscreen()
    {
        if (_fullScreen) return;
        _previousState = WindowState; _previousStyle = WindowStyle;
        WindowStyle = WindowStyle.None; WindowState = WindowState.Maximized;
        LibraryPane.Visibility = Visibility.Collapsed; WorkspaceGrid.ColumnDefinitions[0].Width = new GridLength(0);
        Grid.SetColumn(BoardHost, 0); Board.Margin = new Thickness(0); _fullScreen = true; Board.SetBoardViewActive(true); Board.ActivateConfinement(true);
    }
    private void ExitFullscreen()
    {
        Board.SetBoardViewActive(false); Board.ActivateConfinement(false);
        LibraryPane.Visibility = Visibility.Visible; WorkspaceGrid.ColumnDefinitions[0].Width = new GridLength(250);
        Grid.SetColumn(BoardHost, 1); Board.Margin = new Thickness(18); WindowStyle = _previousStyle; WindowState = _previousState; _fullScreen = false;
    }

    private async void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Imagen PNG|*.png", FileName = SafeName(Board.Document.Name) + ".png" };
        if (dialog.ShowDialog(this) != true) return;
        Board.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(Board);
        var bitmap = new RenderTargetBitmap((int)(Board.ActualWidth * dpi.DpiScaleX), (int)(Board.ActualHeight * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(Board);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using var stream = File.Create(dialog.FileName); encoder.Save(stream);
        StatusText.Text = "PNG exportado";
    }

    private async void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Documento PDF|*.pdf", FileName = SafeName(Board.Document.Name) + ".pdf" };
        if (dialog.ShowDialog(this) != true) return;
        Board.UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(Board);
        var bitmap = new RenderTargetBitmap((int)(Board.ActualWidth * dpi.DpiScaleX), (int)(Board.ActualHeight * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bitmap.Render(Board);
        var jpeg = new JpegBitmapEncoder { QualityLevel = 90 }; jpeg.Frames.Add(BitmapFrame.Create(bitmap));
        await using var imageStream = new MemoryStream(); jpeg.Save(imageStream);
        await File.WriteAllBytesAsync(dialog.FileName, CreateImagePdf(imageStream.ToArray(), bitmap.PixelWidth, bitmap.PixelHeight));
        StatusText.Text = "PDF exportado";
    }

    private static byte[] CreateImagePdf(byte[] jpeg, int pixelWidth, int pixelHeight)
    {
        using var stream = new MemoryStream();
        var offsets = new List<long> { 0 };
        void Write(string value) { var bytes = System.Text.Encoding.Latin1.GetBytes(value); stream.Write(bytes); }
        void Object(int id, string body) { offsets.Add(stream.Position); Write($"{id} 0 obj\n{body}\nendobj\n"); }
        Write("%PDF-1.4\n%âãÏÓ\n");
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Object(3, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pixelWidth} {pixelHeight}] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>");
        offsets.Add(stream.Position); Write($"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {pixelWidth} /Height {pixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {jpeg.Length} >>\nstream\n"); stream.Write(jpeg); Write("\nendstream\nendobj\n");
        var content = System.Text.Encoding.ASCII.GetBytes($"q\n{pixelWidth} 0 0 {pixelHeight} 0 0 cm\n/Im0 Do\nQ\n");
        offsets.Add(stream.Position); Write($"5 0 obj\n<< /Length {content.Length} >>\nstream\n"); stream.Write(content); Write("endstream\nendobj\n");
        var xref = stream.Position; Write("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) Write($"{offset:0000000000} 00000 n \n");
        Write($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return stream.ToArray();
    }

    private static string SafeName(string name)
    { foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-'); return string.IsNullOrWhiteSpace(name) ? "Tablero" : name.Trim(); }

    private string? Prompt(string title, string label, string initial = "")
    {
        var window = new Window { Owner = this, Title = title, Width = 380, Height = 170, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = System.Windows.Media.Brushes.White };
        var grid = new Grid { Margin = new Thickness(16) }; grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var box = new TextBox { Margin = new Thickness(0, 8, 0, 12), Padding = new Thickness(8), Text = initial }; Grid.SetRow(box, 1);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "Crear", Padding = new Thickness(14, 6, 14, 6), IsDefault = true }; var cancel = new Button { Content = "Cancelar", Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(14, 6, 14, 6), IsCancel = true };
        string? result = null; ok.Click += (_, _) => { result = box.Text; window.DialogResult = true; }; buttons.Children.Add(ok); buttons.Children.Add(cancel); Grid.SetRow(buttons, 2);
        grid.Children.Add(new TextBlock { Text = label, Foreground = System.Windows.Media.Brushes.Black }); grid.Children.Add(box); grid.Children.Add(buttons); window.Content = grid; window.Loaded += (_, _) => box.Focus(); window.ShowDialog(); return result;
    }

    private sealed record LibraryEntry(string Label, string? Path, string ProjectPath) { public override string ToString() => Label; }
}
