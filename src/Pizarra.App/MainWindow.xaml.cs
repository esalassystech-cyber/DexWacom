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
    private StreamWriter? _inputLog;
    private bool _refreshingLibrary;
    private bool _deleteArmed;
    private readonly System.Windows.Threading.DispatcherTimer _deleteTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private Action<string>? _inlinePromptAccept;

    public MainWindow()
    {
        InitializeComponent();
        UpdateToolButtons();
        _deleteTimer.Tick += (_, _) => ResetDeleteConfirmation();
        Directory.CreateDirectory(_library);
        OpenInputLog();
        Activated += (_, _) => WriteInputLog(new { window = "activa", hasBoard = Board.FilePath is not null });
        Deactivated += (_, _) => WriteInputLog(new { window = "inactiva" });
        Board.SampleTraced += (source, point, tipDown, accepted, outcome) =>
            WriteInputLog(new { source = source.ToString(), x = point.X, y = point.Y, tipDown, accepted, outcome, tool = Board.Tool.ToString(), boardWidth = Board.ActualWidth, boardHeight = Board.ActualHeight });
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
            if (e.Key == Key.Escape && InlinePrompt.Visibility == Visibility.Visible) { CloseInlinePrompt(accept: false); e.Handled = true; return; }
            // Esc in a text box only cancels the typing; releasing the pen there was easy to trigger by accident.
            if (e.Key == Key.Escape && Keyboard.FocusedElement is TextBoxBase)
            {
                if (BoardList.SelectedItem is LibraryEntry entry) RenameBox.Text = entry.Name;
                Board.Focus();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape) { SuspendBinding(); e.Handled = true; return; }
            // Holding Space turns the pen into the hand (assignable to a tablet ExpressKey).
            if (e.Key == Key.Space && Keyboard.FocusedElement is not TextBoxBase)
            {
                if (!e.IsRepeat) { Board.TemporaryPan = true; Board.Cursor = Cursors.SizeAll; }
                e.Handled = true;
                return;
            }
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
        PreviewKeyUp += (_, e) =>
        {
            if (e.Key != Key.Space || !Board.TemporaryPan) return;
            Board.TemporaryPan = false;
            Board.Cursor = Board.Tool == BoardTool.Pan ? Cursors.SizeAll : null;
        };
        Deactivated += (_, _) => { if (Board.TemporaryPan) { Board.TemporaryPan = false; Board.Cursor = Board.Tool == BoardTool.Pan ? Cursors.SizeAll : null; } };
        Closed += async (_, _) => { DeactivateWintab(); _wintab?.Dispose(); Board.ActivateConfinement(false); _inputLog?.Dispose(); await Board.SaveAsync(); };
    }

    /// <summary>Board input log next to the diagnostics logs: every pen/mouse sample and why it was accepted or not.</summary>
    private void OpenInputLog()
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PizarraWorkflows", "diagnostics");
            Directory.CreateDirectory(folder);
            _inputLog = new StreamWriter(new FileStream(Path.Combine(folder, $"tablero-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        }
        catch { _inputLog = null; }
    }

    private void WriteInputLog(object entry)
    {
        try { _inputLog?.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { timestamp = DateTimeOffset.Now, entry })); }
        catch { }
    }

    private async Task RefreshLibraryAsync(string? selectPath = null)
    {
        _refreshingLibrary = true;
        try
        {
            BoardList.Items.Clear();
            foreach (var dir in Directory.GetDirectories(_library).OrderBy(x => x))
            {
                var project = new LibraryEntry("📁  " + Path.GetFileName(dir), null, dir, Path.GetFileName(dir));
                BoardList.Items.Add(project);
                if (SamePath(dir, selectPath)) BoardList.SelectedItem = project;
                foreach (var file in Directory.GetFiles(dir, "*" + BoardLibrary.BoardExtension).OrderBy(x => x))
                {
                    LibraryEntry board;
                    try { var doc = await new BoardRepository().LoadAsync(file); board = new LibraryEntry("     ▫  " + doc.Name, file, dir, doc.Name); }
                    catch { board = new LibraryEntry("     ⚠  " + Path.GetFileNameWithoutExtension(file) + " (no se pudo abrir)", file, dir, Path.GetFileNameWithoutExtension(file)); }
                    BoardList.Items.Add(board);
                    if (SamePath(file, selectPath)) BoardList.SelectedItem = board;
                }
            }
        }
        finally { _refreshingLibrary = false; }
        ShowItemPanel(BoardList.SelectedItem as LibraryEntry);
    }

    private static bool SamePath(string path, string? other) =>
        other is not null && string.Equals(Path.GetFullPath(path), Path.GetFullPath(other), StringComparison.OrdinalIgnoreCase);

    /// <summary>New project with an automatic name ("Proyecto 1", …) and its first board, opened right away.</summary>
    private async void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        await Board.SaveAsync();
        var name = BoardLibrary.NextName("Proyecto", Directory.GetDirectories(_library).Select(Path.GetFileName)!);
        var dir = BoardLibrary.UniqueDirectoryPath(_library, name);
        Directory.CreateDirectory(dir);
        _selectedProject = dir;
        await CreateBoardAsync(dir);
    }

    /// <summary>New board with an automatic name ("Tablero 1", …) in the selected project; renaming happens in the side panel.</summary>
    private async void NewBoard_Click(object sender, RoutedEventArgs e)
    {
        await Board.SaveAsync();
        await CreateBoardAsync(await EnsureProjectAsync());
    }

    private Task<string> EnsureProjectAsync()
    {
        var project = _selectedProject is not null && Directory.Exists(_selectedProject) ? _selectedProject : Directory.GetDirectories(_library).OrderBy(x => x).FirstOrDefault();
        if (project is null)
        {
            project = BoardLibrary.UniqueDirectoryPath(_library, "Proyecto 1");
            Directory.CreateDirectory(project);
        }
        _selectedProject = project;
        return Task.FromResult(project);
    }

    private async Task CreateBoardAsync(string project)
    {
        var existing = new List<string>();
        foreach (var file in Directory.GetFiles(project, "*" + BoardLibrary.BoardExtension))
        {
            existing.Add(Path.GetFileNameWithoutExtension(file));
            try { existing.Add((await new BoardRepository().LoadAsync(file)).Name); } catch { }
        }
        var doc = new BoardDocument { Name = BoardLibrary.NextName("Tablero", existing) };
        var path = BoardLibrary.UniqueBoardPath(project, doc.Name);
        await new BoardRepository().SaveAsync(doc, path);
        await Board.LoadAsync(doc, path);
        BoardTitle.Text = doc.Name;
        await RefreshLibraryAsync(path);
        StatusText.Text = $"“{doc.Name}” creado · cámbiale el nombre en el panel de la izquierda";
    }

    private async void BoardList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingLibrary) return;
        var entry = BoardList.SelectedItem as LibraryEntry;
        ShowItemPanel(entry);
        if (entry is null) return;
        _selectedProject = entry.ProjectPath;
        if (entry.Path is null || SamePath(entry.Path, Board.FilePath)) return;
        try { await Board.SaveAsync(); var doc = await new BoardRepository().LoadAsync(entry.Path); await Board.LoadAsync(doc, entry.Path); BoardTitle.Text = doc.Name; }
        catch (Exception ex) { StatusText.Text = $"No se pudo abrir el tablero · {ex.Message}"; }
    }

    private void ShowItemPanel(LibraryEntry? entry)
    {
        ResetDeleteConfirmation();
        ItemPanel.Visibility = entry is null ? Visibility.Collapsed : Visibility.Visible;
        if (entry is null) return;
        ItemKindText.Text = entry.Path is null ? "PROYECTO" : "TABLERO";
        RenameBox.Text = entry.Name;
    }

    private void RenameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        Rename_Click(sender, e);
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (BoardList.SelectedItem is not LibraryEntry entry) return;
        var name = RenameBox.Text.Trim();
        if (name.Length == 0 || name == entry.Name) return;
        try
        {
            await Board.SaveAsync();
            string selectPath;
            if (entry.Path is null)
            {
                var target = BoardLibrary.UniqueDirectoryPath(_library, name, entry.ProjectPath);
                if (!SamePath(target, entry.ProjectPath)) Directory.Move(entry.ProjectPath, target);
                if (Board.FilePath is { } open && open.StartsWith(entry.ProjectPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    Board.SetDocumentName(Board.Document.Name, Path.Combine(target, Path.GetFileName(open)));
                _selectedProject = target;
                selectPath = target;
            }
            else
            {
                var target = BoardLibrary.UniqueBoardPath(entry.ProjectPath, name, entry.Path);
                var doc = SamePath(entry.Path, Board.FilePath) ? Board.Document : await new BoardRepository().LoadAsync(entry.Path);
                await new BoardRepository().SaveAsync(doc with { Name = name }, target);
                if (!SamePath(target, entry.Path)) File.Delete(entry.Path);
                if (SamePath(entry.Path, Board.FilePath)) Board.SetDocumentName(name, target);
                selectPath = target;
            }
            await RefreshLibraryAsync(selectPath);
            StatusText.Text = $"Renombrado a “{name}”";
        }
        catch (Exception ex) { StatusText.Text = $"No se pudo renombrar · {ex.Message}"; }
    }

    /// <summary>Two taps to delete: the first arms the button for a few seconds, the second sends the item to the Recycle Bin.</summary>
    private async void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (BoardList.SelectedItem is not LibraryEntry entry) return;
        if (!_deleteArmed)
        {
            _deleteArmed = true;
            DeleteItemButton.Content = "¿Seguro? Toca otra vez";
            _deleteTimer.Stop(); _deleteTimer.Start();
            return;
        }
        ResetDeleteConfirmation();
        try
        {
            var target = entry.Path ?? entry.ProjectPath;
            var openInside = Board.FilePath is { } open && (SamePath(open, target)
                || (entry.Path is null && open.StartsWith(entry.ProjectPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));
            if (openInside) Board.CloseDocument(); else await Board.SaveAsync();
            RecycleBin.Send(target);
            if (entry.Path is null && SamePath(entry.ProjectPath, _selectedProject)) _selectedProject = null;
            if (openInside) BoardTitle.Text = "Abre un tablero o crea uno nuevo";
            await RefreshLibraryAsync(entry.Path is null ? null : entry.ProjectPath);
            StatusText.Text = $"“{entry.Name}” se envió a la Papelera";
        }
        catch (Exception ex) { StatusText.Text = $"No se pudo eliminar · {ex.Message}"; }
    }

    private void ResetDeleteConfirmation()
    {
        _deleteArmed = false;
        _deleteTimer.Stop();
        if (DeleteItemButton is not null) DeleteItemButton.Content = "🗑 Eliminar";
    }

    /// <summary>Opens a board first when none is open, so a shape button never asks anything.</summary>
    private async Task<bool> EnsureBoardAsync()
    {
        if (Board.FilePath is not null) return true;
        await CreateBoardAsync(await EnsureProjectAsync());
        return Board.FilePath is not null;
    }

    private async void AddNote_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureBoardAsync()) return;
        Board.AddNote("");
        StatusText.Text = "Nota creada · escribe dentro con el lápiz";
        await Board.SaveAsync();
    }
    private async void AddProcess_Click(object sender, RoutedEventArgs e) => await AddShapeAsync(WorkflowShapeKind.Process);
    private async void AddDecision_Click(object sender, RoutedEventArgs e) => await AddShapeAsync(WorkflowShapeKind.Decision);
    private async void AddTerminal_Click(object sender, RoutedEventArgs e) => await AddShapeAsync(WorkflowShapeKind.Terminal);
    private async void AddInputOutput_Click(object sender, RoutedEventArgs e) => await AddShapeAsync(WorkflowShapeKind.InputOutput);
    private async Task AddShapeAsync(WorkflowShapeKind kind)
    {
        if (!await EnsureBoardAsync()) return;
        Board.AddShape(kind);
        SetTool(BoardTool.Select, "Bloque creado · arrástralo con el lápiz; usa → Flecha para unirlo y Aa Texto para cambiar su texto");
        await Board.SaveAsync();
    }
    private void Connect_Click(object sender, RoutedEventArgs e) => SetTool(BoardTool.Connect, "Flecha: toca el bloque de origen y luego el de destino");
    private void Select_Click(object sender, RoutedEventArgs e) => SetTool(BoardTool.Select, "Mover: toca un bloque y arrástralo");
    private void Draw_Click(object sender, RoutedEventArgs e) => SetTool(BoardTool.Draw, "Dibujar tinta libre o dentro de una nota");
    private void Erase_Click(object sender, RoutedEventArgs e) => SetTool(BoardTool.Erase, "Goma: pasa el lápiz sobre los trazos que quieras borrar");
    private void Pan_Click(object sender, RoutedEventArgs e) => SetTool(BoardTool.Pan, "Mano: arrastra con el lápiz para subir, bajar o moverte · ⌂ Inicio vuelve al principio");
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => Board.ZoomAtCenter(1.25);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => Board.ZoomAtCenter(1 / 1.25);
    private void ResetView_Click(object sender, RoutedEventArgs e) { Board.ResetView(); StatusText.Text = "De vuelta al principio del tablero"; }
    private void SetTool(BoardTool tool, string status)
    {
        Board.Tool = tool;
        Board.Cursor = tool == BoardTool.Pan ? Cursors.SizeAll : null;
        StatusText.Text = status;
        UpdateToolButtons();
    }
    private void UpdateToolButtons()
    {
        var active = new System.Windows.Media.SolidColorBrush(Color.FromRgb(76, 94, 177));
        var idle = new System.Windows.Media.SolidColorBrush(Color.FromRgb(27, 37, 64));
        if (DrawToolButton is not null) DrawToolButton.Background = Board.Tool == BoardTool.Draw ? active : idle;
        if (EraseToolButton is not null) EraseToolButton.Background = Board.Tool == BoardTool.Erase ? active : idle;
        if (SelectToolButton is not null) SelectToolButton.Background = Board.Tool == BoardTool.Select ? active : idle;
        if (ConnectToolButton is not null) ConnectToolButton.Background = Board.Tool == BoardTool.Connect ? active : idle;
        if (PanToolButton is not null) PanToolButton.Background = Board.Tool == BoardTool.Pan ? active : idle;
    }
    private void Undo_Click(object sender, RoutedEventArgs e) => StatusText.Text = Board.Undo() ? "Deshecho" : "No hay nada que deshacer";
    private void Redo_Click(object sender, RoutedEventArgs e) => StatusText.Text = Board.Redo() ? "Rehecho" : "No hay nada que rehacer";
    private void Edit_Click(object sender, RoutedEventArgs e) => EditSelection();
    private void Delete_Click(object sender, RoutedEventArgs e) =>
        StatusText.Text = Board.DeleteSelected() ? "Bloque quitado" : "Primero toca un bloque con ↖ Mover; para borrar tinta usa ⌫ Goma";
    private void EditSelection()
    {
        if (Board.SelectedText is not { } value) { StatusText.Text = "Primero toca un bloque con ↖ Mover"; return; }
        ShowInlinePrompt("Texto del bloque", value, edited => Board.EditSelected(edited));
    }

    private void ShowInlinePrompt(string title, string initial, Action<string> onAccept)
    {
        _inlinePromptAccept = onAccept;
        InlinePromptTitle.Text = title;
        InlinePromptBox.Text = initial;
        InlinePrompt.Visibility = Visibility.Visible;
        InlinePromptBox.Focus();
        InlinePromptBox.SelectAll();
    }

    private void CloseInlinePrompt(bool accept)
    {
        var onAccept = _inlinePromptAccept;
        _inlinePromptAccept = null;
        InlinePrompt.Visibility = Visibility.Collapsed;
        if (accept) onAccept?.Invoke(InlinePromptBox.Text);
        Board.Focus();
    }

    private void InlinePromptOk_Click(object sender, RoutedEventArgs e) => CloseInlinePrompt(accept: true);
    private void InlinePromptCancel_Click(object sender, RoutedEventArgs e) => CloseInlinePrompt(accept: false);
    private void InlinePromptBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; CloseInlinePrompt(accept: true); }
    }
    private void Fullscreen_Click(object sender, RoutedEventArgs e) { if (_fullScreen) ExitFullscreen(); else EnterFullscreen(); }
    private void LinkWacom_Click(object sender, RoutedEventArgs e)
    {
        _bindingSuspended = false;
        LinkWacomButton.Content = "Vincular Wacom";
        LinkWacomButton.ClearValue(BackgroundProperty);
        WriteInputLog(new { binding = "reanudada" });
        ActivateWintab();
    }

    /// <summary>Esc releases the pen from the window until the user taps the highlighted button again.</summary>
    private void SuspendBinding()
    {
        _bindingSuspended = true;
        DeactivateWintab();
        Board.ActivateConfinement(false);
        WacomStatusText.Text = "Vinculación suspendida (Esc) · toca Reanudar Wacom";
        LinkWacomButton.Content = "⚠ Reanudar Wacom";
        LinkWacomButton.Background = new SolidColorBrush(Color.FromRgb(122, 74, 22));
        WriteInputLog(new { binding = "suspendida-con-esc" });
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
            if (WindowState == WindowState.Minimized || !IsActive) { WriteInputLog(new { source = "WinTab", outcome = "ventana-inactiva", tipDown }); return; }
            if (NativeWindowBounds.ScreenToElement(Board, screenPoint) is not Point boardPoint) { WriteInputLog(new { source = "WinTab", outcome = "sin-conversion", screenX = screenPoint.X, screenY = screenPoint.Y }); return; }
            Board.SetWintabInputObserved(true);
            if (!Board.IsCursorConfined && !_bindingSuspended) Board.ConfineCursor(true);
            Board.HandlePointerSample(boardPoint, pressure, tipDown, buttons, PointerInputSource.WinTab);
        };
        // The cursor is held inside the window only while the pen is near the tablet, so the mouse stays free otherwise.
        _wintab.ProximityChanged += inProximity =>
        {
            if (inProximity) { if (IsActive && !_bindingSuspended) Board.ConfineCursor(true); return; }
            Board.ReleaseMappedPen();
            Board.ConfineCursor(false);
        };
        _wintab.StatusChanged += message => { WacomStatusText.Text = message; Board.SetMappedInputActive(_wintab.IsActive); WriteInputLog(new { status = message, area = WindowScreenBounds().ToString(), mirrored = _wintab.PacketYMirrored }); };
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
        Board.RefreshCursorBounds();
    }

    /// <summary>Client area in physical pixels: the whole tablet maps onto it, toolbar and library included.</summary>
    private Rect WindowScreenBounds()
    {
        if (!IsInitialized || WindowState == WindowState.Minimized) return Rect.Empty;
        return NativeWindowBounds.ClientArea(this);
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

    private static string SafeName(string name) => BoardLibrary.SafeName(name);

    /// <summary>A project (Path is null) or a board in the library list; Name is what the rename box shows.</summary>
    private sealed record LibraryEntry(string Label, string? Path, string ProjectPath, string Name) { public override string ToString() => Label; }
}
