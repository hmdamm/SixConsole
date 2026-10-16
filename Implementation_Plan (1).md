# Implementation Plan — Standalone Windows Logitech Mouse Profile App

## 0. Instruction to the AI Agent

Read this document completely before making changes. Treat it as the project's persistent product specification, not as a loose suggestion. First inspect the provided Logitech Lua script and the current repository, then implement the application end to end. Do not stop after producing a mockup, a plan, or a visually convincing but non-functional prototype.

Before coding, inspect the skills, design references, project instructions, Windows documentation, and repository conventions available in your environment. Explicitly locate, read, and **actually apply** each of these named skills/workflows where available: `anti-vibe-desktop-core`, `frontend-design`, `impeccable`, `audit`, `pro-utility-monochrome`, `ui-ux-pro-max`, and `windows-fluent-native`. Treat them as complementary requirements: native desktop engineering; deliberate layout and visual composition; visual polish and consistency; implementation/accessibility/quality auditing; a restrained utility-focused monochrome visual system; systematic UX patterns and design tokens; and Windows Fluent interaction conventions. Do not just mention their names in a report—use their guidance during design, implementation, and review. If a skill is unavailable, state that explicitly and use the closest available documented alternative; never pretend to have loaded or applied it. If your environment supports project memory, save durable requirements in its supported project-instructions file (for example, `AGENTS.md` or the equivalent) and point it back to this file. If true persistent memory is not available, do not claim that you have saved anything to hidden memory: keep this document as the source of truth and reread it at each major phase.

The goal is to turn the supplied Logitech mouse Lua profile into a **standalone Windows desktop application** with the original profile's features and behavior preserved, while replacing the Logitech-software-only workflow with an independent, maintainable Windows application and a carefully designed native interface.

## 1. Product Goal

Build a polished **Windows desktop utility** that lets the user select an operator, select a weapon/loadout, view and edit the profile's settings, enable or disable the profile, and use the original mouse-button/keyboard workflow where Windows permits it. Use Windows-native windowing, packaging, accessibility, keyboard/focus behavior, and Fluent UI conventions; a browser-based interface is not an acceptable substitute.

The provided Lua script is the **canonical source of truth** for the initial profile data and behavior. Do not omit operators, weapons, settings, recoil values, rapid-fire flags, timing ranges, navigation rules, status information, or input mappings. Do not silently rebalance, normalize, or "improve" the profile numbers. If any source behavior is unclear, document the ambiguity and preserve the closest faithful interpretation rather than inventing new behavior.

This must be a **real, standalone Windows desktop application** with a native Windows windowing and interaction model. It must not be a website wrapped in a window, an Electron-style web wrapper chosen merely for convenience, or a thin GUI that displays hard-coded sample data. Prefer **WinUI 3 + Windows App SDK + C#/.NET** for a genuinely Windows-native implementation unless repository constraints provide a well-evidenced reason to use another native Windows stack. Explain any deviation before implementation.

## 2. First Steps: Inspect Before Implementing

1. Locate and read the entire supplied Lua/text file, including the complete `OPERATORS` table and event handler.
2. Inspect the repository, existing build system, supported project conventions, installed design/engineering skills, and the target operating system.
3. Identify and verify the Windows APIs required for global mouse/keyboard observation, modifier-state detection, and relative pointer movement. Prefer documented Windows mechanisms such as Raw Input, appropriate Win32 input hooks, and `SendInput` where applicable. Confirm behavior and permission requirements on the actual Windows version before promising support.
4. Produce a short internal architecture note and a design brief before implementation. Record key constraints, permissions, chosen stack, design direction, and trade-offs.
5. Create a source-to-feature checklist and a data-integrity test plan. Keep the original Lua/text source unchanged as a reference fixture.

Do not assume Logitech G HUB/LGS APIs are available outside Logitech software. Build a Windows-specific platform adapter using documented, supported Windows input APIs, and clearly communicate limitations and required permissions. Do not bypass anti-cheat systems, hide the app from security software, inject into game processes, or claim compatibility with every game or Windows configuration without verification. The app must not interfere with input when disabled and must provide an obvious emergency-disable path.

## 3. Requirements for Preserving the Lua Profile

### 3.1 Initial global values

Migrate these defaults exactly from the source:

- `isEnabled = false`
- `currentOpIndex = 1`
- `currentGunIndex = 1`
- `recoilMultiplier = 0.9`
- `vertVariance = 1`
- `horizJitter = 1`
- `minDelay = 2` milliseconds
- `maxDelay = 5` milliseconds
- `rfKey = "p"`
- `minHold = 25` milliseconds
- `maxHold = 40` milliseconds
- `minGap = 65` milliseconds
- `maxGap = 90` milliseconds

Keep the source's rounding behavior equivalent to `floor(value + 0.5)` for the effective pull calculation. Do not replace it with a language/runtime rounding function unless tests prove identical behavior for the supported input range.

### 3.2 Operator and weapon catalogue

- Import the **entire** operator roster and every weapon entry from the source table, including entries marked `ATK` and `DEF`.
- Preserve the original operator names, side labels, weapon names, order, `pull` values, and `rapidFire` booleans exactly as supplied.
- Do not hand-retype or trim the roster. Prefer a single normalized data file (JSON, TOML, or the chosen stack's equivalent) generated or transcribed with automated checks against the source.
- Give every operator and weapon a stable identifier independent of its display name or list position.
- Preserve legitimate repeated weapon names as separate operator/loadout entries. Do not deduplicate them globally.
- Add automated integrity checks: number of operators, number of weapon entries, ordering, names, side labels, `pull`, and rapid-fire flags must match the source. Report intentional data migrations explicitly.
- Keep profile data separate from rendering and input-handling code so later game/profile updates do not require UI rewrites.

### 3.3 Core interaction semantics

Preserve the source workflow as the default mapping, while allowing users to customize it in Settings:

- **Mouse button 3:** toggle the profile's enabled/disabled state.
- **Mouse button 5:** when Caps Lock is off, advance to the next operator and reset the selected weapon to the first weapon; when Caps Lock is on, advance to the next weapon for the selected operator.
- **Mouse button 4:** when Caps Lock is off, go to the previous operator and reset the selected weapon to the first weapon; when Caps Lock is on, go to the previous weapon for the selected operator.
- **Left mouse button:** while enabled, run the selected weapon's configured behavior while the button remains held; stop promptly when it is released.
- **Caps Lock:** acts as the default modifier that changes the meaning of buttons 4 and 5 from operator navigation to weapon navigation.
- Keep the selected operator, side, weapon, enabled state, and relevant timing/mode details visibly synchronized with actual application state.
- Handle list wraparound correctly at the first/last operator and weapon.
- Avoid stuck keys, stuck mouse buttons, runaway input loops, or background processing after release, disable, application shutdown, permission loss, or an input-backend error.

If the platform cannot safely observe a hardware button or modifier, provide an explicit configurable alternative and explain the limitation. Never represent an unsupported mapping as functional.

### 3.4 Per-weapon behavior

Preserve the two modes represented by `rapidFire`:

- **Regular/full-auto recoil mode (`rapidFire = false`):** while the left mouse button is held and the profile is enabled, apply the source's configured vertical movement with the same variance/jitter model and delay range.
- **Rapid-fire mode (`rapidFire = true`):** preserve the configured secondary-key press/release timing (`rfKey`, `minHold`, `maxHold`, `minGap`, `maxGap`) together with the source's movement, variance, jitter, and stop conditions.
- Use the selected weapon's source `pull` value and global multiplier to calculate effective pull. Apply the source's random vertical variance and horizontal jitter within their configured bounds.
- Keep timing units explicit and store them consistently in milliseconds.
- Validate ranges: minimums must not exceed maximums, values must be finite and within documented platform limits, and invalid edits must not be applied to the live input engine.
- Make it clear which settings are global and which values belong to a particular weapon. Do not silently introduce per-weapon overrides unless that feature is deliberately designed and documented.

### 3.5 Status and diagnostics

Replace the original console-only `LogStatus()` output with an always-readable UI status area that preserves its informational purpose:

- Enabled/disabled state.
- Current sensitivity/recoil multiplier and rapid-fire key.
- Attacker and defender rosters, with the selected operator clearly indicated.
- Selected operator, side, selected weapon, current weapon index, and total weapons for that operator.
- Mode (`Rapid Fire` or `Full-Auto Recoil`) and the effective pull value.
- Relevant timing ranges for the selected mode.
- Useful errors, permission problems, and input-backend status.

The app does not need to imitate terminal ASCII alignment. Present the same information using readable, accessible Windows-native interface components.

## 4. Settings and Persistence

Provide a real settings workflow, not decorative controls:

- Editable global settings for recoil multiplier, vertical variance, horizontal jitter, regular-mode delay min/max, rapid-fire key, rapid-fire hold min/max, and rapid-fire gap min/max.
- Clear unit labels beside timing values.
- Restore-defaults action based on the exact source defaults, with confirmation if user-edited values would be lost.
- Save settings between restarts using an established, safe local configuration mechanism.
- Provide a persistent `System / Light / Dark` theme preference (at minimum Light and Dark must be fully implemented); apply changes immediately and save the preference locally.
- Restore the last selected operator and weapon on launch only if this is consistent with the app's intended behavior; document the choice. The first-run defaults must still match the source.
- Validate edits inline and provide understandable error messages.
- Apply changes atomically so invalid or half-saved configurations cannot leave the input engine in an unknown state.
- Include import/export of the app's own profile/config format if practical. Never overwrite the canonical source or another profile without explicit user action.

## 5. UI/UX: High-Quality, Deliberate Product Design

**The visual result is a first-class requirement. Do not produce a generic AI-generated-looking dashboard.** The app must look like a real desktop utility designed by an experienced product designer, with a coherent visual system and purposeful interaction design.

### 5.1 Design process required before UI implementation

1. Load and apply the exact named skills `anti-vibe-desktop-core`, `frontend-design`, `impeccable`, `audit`, `pro-utility-monochrome`, `ui-ux-pro-max`, and `windows-fluent-native`, plus relevant official Microsoft Windows App SDK / WinUI / Fluent Design guidance. Record which files or workflows were actually inspected and how their guidance affected decisions. Do not merely mention a skill; actually apply it. If a skill cannot be found in the environment, document that fact and do not claim it was used.
2. Establish the app's information architecture, user journeys, hierarchy, spacing scale, typography, color tokens, component states, theme tokens for Light and Dark, native Windows navigation/window patterns, and interaction rules before coding the UI.
3. Create a compact design brief and wireframes or a page/component map. If the environment supports visual preview, inspect the actual rendered interface and iterate on it.
4. Build a small, consistent design system with reusable components and semantic Light/Dark theme resources. Use native WinUI controls and Windows-native navigation, focus, keyboard, title-bar, menu, and dialog conventions wherever appropriate. Avoid styling every screen independently or recreating native controls unnecessarily.
5. Test the UI at realistic Windows desktop window sizes, including maximized, normal, narrow/resized, and high-DPI/scaled displays where available. Verify native resize behavior, title-bar integration, window min/max sizing, clipped controls, overflow, awkward empty space, inconsistent alignment, and unreadable dense lists.
6. Run a dedicated design and implementation audit using the `audit` skill (if available): inspect every screen and state, keyboard/focus paths, accessibility, theme parity, consistency, error handling, and whether every control is genuinely functional. Fix high-impact findings before proceeding.

### 5.2 Visual direction

Aim for a **premium, restrained Windows control utility**: precise, calm, purposeful, and easy to scan. Apply `pro-utility-monochrome` for a disciplined, utility-first visual foundation and `windows-fluent-native` for authentic Windows interaction patterns, native window behavior, and Fluent conventions. The app should have its own identity and must not depend on copied proprietary assets.

- Use a coherent neutral base palette with one restrained accent color, meaningful status colors, and accessible foreground/background contrast. Keep the utility-focused monochrome foundation disciplined while reserving color for clear state, selection, validation, and system-status meanings.
- Establish clear type hierarchy, carefully tuned spacing, consistent alignment, useful labels, and disciplined icon usage.
- Prefer clean surfaces, subtle borders, and purposeful depth over decorative effects.
- Use motion only where it communicates a state change; keep transitions fast, subtle, and respectful of reduced-motion preferences.
- Provide **fully supported Light and Dark themes as first-class requirements**, not as a later enhancement. Also provide a **System** option that follows the Windows app/system theme when feasible; use System as the initial default unless the existing project has a documented convention. Include an obvious theme selector in Settings, apply theme changes immediately without restarting, persist the selection across launches, and ensure every screen, dialog, menu, tooltip, validation message, focus indicator, and disabled/selected state works in both themes. Check contrast and readability separately in Light, Dark, and System modes.
- Use authentic Fluent System Icons or another consistent Windows-appropriate icon set; do not use emoji as interface icons. Prefer native control visuals and familiar Windows interaction patterns over custom imitations.
- Do not use gratuitous gradients, neon glows, glassmorphism everywhere, huge rounded cards, giant headings, excessive pills, decorative charts, random icons, or a template-like "AI dashboard" layout.
- Do not add fake analytics, fake hardware telemetry, invented battery/connection metrics, meaningless charts, or placeholder activity feeds.
- Avoid a wall of controls. Group settings by purpose and show expert-level values in a readable, compact layout.
- Use consistent corner radii and shadows; avoid mixing many different design styles.

### 5.3 Suggested information architecture

Adapt this structure based on the best design after inspecting the full feature set, but retain every required workflow:

**A. Main / Profile view**
- A clear global enabled/disabled control with unmistakable state and an emergency-disable action.
- Searchable operator roster, separated by Attacker and Defender groups/tabs or an equally clear design.
- A selected-operator detail area with the weapon list for that operator.
- Obvious active selection for both operator and weapon.
- A compact, clearly labeled summary of the active mode, effective pull, and the timing range that applies to it.
- A small, persistent status area for active state, input permissions/backend health, and errors.

**B. Settings view**
- Global recoil/movement settings in a logical group.
- Regular/full-auto timing settings in their own group.
- Rapid-fire key and timing settings in their own group.
- Input mappings and modifier behavior in their own group.
- Save/apply, cancel/revert, and restore-defaults actions with predictable behavior.

**C. Help / Diagnostics view or section**
- Explain the current input backend and its permission requirements.
- Show actionable troubleshooting guidance without exposing unnecessary raw logs by default.
- Explain the default mouse-button/Caps Lock workflow and how to disable the feature quickly.

A sidebar is optional, not mandatory. Choose the layout that best serves the actual workflows and available window size. Do not add navigation items that have no meaningful function.

### 5.4 Interaction quality

- Prioritize the common flow: enable/disable, find operator, select weapon, verify mode/settings.
- Make current selection obvious without relying on color alone.
- Use tooltips only for genuinely useful supplemental explanations, not to hide essential labels.
- Use consistent focus indicators, logical keyboard navigation, accessible labels, and appropriate hit targets.
- Include empty, loading/initializing, permission-required, disabled, active, invalid-input, error, and stopping states where applicable.
- Avoid modal dialogs for routine actions; reserve confirmation dialogs for destructive or potentially disruptive operations.
- Reflect state changes immediately and consistently across roster, weapon detail, status, and settings.
- Do not use a button or toggle that looks interactive but is not wired to real behavior.

### 5.5 Explicit anti-patterns to avoid

The result is unacceptable if it resembles a default component-library starter template with superficial colors applied. Do not create a generic dashboard made of identical cards. Do not implement the UI from a one-line prompt without design exploration. Do not treat visual design as a final cosmetic pass. Do not use placeholder data once the real source table is available. Do not claim success based only on a screenshot or successful frontend build.

## 6. Architecture and Platform Integration

Target **Windows desktop only** for this implementation. Inspect the repository and environment, then choose a maintainable Windows-native stack. The default preference is **C#/.NET with WinUI 3 and Windows App SDK**, using native Fluent controls and documented Win32 APIs for input integration where needed. WPF may be considered only if there is a concrete repository or compatibility reason, and it must still meet the design and accessibility requirements. Do not choose a browser-based stack or web wrapper simply because it is faster to scaffold. Record the chosen stack and evidence-based rationale before implementation. Do not spend effort adding Linux or macOS adapters; explicitly identify those platforms as out of scope.

Use a separation of concerns similar to the following, with Windows-native services and lifecycle handling:

- **UI layer:** screens, components, user feedback, settings forms, theme, accessibility.
- **Domain/profile layer:** typed operator/weapon schema, validation, source-derived profile data, calculations, navigation rules.
- **Input engine:** event loop, press/release lifecycle, timing, movement and key-emission behavior.
- **Platform adapter:** Windows-specific mouse/keyboard APIs, permissions, input-device detection and capability reporting.
- **Persistence layer:** settings load/save, migrations, profile import/export.
- **Diagnostics layer:** structured, user-safe error reporting and optional debug logs.

The input engine must not depend on UI component state. UI interactions should call a well-defined application service, and the engine should publish state changes back to the UI using a safe, typed event/state mechanism.

### Windows-only platform rules

- The target is a standalone **Windows 10/Windows 11 x64 desktop app**, but only claim a Windows version as supported after building and testing on it. Prefer validating the current Windows environment first.
- Use supported Windows input APIs and handle application focus, global input observation, device changes, cancellation, and shutdown correctly. Keep physical input events distinguishable from synthetic events.
- Explain any Windows security prompts, permissions, or elevation needs. Do not request administrator privileges unless a documented technical requirement makes them unavoidable; explain the reason before enabling the privileged behavior.
- Fail safely when input capabilities are missing: disable automation, show actionable guidance, and keep profile browsing and settings usable.
- Do not implement stealth, anti-cheat evasion, process injection, or hidden input behavior intended to evade security controls.
- Linux and macOS support are out of scope for this deliverable. Do not claim cross-platform compatibility.

## 7. Reliability, Safety, and Lifecycle

- Provide a highly visible emergency stop/disable control accessible without relying on the same mouse-button mapping that might be malfunctioning.
- On application exit, backend failure, permission loss, device disconnect, or unexpected exception, stop all loops and release any keys/buttons emitted by the app.
- Use cancellation-aware timing loops and explicit lifecycle states; avoid busy-waiting and uncontrolled background threads/tasks.
- Prevent duplicate event handlers, repeated starts, input recursion, and UI/backend race conditions.
- Do not perform input emulation while the profile is disabled.
- Keep a clear distinction between physical input events and synthetic events so that generated input cannot recursively trigger the same automation.
- Handle rapid press/release cycles and application shutdown deterministically.
- Log enough diagnostic information to debug failures but avoid collecting or uploading telemetry. No network connection should be required for core functionality.
- Explain that compatibility with games and their rules varies. Do not add anti-cheat bypasses or promise that use is permitted in a specific title.

## 8. Testing Strategy

Use automated tests wherever possible and manual tests for Windows input behavior. At minimum, add:

### Data integrity
- Every operator and weapon from the Lua source is present exactly once in the correct place, with source order preserved.
- Exact counts of operators and weapons match the source.
- Every `side`, weapon `name`, `pull`, and `rapidFire` value matches the source.
- Default settings match the source values listed in this document.

### Domain and navigation
- Effective pull calculation and rounding match the source.
- Minimum/maximum timing validation works.
- Next/previous operator and weapon navigation wraps correctly.
- Changing operator resets the selected weapon to index 1, as in the source.
- Caps Lock modifier behavior matches the source defaults.

### Input lifecycle
- Disabled state emits no synthetic input.
- Left-button release stops the active loop promptly.
- Disable, emergency stop, device disconnect, backend error, and app shutdown stop activity and release emitted keys/buttons.
- Rapid-fire press/release and delay ranges stay within configured bounds (subject to documented OS scheduling granularity).
- No duplicate event processing or synthetic-event recursion occurs.
- Missing permissions result in a safe, understandable state rather than a crash or false success.

### Settings and persistence
- Valid values persist across restart.
- Invalid ranges are rejected without partially updating the engine.
- Restore defaults exactly matches the source defaults.
- Import/export, if implemented, validates profile schema/version and fails safely on malformed files.

### UI and UX
- All visible controls work and reflect the real application state.
- Operator search/filtering does not change underlying source ordering or data.
- Keyboard navigation and focus visibility work.
- Light, Dark, and System-following themes remain readable, complete, and visually coherent; theme changes apply immediately and persist after restart.
- Native Windows keyboard navigation, focus indicators, title-bar/window behavior, and high-contrast/accessibility settings are reviewed.
- Resizing the window does not clip essential controls.
- Review screenshots of the real running app at representative sizes; iterate on spacing, hierarchy, contrast, selected states, and empty/error states.

## 9. Build, Packaging, and Documentation

- Provide a reproducible development setup and a clear build command.
- Package the app for Windows using an established method appropriate to the chosen stack (for example, MSIX/MSIXBundle or a self-contained Windows installer). Choose one distribution path, document prerequisites and signing limitations, and verify the produced package on Windows.
- Do not claim Windows support solely because shared code compiles. Verify the Windows app, input adapter, startup/shutdown lifecycle, theme behavior, and installer/package on the actual target environment. Cross-platform release is not required.
- Include a concise README with features, supported Windows versions, prerequisites, permissions/security prompts, installation/build/packaging steps, usage, default controls, configuration storage location, Light/Dark/System theme behavior, known limitations, troubleshooting, and uninstall instructions.
- Include a short note documenting how the original Lua semantics were mapped to the standalone application.
- Keep secrets and unrelated user data out of the repository. Do not add network telemetry or account sign-in for a local utility unless a future requirement genuinely needs it.

## 10. Implementation Phases and Deliverables

### Phase 1 — Source audit and design
- Read the complete source.
- Record the full feature/input mapping and profile-data integrity counts.
- Inspect and apply the seven named design/engineering skills, review Microsoft Windows Fluent/WinUI guidance, and select the Windows-native stack/input adapter.
- Produce the design brief, interaction map, component inventory, and initial wireframes.

**Deliverable:** source-to-feature checklist, architecture note, and design specification.

### Phase 2 — Domain model and migration
- Create the typed operator/weapon schema and migrate the complete catalogue.
- Add validation and automated comparison/integrity tests against the source.
- Implement selection/navigation and calculation logic independently of UI.

**Deliverable:** tested domain layer with source-faithful profile data.

### Phase 3 — Functional input backend
- Implement the Windows-specific input adapter for the verified Windows target.
- Add input lifecycle handling, default mappings, cancellation, emergency stop, and permission reporting.
- Test disabled, active, stop, failure, and shutdown conditions before connecting the full UI.

**Deliverable:** operational, safely stoppable input engine and documented platform constraints.

### Phase 4 — Designed UI and persistence
- Implement the design system and screens from the approved design brief.
- Connect every control to the real domain/backend services.
- Add persistence, validation, restore defaults, status updates, and troubleshooting.
- Iterate using screenshots of the actual running app rather than trusting code inspection alone.

**Deliverable:** complete standalone Windows application with no placeholder data or inert controls, including fully working Light and Dark themes.

### Phase 5 — Verification and handoff
- Run automated tests, build/package checks, manual input lifecycle tests, and UI review at several window sizes.
- Resolve high-impact visual issues before declaring completion.
- Update README and known-limitations documentation.

**Deliverable:** runnable Windows package, source code, tests, build instructions, and a completion report listing what was implemented, what was tested, which requested skills were applied, and anything not supported.

## 11. Definition of Done

The task is complete only when all of the following are true:

- [ ] The app runs independently of Logitech G HUB/LGS on the verified Windows target.
- [ ] All operators and weapon entries in the source are migrated and automatically verified.
- [ ] Original defaults, values, modes, calculations, navigation, modifier behavior, and status information are preserved.
- [ ] The profile can be enabled/disabled and the actual input backend responds to real state changes.
- [ ] All settings are editable, validated, saved, and restorable to source defaults.
- [ ] Every visible UI control is functional; there are no mock actions or placeholder catalogue entries.
- [ ] The interface demonstrates deliberate Windows-native product design, a reusable design system, accessible states, consistent spacing/type/color, and careful visual iteration.
- [ ] Light and Dark themes are fully implemented, selectable, persisted, and visually audited; System theme following is implemented if supported by the chosen stack.
- [ ] The seven requested skills were inspected and applied where available, and unavailable skills are explicitly disclosed rather than falsely claimed.
- [ ] The interface avoids the common generic "vibe-coded" look described in Section 5.5.
- [ ] Emergency stop, shutdown, release handling, permission failure, and backend failure have been tested.
- [ ] The actual Windows build and distributable package have been tested on the verified Windows target; Linux and macOS are clearly identified as out of scope.
- [ ] Documentation explains Windows setup, permissions/security prompts, usage, default mappings, theme selection, limitations, and troubleshooting.
- [ ] The final report is honest about any gaps and identifies which of the seven named skills were actually available and applied. Do not mark any checklist item complete without evidence.

## 12. Final Agent Response

When implementation is finished, report:

1. The chosen Windows-native stack and the evidence-based reason for choosing it.
2. The verified Windows version/architecture and the exact Windows packaging output.
3. The preserved features and data-integrity test results.
4. The automated and manual tests actually performed.
5. The UI/UX and accessibility work completed, including which of `anti-vibe-desktop-core`, `frontend-design`, `impeccable`, `audit`, `pro-utility-monochrome`, `ui-ux-pro-max`, and `windows-fluent-native` were actually inspected/applied, plus the Windows/Fluent guidelines used.
6. Any platform limitations, permission requirements, or incomplete items.
7. Exact commands to run, test, build, and package the app.

Be precise and evidence-based. Do not report a build, test, permission check, UI review, or platform as successful unless it was actually verified.
