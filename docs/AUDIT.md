# AUDIT.md — design & implementation review (applied pre-delivery)

The `audit` skill in this environment was an empty stub ("Not pinned"); this checklist replaces it,
following its intended purpose (audit skill discipline) plus the ui-ux-pro-max pre-delivery
discipline and the named skills' rules.

## Contrast & palette
- [x] Only Fluent/monochrome tokens from `windows-fluent-native`; accent #60CDFF (dark) / #0078D4
      (light); success/warning/error reserved for state meanings; no purple, gradients, or glows.
- [x] Text hierarchy in both themes uses #FFFFFF/#C5C5C5 (dark) and #1A1A1A/#5A5A5A (light) on
      matching layer colors — white-on-#2B2B2B ≈ 15.2:1, #1A1A1A-on-#FFFFFF ≈ 16.8:1 (AA+).
- [x] Status colors: dark red #FF99A4 & light #C42B1C used for error text on base layers (≥4.5:1).
- [ ] High-contrast theme resources exist; HC visual pass verified only via code review, not visually.

## Keyboard & focus
- [x] All controls are native WinUI (TextBox, NumberBox, ToggleSwitch, Buttons, RadioButtons,
      NavigationView items) → native Tab order, focus rectangles, AccessKeys available via RTL.
- [x] No mouse-only interactions; every action reachable by Tab + Enter/Space.
- [x] Visible focus ring: native default (verified in dark shot — control focus visuals untouched).
- [ ] Fully instrumented keyboard-only walkthrough not performed (documented gap; manual item).

## Every control is functional
- [x] Titlebar STOP → `ProfileController.EmergencyStop` → engine cancels + rfKey up + disable.
- [x] Status toggle (TwoWay + Toggled) → engine toggle (engine event re-syncs state).
- [x] Operator tiles (ToggleButton list per side) → Selection.SelectOperator(id); wraparound logic
      shared with engine navigation (same `SelectionModel`).
- [x] Weapon rows (ListView) → Selection.SelectWeapon; engine-driven navigation re-syncs lists.
- [x] Search box filters both rosters; original ordering untouched (filtered view only).
- [x] Settings: NumberBox (min/max + spin buttons per ui-ux-pro-max guidance), Save/Revert/
      Restore-defaults dialog (ContentDialog confirm), theme RadioButtons apply immediately.
- [x] Status bar + titlebar pill reflect engine state including errors (state, not color alone:
      text + colored dot both).
- [x] No placeholder data anywhere: rosters render from the compiled catalogue; settings from the live model.

## States
- [x] Engine states surfaced: INITIALZING/DISABLED/ACTIVE/ENGINE ERROR/STOPPED/SHUTTING DOWN.
- [x] Error path: `LastError` shown in red under "Input backend" and in the status bar.
- [x] Disabled/empty: disabled state shows muted pill + "DISABLED"; empty search shows empty lists
      (no crash); malformed settings file falls back to source defaults.
- [ ] Loading/initializing state shows only as "INITIALIZING" pill at startup; no heavy
      initialization exists beyond input hook startup (verified non-blocking).

## Theme flow
- [x] Theme choice persisted in settings.json; applied on launch before window activation;
      Settings → Theme immediate; System/Light/Dark all function.
- [x] Verified visually: Dark (default run) and Light (settings-seeded run) screenshots reviewed.
- [ ] System-theme *auto-follow* at run time (Windows theme change while app open) relies on
      `ElementTheme.Default`; not visually re-verified after an OS theme flip mid-session.

## Layout & resize
- [x] Window 1152×592 default; content pane scrolls; roster wraps (custom WrapPanel); no fixed
      pixel layouts; NavigationView collapses to rail.
- [ ] Narrow-resize (< 480px) visual check and maximized-state check were NOT performed; manual item.

## Engine & safety
- [x] Synthetic input distinguishable (LLMHF_INJECTED filter + extraInfo marker) — no recursion.
- [x] All stop paths (toggle off, LMB release, emergency stop, dispose, hook failure) cancel
      loops via handles and release the rapid-fire key.
- [x] No admin requirement; no injection, no anti-cheat evasion; engine does nothing while disabled.
- [x] Cancellation-aware waits (`WaitHandle.WaitOne(ms)`) — no busy-waiting.
- [x] Log writes are wrapped; diagnostics never crash the app. Log file kept local (no telemetry).

## Findings & fixes applied during review
1. Launch ordering bug (controller after window) → fixed by building controller before the window.
2. Stale titlebar status (event raced window init) → fixed with initial-state pull.
3. Navigation wraparound off-by-one in zero-based conversion → caught by tests, fixed.
4. Two stale views after nav switch (event double-subscribe risk) → single subscription in shell.
5. PrintWindow double-render artifacts in screenshots → acknowledged as capture artifact, not app bug.

## Functional-debugging pass (2026-10-16): root causes found & fixed, verified live

Regression suite grew 37 → 53 tests; every fix below has a matching regression test or was
verified interactively through the real UI (UIA-driven clicks, screenshots, persisted-file checks).

Critical
1. `INPUTUNION`/`KEYBDINPUT` marshaling was invalid — a managed `byte[]` field inside the
   explicit-layout union made the type loader reject the struct (`TypeLoadException`), crashing
   the recoil/rapid-fire worker thread on the FIRST synthetic output. Rewritten with the correct
   x64 ABI (`ushort wScan`, unmanaged union; INPUT=40/KEYBDINPUT=24/MOUSEINPUT=32) and pinned by
   `Win32SendInputLayoutTests` plus injector construction tests.
2. `Dispose()` posted its quit message to thread id `0` (stub) — hook thread never signalled;
   now posts the stored hook-thread id.
3. Settings page rendered with every card row stacked on row 0 — the card Grids relied on
   auto-created rows, which WinUI sizes as star rows that collapse under the ScrollViewer's
   infinite measure. Fixed with explicit `RowDefinition Height="Auto"` rows. (Longstanding bug:
   the page had only ever been verified via text dumps, never screenshotted.)

High
4. Status bar didn't refresh after UI-driven selection changes (only engine events did) —
   shell now subscribes to `Selection.Changed` + settings-applied and re-syncs. Verified live:
   clicking a roster tile updates `op n/76 · weapon n/m · pull px` immediately.
5. Profile page leaked one engine subscription per visit (ghost view-models kept rebuilding
   rosters) — `Detach()` on unload + shell no longer recreates the already-displayed page.
6. Settings Save did not propagate to open views (pull texts stalled until an engine event) —
   `AppServices.SettingsApplied` event now refreshes Profile view-model, weapon list, and status
   bar. Verified live: multiplier 0.9→1.1 via UI updated `pull 42→52 px` everywhere.
7. Selection saves ran synchronously inside the low-level mouse hook callback (file I/O on the
   hot input path) — now serialized, coalesced (120 ms), and written on a worker. All mutating
   access to persisted settings is gate-protected; store file access is serialized.
8. Weapon list lost its visual selection highlight after every snapshot rebuild —
   `SelectedWeaponIndex` now re-pushed into the ListView after rebuilds.
9. Second instance would double-inject input — named-mutex guard added (logged & suppressed).

Also
10. `EngineSnapshot.OperatorCount` was always 0 — real count now flows (status bar shows
    `op 26/76`), dead stubs (`GetHookThreadId`, `OperatorsCount`, `DisposeAndKeepAlive`,
    `RfKeyCode`, `HasChanges`) removed; duplicate publishes on toggle/disable/stop consolidated.
11. NumberBoxes hardened (`ValidationMode="InvalidInputOverwritten"`); invalid saves provably
    never touch state or file (verified live; tests cover atomicity).
12. Window close now disposes the engine (unhook + release held keys); UI dispatcher callback
    wraps handler exceptions in a log instead of surfacing them on the engine path.
13. Persistence testable without touching user data: `SettingsStore` takes a directory override
    (roundtrip, corrupted-file fallback, out-of-range clamp, apply-persists tests).
14. Accessibility: statuses and weapon rows read sensibly to UIA (weapon rows were raw record
    dumps; roster tiles and every NumberBox now carry automation names).
15. `ProfileSettings.Create` normalizes float32 noise from NumberBox round-trips
    (1.100000023841858 → 1.1) so persisted JSON matches user intent.
16. WrapPanel vertical rhythm fixed (row gap ignored the tiles' bottom margin).

## Remaining manual items (honest)
- Physical mouse-button behavior (MB3 toggle, MB4/5 navigation, LMB loops) requires a human with
  a real mouse and cannot be synthetically verified (by design, injected events are filtered).
- Maximized / very-narrow resize beauty pass.
- High-contrast (Windows HC) visual pass.
- In-game behavior (subject to per-game anti-cheat; explicitly not bypassed).
