# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

PortLens is a Windows-only WPF desktop app that monitors local TCP listening ports and surfaces development services. The repository uses a solution file and a four-project layout under `work/`:

- `PortLens.Core` — .NET 10 class library that owns port scanning, process inspection, and framework inference.
- `PortLens.Desktop` — .NET 10 WPF executable (`<UseWPF>true</UseWPF>`), references Core, and owns the UI, tray icon, settings, and user actions.
- `PortLens.Core.Tests` — xUnit test project covering the pure logic in Core.
- `PortLens.Benchmarks` — BenchmarkDotNet console project for core performance scenarios.

## Common commands

All commands should be run from the repo root unless noted.

### Build

```powershell
dotnet build PortLens.sln
```

### Run

```powershell
dotnet run --project work/PortLens.Desktop/PortLens.Desktop.csproj
```

### Test

```powershell
dotnet test PortLens.sln
```

To run only Core tests:

```powershell
dotnet test work/PortLens.Core.Tests/PortLens.Core.Tests.csproj
```

### Publish

Use the provided publish script, which produces a framework-dependent win-x64 build into `outputs/PortLensMaterial`:

```powershell
./scripts/publish.ps1
```

Optional parameters:

```powershell
./scripts/publish.ps1 -Configuration Debug -Output outputs/PortLens
```

### Benchmarks

Run BenchmarkDotNet scenarios (builds the benchmark project in Release automatically):

```powershell
dotnet run --project work/PortLens.Benchmarks/PortLens.Benchmarks.csproj --configuration Release
```

Run a single benchmark quickly:

```powershell
dotnet run --project work/PortLens.Benchmarks/PortLens.Benchmarks.csproj --configuration Release -- --filter *FrameworkDetectionBenchmark* --job short
```

### CI

A GitHub Actions workflow in `.github/workflows/ci.yml` runs on pushes and PRs to `main`/`master`: restore, build, test, publish, and upload the `outputs/PortLensMaterial` artifact.

## Architecture notes

### Scanning pipeline (`PortLens.Core`)

`PortScanner.Scan(PortScanOptions)` is the main entry point. It:

1. Calls `NativeTcp.GetTcpListeners()` to read the TCP table via P/Invoke to `iphlpapi.dll` (IPv4 and IPv6).
2. Uses `PortScannerFilters` to keep local listening addresses, group by `Protocol + LocalPort + ProcessId`, and pick the preferred listener address (`127.0.0.1` > `0.0.0.0` > `::`/`[::]`).
3. Uses `ProcessInspector` to enrich each row with process name, CPU, memory, uptime, executable path, command line, working directory, framework, and project name.
4. If `ShowAll` is false, drops entries whose framework is not in the configured `EnabledFrameworks` set.

### Process inspection

`ProcessInspector` coordinates several focused services:

- `ProcessCommandLineReader` fetches command lines via native Windows APIs (`NtQueryInformationProcess` with `ProcessCommandLineInformation`, falling back to PEB + `RTL_USER_PROCESS_PARAMETERS.CommandLine`), with no PowerShell/CIM overhead. Whitespace is collapsed by `CommandLineNormalizer`.
- `ProcessCurrentDirectoryReader` reads the current working directory from the process PEB.
- `ProcessTreeReader` counts child processes.
- `FrameworkDetector` infers frameworks from a combined haystack of process name, command line, working directory, and executable path.
- `ProjectNameResolver` / `ProjectRootResolver` walk command-line paths and directory markers to infer project names and roots.
- `CpuSampler` computes CPU from a process snapshot's `TotalProcessorTime`.
- `ProcessInspector.CaptureSnapshot` calls `Process.GetProcesses()` once per scan and passes the snapshot dictionary to enrichment methods, avoiding repeated `Process.GetProcessById` calls.

All heavy lookups are cached in `ConcurrentDictionary`s and pruned to live process IDs each scan.

### UI layer (`PortLens.Desktop`)

The UI uses MVVM and dependency injection:

- `MainWindowViewModel` owns scan scheduling, search debouncing, filtering, grouping, blacklist, framework rules, and status-bar display state (`StatusText`, `ServiceCountText`, `LastScanText`, `AppResourceText`, `AppVersionText`, `ShowAppMetrics`). It exposes a `SnackbarRequested` event so the view can show snackbar messages without the view model depending on a UI service.
- `MainWindow` is the view shell: window management, settings persistence, app metrics timer, tray interaction, and event handlers. It subscribes to `MainWindowViewModel.SnackbarRequested` and manually wires `TrayIconService` and `PortEntryActionService` to avoid DI cycles.
- On startup, `MainWindow` creates the view model, sets `DataContext`, and then calls `ApplyPersistedSettings()`, which uses `BuildStateFromSettings()` + `MainWindowViewModel.ApplyState(...)` to hydrate the view model from `DesktopSettingsStore`. This order matters: settings must be applied after the view model exists.
- `ServiceRegistration` wires Core services and `MainWindowViewModel` into a `Microsoft.Extensions.DependencyInjection` container. `MainWindow` is registered as a singleton factory because it must be created before the services that need to reference it.
- Filtering and grouping use WPF `CollectionViewSource`:
  - Search filters on a precomputed joined string of port, PID, names, framework, command, and directory.
  - Project grouping uses `PortEntryViewModel.ProjectGroupKey` from `ProjectRootResolver`.
- `MainWindowViewModel.ApplyEntries` uses a `SuppressibleObservableCollection` to diff the incoming scan results against the existing VMs and raise a single `Reset` event, reducing the WPF layout and grouping work during refreshes.
- A `DispatcherTimer` drives recurring scans. The interval slows from the user-selected foreground interval (3/5/10/30s) to at least 30s when hidden or minimized.
- Settings are persisted to `%APPDATA%/PortLens/settings.json` through `DesktopSettingsStore`, with versioned migrations.
- `Themes/PortLensColors.xaml` and `Themes/PortLensStyles.xaml` centralize brushes and styles.

### Settings and dialogs

- `DesktopSettings` is the persisted model. Defaults include the enabled framework list and UI flags, plus a `Version` field for migrations.
- `SettingsDialog` is a `UserControl` shown inside a MaterialDesign `DialogHost`. It has three tabs: General, Rules (framework toggles), and Blacklist (excluded ports).
- `KillConfirmationDialog` builds its UI in code and returns `true`/`false` through the same `DialogHost`.

### Tray icon

`TrayIconService` wraps a WinForms `NotifyIcon`. Left-click restores the window; right-click opens a WPF `ContextMenu` built in code. It supports pause/resume scanning, refresh, copy port summary, settings, and exit.

### User actions

`PortEntryActionService` centralizes entry-level actions: open URL, copy URL/PID/command, open directories, open terminal (prefers `wt.exe`, falls back to PowerShell), and kill process tree. Killing calls `PortScanner.Kill`, which invokes `Process.Kill(entireProcessTree: true)` after user confirmation.

### Logging

`Microsoft.Extensions.Logging` is wired through DI. Desktop registers a custom `FileLoggerProvider` that writes warnings and errors to `%LocalAppData%\PortLens\logs\portlens-YYYYMMDD.log`. Global unhandled exceptions are logged via `App.xaml.cs` handlers.

## Important implementation details

- The app targets `net10.0-windows` and is not cross-platform; the native TCP tables and process memory reads are Windows-only.
- The WPF assembly name is `PortLens` (`<AssemblyName>PortLens</AssemblyName>`), but the project file is `PortLens.Desktop.csproj` and the default namespace is `PortLens.Desktop`. Many build artifacts therefore appear as `PortLens.dll`/`PortLens.exe` even though the folder is named `PortLens.Desktop`.
- `GlobalUsings.cs` in the Desktop project only imports `System.IO` globally.
- The publish script uses `--self-contained false`, so the runtime must be installed on the target machine.
- `outputs/` is gitignored and contains previously published standalone builds plus icon assets; do not check new build artifacts into git.
- Avoid introducing DI cycles between `MainWindow` and `MainWindowViewModel`. `MainWindow` references the view model, but the view model must not depend on a service whose implementation depends on `MainWindow`. Use events or manual service construction in `MainWindow` when a service needs the window instance.
- Because `MainWindow.DataContext` is `MainWindowViewModel`, XAML bindings resolve against the view model. Status-bar text, version text, and `ShowAppMetrics` live in `MainWindowViewModel`, not `MainWindow`. If a binding must target `MainWindow` itself (e.g., `SnackbarMessageQueue`), use `RelativeSource={RelativeSource AncestorType=Window}`.

## File paths worth knowing

- Entry point: `work/PortLens.Desktop/App.xaml`
- Main window: `work/PortLens.Desktop/MainWindow.xaml`
- Main view model: `work/PortLens.Desktop/ViewModels/MainWindowViewModel.cs`
- DI registration: `work/PortLens.Desktop/ServiceRegistration.cs`
- Scanning core: `work/PortLens.Core/Services/PortScanner.cs`
- Filter logic: `work/PortLens.Core/Services/PortScannerFilters.cs`
- Process enrichment: `work/PortLens.Core/Services/ProcessInspector.cs`
- Native TCP table reader: `work/PortLens.Core/Services/NativeTcp.cs`
- Settings model/store: `work/PortLens.Desktop/Settings/DesktopSettings.cs`, `DesktopSettingsStore.cs`
- Tests: `work/PortLens.Core.Tests/`
- Native command-line reader: `work/PortLens.Core/Services/ProcessCommandLineReader.cs`
- Command-line whitespace normalizer: `work/PortLens.Core/Services/CommandLineNormalizer.cs`
- Process snapshot: `work/PortLens.Core/Models/ProcessSnapshot.cs`
- Entry identity key: `work/PortLens.Core/Models/PortEntryKey.cs`
- Bulk collection updates: `work/PortLens.Desktop/Collections/SuppressibleObservableCollection.cs`
- Entry view model: `work/PortLens.Desktop/ViewModels/PortEntryViewModel.cs`
- Benchmarks: `work/PortLens.Benchmarks/PortLensBenchmarks.cs`
- Publish script: `scripts/publish.ps1`
- CI workflow: `.github/workflows/ci.yml`
