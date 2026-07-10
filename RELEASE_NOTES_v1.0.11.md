## What's Changed

### New Features
- Added configurable framework detection rules. PortLens can now persist and apply editable rules for process name keywords, command-line keywords, path keywords, and default ports.
- Added a Rules management experience in Settings with a searchable rule list and a focused detail editor for the selected rule.

### Improvements
- Reworked the Rules settings tab from a long stacked form into a two-column layout, making it easier to locate and edit individual framework rules.
- Added a dedicated settings search text box style so Material Design outlined search fields render at the correct height.
- Default framework rules now live in a shared Core rules model instead of being embedded directly in `FrameworkDetector`.

### Bug Fixes
- Fixed overly broad default process-name matching so `node`, `python`, and `java` processes no longer cause unrelated frameworks to win by rule order.
- Fixed project root resolution at user/profile/temp/app-data boundaries so parent folders such as the user profile are not treated as project roots merely because they contain editor markers.

### Engineering
- Added Core tests for custom framework rules, path keyword matching, and rule order behavior.
- Made project root/name resolver tests use writable temp directories instead of assuming access to drive roots.

### Full Changelog
v1.0.10...v1.0.11
