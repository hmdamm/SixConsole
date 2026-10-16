# Changelog

All notable changes to **Six Console** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

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
