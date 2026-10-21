# Implementation Plan — SixConsole Safe Update

## 1. Objective

Prepare the next SixConsole release while preserving the existing UI/UX and avoiding regressions. Add user-configurable keyboard shortcuts for ordinary application actions, keep current defaults unchanged, and support safe migration of user preferences.

**Important scope boundary:** The supplied Lua profile contains gameplay automation, including recoil compensation, rapid-fire behavior, and injected game inputs. This plan does not integrate, extend, tune, or distribute those behaviors. Lua support in this update is limited to validated, non-executable configuration data for the desktop application's own benign UI and profile-management features.

## 2. Non-negotiable Requirements

- Do not redesign, restyle, rearrange, or rename existing UI elements.
- Do not change the existing default shortcut or default behavior for any supported benign application action.
- Existing users must retain their settings after updating.
- New settings must be additive, backward-compatible, and reversible.
- Do not execute arbitrary Lua code or accept scripts that automate gameplay, synthesize mouse/keyboard input into another application, compensate recoil, or implement rapid fire.
- Do not silently change unsupported or invalid settings. Explain the issue and retain the last known-good configuration.
- Do not claim the release is ready until builds and regression tests pass on the supported platforms.

## 3. Phase A — Baseline and UI/UX Lock

1. Inspect the current repository, architecture, settings persistence, input abstraction, packaging workflows, and latest release tag.
2. Record the current default values and behavior for every existing benign shortcut/action before making changes.
3. Capture baseline screenshots of all relevant screens in light and dark mode, at supported window sizes and display scaling levels.
4. Add regression tests or visual snapshots where the existing test framework supports them.
5. Treat existing view markup, layout, spacing, typography, colors, navigation, and control placement as locked. The new configuration should use the existing Settings conventions; do not add a new screen unless the current architecture has no suitable settings location.

## 4. Phase B — Configurable Shortcuts

### User-facing behavior

- Add a shortcut editor to the existing Settings area without changing the overall layout or visual language.
- Support assigning a key or supported key combination to a benign application action, such as opening the app's settings or switching an application profile.
- Preserve the current default shortcut and its current behavior. Users who never open Settings should see no behavior change.
- Provide **Record shortcut**, **Save**, **Cancel**, and **Reset to default** behavior using the application's existing control patterns.
- Display the selected shortcut using a readable, consistent key label.
- Prevent accidental assignment of an empty shortcut, duplicate bindings for conflicting actions, or combinations reserved by the operating system where detectable.
- Provide a clear way to cancel shortcut capture with Escape, without saving a partial binding.
- Do not intercept keystrokes globally unless the existing application architecture and platform permissions explicitly support the intended benign app shortcut. Prefer app-scoped shortcuts. Any global shortcut must be opt-in, documented, and easy to disable.

### Technical design

1. Create a typed shortcut model rather than storing arbitrary strings. Include the primary key, modifiers, scope, action identifier, and schema version.
2. Put shortcut registration and dispatch behind a single service so UI code does not register competing handlers.
3. Use platform-native key APIs through the existing platform abstraction. Do not create unbounded threads, busy loops, or per-keystroke allocations.
4. Register shortcuts when the application is ready and unregister them reliably when settings change, the window closes, or the application exits.
5. Detect conflicts before committing a change. If registration fails, show a clear error and restore the last working binding.
6. Persist only validated shortcut definitions. On startup, if settings are malformed, use the last known-good/default configuration and record a diagnostic without crashing.
7. Keep keyboard focus and accessibility intact while shortcut recording is active. Do not capture text entered into unrelated controls.

## 5. Phase C — Safe Lua Configuration Import

The supplied Lua file is executable automation code, so do not load it with `dofile`, `load`, `require`, or an equivalent runtime execution path.

For any profile-import support in this release:

1. Define a documented, non-executable schema for benign app preferences only (for example, display labels, profile names, and UI preferences that the application already supports).
2. Parse input as data using a restricted parser or a dedicated JSON-based interchange format; never evaluate user-supplied code.
3. Validate every field against an allowlist, type constraints, length limits, and supported schema versions.
4. Reject executable callbacks, event handlers, mouse/keyboard injection commands, timing loops, recoil values, rapid-fire settings, and unknown fields that can affect input automation.
5. Show a preview of accepted settings before import. Make import transactional: either all validated settings are applied, or none are.
6. Back up the current settings before import and offer a restore path if validation or migration fails.
7. Provide actionable validation errors without printing secrets or dumping arbitrary file contents into logs.

If the new profile cannot be represented using the safe schema, report it as unsupported instead of partially importing it or silently ignoring unsupported behavior.

## 6. Phase D — Settings Migration and Compatibility

- Add a schema version to persisted settings.
- Migrate old settings using explicit, deterministic migration steps.
- For existing installations, preserve every current default and user customization unless the user explicitly changes it.
- If a new shortcut field is absent, use the existing default shortcut; do not substitute a new default.
- Write settings atomically (temporary file followed by replace) where supported, and keep a recoverable backup.
- Test upgrades from the current released version as well as clean installs.

## 7. Phase E — Reliability and Resource Use

- Keep event handlers short and non-blocking.
- Avoid synchronous file I/O inside input callbacks and UI event handlers.
- Reuse a bounded input/event dispatcher where needed; do not spawn a new thread for each event.
- Ensure cancellation and shutdown paths release registrations, event subscriptions, handles, and timers.
- Add defensive error handling around settings parsing, shortcut registration, and import operations.
- Log failures with enough context for diagnosis while avoiding high-frequency log spam.
- Do not introduce background polling when event-driven APIs are available.

## 8. Required Test Plan

### Shortcut tests

- First launch: existing default shortcut remains unchanged.
- Existing installation upgrade: old settings and behavior remain intact.
- Record and save a valid key, modifier combination, and supported shortcut.
- Cancel recording without changing the saved shortcut.
- Reset to default and confirm the original default is restored.
- Duplicate/conflicting binding is rejected with an explanation.
- Unsupported key, malformed settings, and registration failure do not crash the app.
- Shortcut registration is removed or updated correctly after changing settings and during shutdown.
- Confirm app-scoped shortcuts do not capture text or interfere with ordinary typing in other applications.

### Import and migration tests

- Valid benign configuration imports successfully.
- Invalid types, unknown schema versions, oversized files, malformed data, and duplicate values are rejected safely.
- Executable Lua and input-automation instructions are never executed.
- Failed imports leave the current configuration unchanged.
- Rollback restores the prior settings.

### UI/UX regression tests

- Compare baseline and updated screenshots. No existing layout, typography, color, spacing, icon, navigation, or theme changes are permitted.
- Verify light/dark themes, window resizing, display scaling, keyboard navigation, and accessibility labels.
- Verify the new settings use existing design patterns and do not move or restyle unrelated controls.

### Release-quality tests

- Run all unit, integration, and UI tests available in CI.
- Build each currently supported target from a clean checkout.
- Verify startup, settings persistence, restart, upgrade, and clean uninstall/install paths.
- Check crash logs and resource use during a representative extended session.
- Confirm that failed registration/import actions recover gracefully and leave the application usable.

## 9. Phase F — Release Process

1. Review the diff and confirm that changes are limited to shortcut settings, safe config validation, migrations, tests, and documentation.
2. Update the changelog with only verified changes and known limitations.
3. Run formatting, static analysis, dependency/security checks, the full test suite, and clean release builds.
4. Confirm version metadata is consistent across the application, installer/archive names, and release notes.
5. Create a release candidate and test it on a clean machine or VM for each supported operating system.
6. Publish only after all release gates pass. Attach checksums generated from the final artifacts and include installation/upgrade instructions.
7. Keep the previous release available as a rollback option. If critical regressions appear, pause distribution and publish a corrected build rather than recommending unsafe workarounds.

## 10. Definition of Done

- Existing UI/UX and defaults are unchanged except for the additive shortcut controls in the existing Settings area.
- Users can set, save, cancel, reset, and validate shortcuts for supported benign app actions.
- Existing user settings survive upgrade and malformed configuration cannot crash startup.
- Import is restricted to validated non-executable configuration data.
- No gameplay automation, recoil compensation, rapid-fire, or input injection into a game is implemented or distributed by this update.
- All required tests pass on supported targets, the release artifacts are reproducible, and release notes accurately describe the tested changes.
