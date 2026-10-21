# Changelog

All notable changes to **Six Console** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.1.0] - 2026-10-22

### Added
- **User-Configurable Keyboard Shortcuts**:
  - Configurable in-app shortcuts for benign actions: Toggle Profile (`F10`), View Navigation (`Ctrl+1` Profile, `Ctrl+2` Settings, `Ctrl+3` Help), Operator Navigation (`Ctrl+Up`/`Ctrl+Down`), and Weapon Navigation (`Ctrl+Left`/`Ctrl+Right`).
  - Interactive shortcut recorder in Settings with native key capture, conflict detection, OS-reserved key safeguards (Escape, Alt+Tab, Alt+F4, Ctrl+Alt+Del), Escape-to-cancel, and one-click Reset to Defaults.
  - Centralized `ShortcutService` handling clean dispatch, registration, and unregistration lifecycle.
- **Safe Configuration Import (JSON Schema v2)**:
  - Strictly allowlisted, non-executable JSON importer for benign application preferences (display labels, theme, UI shortcuts).
  - Explicit validation rejecting scripts, recoil overrides, rapid-fire parameters, and arbitrary input automation tokens.
  - File size guard (64 KB ceiling) and transactional import with interactive preview dialog and rollback on failure.
- **Settings Store Schema v2 & Resilience**:
  - Upgraded settings store with schema versioning (`CurrentSchemaVersion = 2`) and deterministic v1-to-v2 migration preserving all existing user settings.
  - Atomic JSON file writing via temporary file replacement (`settings.json.tmp` -> `settings.json`) paired with automated backup (`settings.json.bak`) and corruption recovery.
- **Comprehensive Test Suite**:
  - Added 24 automated unit and regression tests in `ShortcutServiceTests`, `SafeConfigImportTests`, and `SettingsMigrationTests`, expanding test coverage to 83 passing tests.

## [1.0.0] - 2026-10-17

### Added
- **Full Operator & Weapon Roster**: 76 Rainbow Six Siege operators (Attacker and Defender) and 142 distinct weapon configurations transcribed 1:1 from canonical Lua script into `operators.json`.
- **Mathematical Fidelity**: Effective pull calculation faithfully implementing `floor(pull * recoilMultiplier + 0.5)` with vertical variance and horizontal jitter simulation.
- **Dual Weapon Modes**:
  - Full-Auto recoil compensation with configurable delay intervals (2–5 ms default).
  - Rapid-fire mode for semi-automatic/DMR weapons emitting secondary key bursts (`rfKey` default 'P', 25–40 ms hold, 65–90 ms gap).
- **Native Windows Desktop UI**:
  - Built with WinUI 3, Windows App SDK, and CommunityToolkit MVVM.
  - Windows Fluent Design system supporting System, Dark, and Light themes with immediate live switching.
  - Interactive operator roster filtering and search.
  - Live Loadout display showing pull values and timing windows.
  - Real-time status bar and title bar pill indicators.
  - Integrated Emergency STOP control in title bar.
- **Hardware Mouse Hook Engine**:
  - Low-level Win32 mouse hook (`WH_MOUSE_LL`) running on a dedicated worker thread with message pump.
  - Reliable Middle Mouse Button (MMB) toggle.
  - Mouse Button 4 & 5 navigation (Operator selection when Caps Lock is off; Weapon selection when Caps Lock is on).
  - Synthetic input emission via `SendInput` tagged with `LLMHF_INJECTED` to prevent hook feedback loops.
- **Settings & Persistence**:
  - Atomic JSON persistence to `%LOCALAPPDATA%\SixConsole\settings.json`.
  - Comprehensive input validation with inline feedback and safe source default fallbacks.
  - Dedicated diagnostic logging to `%LOCALAPPDATA%\SixConsole\log.txt`.
- **Automated Test Suite**:
  - 59 comprehensive xUnit unit and regression tests covering source data integrity, calculations, navigation wraparound, settings validation, persistence, and input engine loops.

### Fixed
- **Option Selection Performance**: Resolved UI lag during operator and weapon selection by transitioning `OperatorRow` to an observable model, replacing full collection teardowns with in-place updates.
- **Middle Mouse Button Reactivation**: Fixed button handler ordering in `InputEngine` to allow physical Middle Mouse Button presses to toggle the feature back ON when disabled.
