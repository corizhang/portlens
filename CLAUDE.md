# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

PortLens is a Windows-only WPF desktop app that monitors local TCP listening ports and surfaces development services. The repository uses a two-project layout under `work/`:

- `PortLens.Core` — .NET 10 class library that owns port scanning, process inspection, and framework inference.
- `PortLens.Desktop` — .NET 10 WPF executable (`<UseWPF>true</UseWPF>`), references Core, and owns the UI, tray icon, settings, and user actions.

There is no `.sln` file; build and publish operate directly on the `.csproj` files.

## Common commands

All commands should be run from the repo root unless noted.

### Build

```powershell
dotnet build work/PortLens.Desktop/PortLens.Desktop.csproj
```

### Run

```powershell
dotnet run --project work/PortLens.Desktop/PortLens.Desktop.csproj
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

There are no unit tests in this repository.

## Architecture notes

### Scanning pipeline (`PortLens.Core`)

`PortScanner.Scan(PortScanOptions)` is the main entry point. It:

1. Calls `NativeTcp.GetTcpListeners()` to read the TCP table via P/Invoke to `iphlpapi.dll` (IPv4 and IPv6).
2. Filters to local listening addresses, groups by `Protocol + LocalPort + ProcessId`, and picks the preferred listener address (`127.0.0.1` > `0.0.0.0` > `::`/`[::]`).
3. Uses `ProcessInspector` to enrich each row with process name, CPU, memory, uptime, executable path, command line, working directory, framework, and project name.
4. If `ShowAll` is false, drops entries whose framework is not in the configured `EnabledFrameworks` set.

### Process inspection

`ProcessInspector` is the heaviest part of Core:

- Command lines are fetched via PowerShell/CIM (`Win32_Process`), either one at a time or batched with `ReadMany`.
- The current working directory is read from the process PEB via `NtQueryInformationProcess` and `ReadProcessMemory`.
- Framework detection is string matching against a combined haystack of process name, command line, working directory, and executable path.
- Project directory inference walks command-line paths looking for markers such as `node_modules`, `.csproj`, `.dll`, `.jar`, `manage.py`, etc.
- CPU is computed across scans by comparing `Process.TotalProcessorTime` samples.
- All heavy lookups are cached and pruned to live process IDs each scan.

### UI layer (`PortLens.Desktop`)

`MainWindow` is the application shell. Important responsibilities:

- Uses `PortScanner` on a background thread (`Task.Run`) and applies results to an `ObservableCollection<PortEntryViewModel>` via `ApplyEntries`. Existing viewmodels are reused and updated in place to avoid list flicker; new ones are inserted at the correct sorted position.
- Filtering and grouping are done through WPF `CollectionViewSource`:
  - Search filters on a joined string of port, PID, names, framework, command, and directory.
  - Project grouping uses `PortEntryViewModel.ProjectGroupKey`, which is resolved by `ProjectRootResolver`.
- A `DispatcherTimer` drives recurring scans. The interval slows from the user-selected foreground interval (3/5/10/30s) to at least 30s when the window is hidden or minimized.
- Settings are persisted to `%APPDATA%/PortLens/settings.json` through `DesktopSettingsStore`.

### Settings and dialogs

- `DesktopSettings` is the persisted model. Defaults include the enabled framework list and UI flags.
- `SettingsDialog` is a `UserControl` shown inside a MaterialDesign `DialogHost`. It has three tabs: General, Rules (framework toggles), and Blacklist (excluded ports).
- `KillConfirmationDialog` builds its UI in code and returns `true`/`false` through the same `DialogHost`.

### Tray icon

`TrayIconService` wraps a WinForms `NotifyIcon`. Left-click restores the window; right-click opens a WPF `ContextMenu` built in code. It supports pause/resume scanning, refresh, copy port summary, settings, and exit.

### User actions

`PortEntryActionService` centralizes entry-level actions: open URL, copy URL/PID/command, open directories, open terminal (prefers `wt.exe`, falls back to PowerShell), and kill process tree. Killing calls `PortScanner.Kill`, which invokes `Process.Kill(entireProcessTree: true)` after user confirmation.

## Important implementation details

- The app targets `net10.0-windows` and is not cross-platform; the native TCP tables and process memory reads are Windows-only.
- The WPF assembly name is `PortLens` (`<AssemblyName>PortLens</AssemblyName>`), but the project file is `PortLens.Desktop.csproj` and the default namespace is `PortLens.Desktop`. Many build artifacts therefore appear as `PortLens.dll`/`PortLens.exe` even though the folder is named `PortLens.Desktop`.
- `GlobalUsings.cs` in the Desktop project only imports `System.IO` globally.
- The publish script uses `--self-contained false`, so the runtime must be installed on the target machine.
- `outputs/` is gitignored and contains previously published standalone builds plus icon assets; do not check new build artifacts into git.

## File paths worth knowing

- Entry point: `work/PortLens.Desktop/App.xaml`
- Main window: `work/PortLens.Desktop/MainWindow.xaml`
- Scanning core: `work/PortLens.Core/Services/PortScanner.cs`
- Process enrichment: `work/PortLens.Core/Services/ProcessInspector.cs`
- Native TCP table reader: `work/PortLens.Core/Services/NativeTcp.cs`
- Settings model/store: `work/PortLens.Desktop/Settings/DesktopSettings.cs`, `DesktopSettingsStore.cs`
- Publish script: `scripts/publish.ps1`
