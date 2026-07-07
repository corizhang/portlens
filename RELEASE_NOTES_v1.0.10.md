## What's Changed

### Bug Fixes
- Fixed list jitter/scroll jump during automatic port scans. The card list no longer rebuilds or refreshes when scan results have the same order and content.
- `PortEntryViewModel.Update` now only raises `PropertyChanged` for properties whose formatted value actually changed, eliminating the PropertyChanged storm on every refresh interval.
- `RefreshSearchFilterAsync` now skips `FilteredEntries.Refresh()` when the matching key set is identical to the previous scan.

### Performance Improvements
- Tray icon context menu is built once and reused; only dynamic state (status text, pause/resume, enabled items) is updated on right-click.
- About tab shields.io badge images are cached in memory across dialog opens.
- Command-line whitespace normalization no longer uses `Regex.Replace`; replaced with a zero-allocation-style `StringBuilder` loop.
- App metrics timer pauses while the window is hidden or minimized, resuming with a fresh CPU baseline when restored.

### Engineering
- Added `PortLens.Benchmarks` project with BenchmarkDotNet scenarios for `PortScanner.Scan`, `FrameworkDetector.InferFramework`, `ProjectRootResolver`, and `ProcessTreeReader.CountDescendants`.
- Added `[MemoryDiagnoser]` to all benchmarks to track allocations.

### Full Changelog
v1.0.9...v1.0.10
