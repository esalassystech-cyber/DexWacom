# Pizarra Workflows Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Spanish personal Windows app for Wacom Bluetooth sticky notes and system workflow boards.

**Architecture:** WPF desktop shell with a domain library for board documents, geometry, autosave, and exports. WinPenKit is pinned as a submodule; the app uses its WPF stylus session and a disposable native cursor-clipping adapter. A per-user self-contained installer ships the finished app.

**Tech Stack:** .NET 10 x64, C# 14, WPF, WinPenKit commit `e5a7f61b78d64d6a77249b27bd76975af4d6cb4c`, System.Text.Json, xUnit, Inno Setup.

**Spec:** `docs/superpowers/specs/2026-10-29-pizarra-workflows-design.md`

## Global Constraints

- Windows 11 x64; no account, cloud sync, analytics, or network access at runtime.
- Keep the official Wacom driver installed and use Bluetooth for the CTL-4100WL.
- Board view is borderless full screen on the primary display; app releases cursor clipping whenever it is not active.
- `.flowboard` is a versioned JSON file and remains editable in this app.
- App data belongs under `%USERPROFILE%\Documents\Wacom Workboard`; the app installs under `%LOCALAPPDATA%\Programs\Wacom Workboard`.
- Pin WinPenKit to the submodule commit recorded in git; do not depend on its moving main branch.

## Review Focus

- Missing Wacom or driver: startup remains usable with mouse and must never clip the cursor.
- Pen disconnect or focus change while clipped: release clipping and stop ink safely.
- DPI and fractional screen coordinates: preserve stroke position through WPF transforms.
- Malformed or newer `.flowboard`: report the file error and never overwrite it during recovery.
- Moving/deleting workflow nodes: connectors follow node geometry, and no saved connector silently references a missing node.

---

### Task 1: Domain model, canvas geometry, and board storage

**Files:**
- Create: `src/Pizarra.Core/BoardDocument.cs`
- Create: `src/Pizarra.Core/BoardRepository.cs`
- Create: `src/Pizarra.Core/CanvasTransform.cs`
- Test: `tests/Pizarra.Tests/BoardRepositoryTests.cs`
- Test: `tests/Pizarra.Tests/CanvasTransformTests.cs`

**Interfaces:**
- `BoardDocument`: schema version, board id/name, ordered elements, connectors.
- `BoardRepository.SaveAsync(BoardDocument, string, CancellationToken)` and `LoadAsync(string, CancellationToken)`.
- `CanvasTransform.ScreenToWorld(Point, Rect, double, Vector)` and inverse `WorldToScreen(...)`.

- [ ] **Step 1: Write tests for JSON round-trip, unknown schema rejection, atomic replacement, and coordinate conversion at fractional scale.**
- [ ] **Step 2: Run the focused test suite and confirm the new behavior fails.**
- [ ] **Step 3: Implement immutable DTOs, version-1 serialization, path-safe atomic save, and the invertible canvas transform.**
- [ ] **Step 4: Run focused and full domain tests; confirm round-trip preserves every element field and transform round-trips within 0.01 DIP.**

### Task 2: Wacom pen source and cursor confinement lifecycle

**Files:**
- Create: `src/Pizarra.App/Input/PenInputController.cs`
- Create: `src/Pizarra.App/Input/CursorConfinementController.cs`
- Create: `src/Pizarra.App/Input/Win32CursorClipper.cs`
- Test: `tests/Pizarra.Tests/PenInputControllerTests.cs`
- Test: `tests/Pizarra.Tests/CursorConfinementControllerTests.cs`

**Interfaces:**
- `IPenPointSource.Start(nint hwnd)`, `Drain()`, `OnActivated()`, `Stop()`; production adapter owns `WpfStylusSession`.
- `ICursorClipper.Clip(Rect screenPixels)` and `Release()`; production adapter wraps `ClipCursor`.
- `PenInputController.Process(PenPoint, Rect clientScreen, double dpiX, double dpiY, CanvasTransform)`.

- [ ] **Step 1: Write tests for pressure normalization, screen-to-client-to-world mapping, Wacom detection gating, Escape/focus/minimize/disconnect release, and idempotent disposal.**
- [ ] **Step 2: Run focused tests and confirm each fails for its intended missing behavior.**
- [ ] **Step 3: Implement the WinPenKit adapter, coordinate conversion, device-presence watcher, and exception-safe clip lifecycle.**
- [ ] **Step 4: Run tests and a Windows smoke check; verify a synthetic pen stream is constrained to the board rectangle and releases on all exits.**

### Task 3: WPF library and infinite workflow canvas

**Files:**
- Create: `src/Pizarra.App/MainWindow.xaml`
- Create: `src/Pizarra.App/Views/BoardView.xaml`
- Create: `src/Pizarra.App/Views/BoardCanvas.cs`
- Create: `src/Pizarra.App/ViewModels/BoardViewModel.cs`
- Test: `tests/Pizarra.Tests/WorkflowBoardTests.cs`

**Interfaces:**
- `BoardViewModel.CreatePostIt`, `CreateProcess`, `CreateDecision`, `Connect`, `Move`, `Delete`, `Undo`, and `Redo` update a `BoardDocument` and raise `Changed`.
- `BoardCanvas.Render(BoardDocument, CanvasTransform)` renders nodes/connectors and hit-tests in world coordinates.

- [ ] **Step 1: Write tests for connectors following moved nodes, node deletion cleanup, and undo/redo restoring a board snapshot.**
- [ ] **Step 2: Run focused tests and confirm failure for the missing operations.**
- [ ] **Step 3: Implement the Spanish project library, full-screen board layout, tool modes, mixed text/ink post-its, process/decision shapes, attached arrows, selection, zoom, and pan.**
- [ ] **Step 4: Run tests, launch the app, and verify core board interactions with mouse and synthetic pen points.**

### Task 4: Autosave, library operations, and exports

**Files:**
- Create: `src/Pizarra.App/Services/LibraryService.cs`
- Create: `src/Pizarra.App/Services/BoardExportService.cs`
- Modify: `src/Pizarra.App/ViewModels/BoardViewModel.cs`
- Test: `tests/Pizarra.Tests/LibraryServiceTests.cs`
- Test: `tests/Pizarra.Tests/BoardExportServiceTests.cs`

- [ ] **Step 1: Write tests for folder/board CRUD, debounced autosave, PNG dimensions, and valid PDF signature/content page.**
- [ ] **Step 2: Run focused tests and confirm each missing behavior fails.**
- [ ] **Step 3: Implement local library under Documents, debounced saves, WPF PNG rendering, and PDF export using a pinned supported PDF library.**
- [ ] **Step 4: Run focused and full tests; open exported files using Windows shell and verify output.**

### Task 5: Per-user installer and release verification

**Files:**
- Create: `installer/PizarraWorkflows.iss`
- Create: `scripts/publish.ps1`
- Create: `README.md`

- [ ] **Step 1: Write packaging checks for expected published files, versioned installer metadata, no admin manifest, and per-user destinations.**
- [ ] **Step 2: Run checks against an empty publish folder and verify expected failure.**
- [ ] **Step 3: Implement self-contained x64 publish, Inno Setup per-user `.exe`, shortcuts, uninstall entry, and first-run driver instructions.**
- [ ] **Step 4: Build installer; install and uninstall under current user; verify app data remains after uninstall and no machine-wide files are written.**

### Final hardware acceptance

- [ ] Pair the CTL-4100WL over Bluetooth with the official Wacom driver and draw thin/pressure-varied strokes in a post-it.
- [ ] Verify the tablet maps across the board while it is full-screen on the single 3440 × 1440 monitor.
- [ ] Verify the cursor remains in the app; Escape, Alt+Tab, minimize, close, and Bluetooth disconnect each release it.
- [ ] Record the hardware/driver versions and results in the README. If the device is not connected to the development PC, report this acceptance as pending rather than passed.
