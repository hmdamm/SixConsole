# CRITICAL TASK OVERRIDE — Stability, Crash, and Performance Remediation

## Highest-Priority Instruction for This Maintenance Pass

**The current application crashes after approximately 2–3 minutes of use and can make the entire Windows system unresponsive until the computer is restarted. Treat this as a critical stability defect, not a cosmetic issue.** Your task is to find and fix the actual root causes, reduce unnecessary resource usage, and verify that the application remains stable.

This is a **stability-only maintenance task**. The existing application is the product baseline. The later sections of this document describe the intended features and behavior that must be preserved; any older UI/UX redesign recommendations in this document are **not authorization to redesign the current interface during this task**.

### Non-Negotiable Preservation Rules

Do not change any of the following unless a change is strictly necessary to fix a demonstrated defect and is documented with a clear reason:

- The current UI/UX, visual appearance, layout, navigation, typography, spacing, colors, icons, animations, window layout, or wording.
- The application's product identity, supported functionality, feature set, configuration options, profile contents, defaults, operator/weapon data, or documented specifications.
- User-visible workflows, controls, shortcuts, mouse/keyboard mappings, selection order, profile behavior, timing values, or expected input behavior.
- Supported operating systems, existing platform targets, packaging formats, or release workflows.
- Existing user settings and data, configuration-file formats, and compatibility with existing installations.

Do not restyle components, replace the design system, add dashboards, remove features, simplify the product by dropping functionality, or perform unrelated refactoring. **The goal is to fix the existing application, not redesign or rebuild it.**

Internal architecture, resource handling, concurrency, event processing, platform adapters, and implementation details may be changed as necessary to eliminate defects, but externally observable behavior must remain equivalent for valid inputs. If a proposed fix could change a specified behavior, explain the conflict and choose the least disruptive correction. Do not silently change behavior.

If a UI change is genuinely unavoidable to correct a defect, keep it as small as possible, preserve the existing styling and geometry, and document exactly why it was required. No voluntary visual changes are permitted.

## 1. Mission and Required Outcome

Investigate, reproduce safely, diagnose, and fix the application's crashes, hangs, runaway processing, leaks, deadlocks, race conditions, and other defects. Make the application as lightweight as reasonably possible without reducing functionality or reliability.

Do not assume the cause. Inspect the real repository and runtime behavior, collect evidence, trace the failure to its source, and implement targeted fixes. Avoid speculative rewrites and broad architectural changes unless measurements demonstrate that they are necessary.

Do not claim that every bug is fixed merely because the application compiles or launches. A successful build is only one verification step.

## 2. Inspect the Existing Application Before Editing

1. Read this entire document and the repository's actual project instructions before changing code. Treat this override as the highest-priority scope constraint for the current task.
2. Inspect the source code, dependencies, build scripts, background workers, input hooks, timers, polling loops, native bindings, event subscriptions, and platform-specific code.
3. Inspect existing logs, crash reports, exception reports, test results, and available diagnostics. If no diagnostics exist, add narrowly scoped, low-overhead diagnostics without modifying the UI.
4. Identify all long-running tasks, threads, tasks/futures, timers, callbacks, native handles, device handles, file watchers, inter-process communications, and event hooks. Document who creates, owns, cancels, and disposes each one.
5. Establish a baseline for memory, CPU usage, thread count, handle count, input-event frequency, startup time, and responsiveness using the tools available in the environment.
6. Record the existing UI appearance and critical behavior before modifications so they can be compared after the fixes.
7. Reproduce the reported failure if it is safe and feasible. Prefer a controlled test environment and simulated input before using real system-level input integration.
8. Create a concise root-cause and remediation plan. Then implement and test the fixes in the current repository; do not stop after writing recommendations.

Do not guess the framework, operating-system APIs, or root cause. Verify these from the actual codebase.

## 3. Critical Windows Stability and System-Hang Investigation

Because the reported failure can affect the entire operating system, investigate system-level impact as a priority. Distinguish an application crash from a full desktop freeze, input subsystem overload, or system-wide resource exhaustion.

Inspect for, at minimum, the following possible causes where applicable:

- Infinite or runaway loops, excessive-frequency polling, busy-waiting, or loops that fail to observe cancellation promptly.
- High-frequency mouse/keyboard input callbacks that recurse, trigger one another, or generate feedback loops.
- Input hooks, event listeners, native input injection, device interfaces, or OS handles that are not reliably released.
- Deadlocks, lock contention, race conditions, re-entrant callbacks, or unsafe calls between UI and worker threads.
- Blocking sleeps, synchronous I/O, waits, or lengthy native calls on the UI thread or inside system event callbacks.
- A new thread, timer, hook, or task being created repeatedly instead of reusing or properly disposing of an existing one.
- High-priority or real-time scheduling settings that starve normal operating-system work.
- Event queues that grow without bounds, duplicate subscriptions, unbounded logs, or uncontrolled message dispatch.
- Memory leaks, native allocations that are never released, handle leaks, unmanaged-resource leaks, and excessive temporary-object creation.
- Timer re-entrancy, overlapping periodic jobs, cancellation races, unsafe shutdown order, or callbacks firing after object destruction.
- Platform interop errors, architecture mismatches, exception propagation across native boundaries, or invalid lifetime management.
- Configuration reloads or settings changes that leave a background processor in an inconsistent state.
- Repeated file writes, excessive disk logging, uncontrolled IPC, or other activity that grows over time.
- Incorrect behavior when a mouse button is released, the app is disabled, the device disconnects, permissions change, or the application exits.

These are investigation leads, not assumptions. Use profiling, logs, tests, and source analysis to establish which defects actually exist.

### System-Safety Rules During Testing

- Do not intentionally reproduce a known whole-system freeze on the user's primary machine.
- Prefer unit tests, simulated input, a dedicated test harness, a VM, or another isolated test environment for risky regression tests.
- Do not introduce kernel drivers, install system services, elevate privileges automatically, disable Windows protections, or make unrelated operating-system changes as a workaround.
- Do not use extreme process priorities, uncontrolled input injection, unbounded polling, or hidden persistent hooks to mask the defect.
- Put hard upper bounds on internal event queues and work scheduling where appropriate; handle overload explicitly rather than allowing runaway work.
- Ensure all application-owned hooks and native resources have explicit lifetimes and deterministic cleanup.
- On internal failures, permission loss, device disconnect, cancellation, or shutdown, fail safely and stop the affected background activity instead of leaving a process running uncontrollably.
- Preserve the application's existing user-visible behavior and controls. Do not add a new screen or redesign the UI to work around an internal reliability defect.

## 4. Concurrency, Input, and Lifecycle Requirements

Review all background processing and platform integrations. Apply only the changes needed to make the implementation safe and deterministic.

- Keep the UI thread responsive. Long-running work, waits, and potentially blocking native operations must not execute synchronously on the UI thread.
- Use a clearly owned worker/task lifecycle with cancellation and bounded shutdown behavior.
- Do not spawn a worker, timer, hook, or polling loop for every input event unless the code demonstrably requires it and its lifetime is bounded.
- Ensure repeated start/stop or enable/disable operations cannot create duplicate workers or duplicate event subscriptions.
- Ensure event handlers are unsubscribed exactly as required during disable, reinitialization, and shutdown.
- Make shared mutable state thread-safe using the smallest suitable synchronization mechanism. Avoid holding locks while calling external/native APIs or waiting for another thread.
- Prevent callbacks from using disposed objects. Guard lifecycle transitions, and make cleanup safe to call more than once where appropriate.
- Handle exceptions inside long-running workers and native callback boundaries so one failure cannot leave uncontrolled activity behind.
- Use monotonic timing for elapsed-time calculations where the runtime supports it. Do not assume timers can accurately deliver arbitrarily small intervals; respect real platform/runtime limits without silently changing configured values.
- Ensure input processing stops promptly on release, disable, cancellation, app exit, and relevant backend failure. Release any key/button state or resource owned by the app when the relevant lifecycle ends.
- Prevent app-generated events from unintentionally retriggering its own listeners. Distinguish injected and physical events where the platform API supports this reliably.
- Debounce only events that are genuinely duplicated or noisy; do not introduce debounce logic that breaks valid rapid user input or changes normal interaction semantics.
- Avoid uncontrolled retry loops. Use bounded retries and backoff for recoverable errors, then surface a useful diagnostic when recovery fails.
- Handle device disconnection, sleep/resume, session changes, missing permissions, and backend reinitialization without spinning or leaking resources.
- Shut down in a deterministic order: stop accepting new work, cancel active operations, wait safely for workers, unregister hooks/listeners, release native resources, flush required diagnostic output, and then exit.

Do not implement these as generic checklists without inspecting the actual project. Tie each applicable correction to a concrete finding in the code or a reproduced test failure.

## 5. Performance and Resource Reduction

Make the app lighter through measured improvements, not by removing features or weakening correctness.

- Identify expensive operations and repeated allocations before optimizing them.
- Remove unnecessary polling and idle work where event-driven processing is available and behavior remains equivalent.
- Avoid unnecessary timers, duplicate background workers, redundant UI updates, excessive serialization, and repeated parsing of unchanged configuration data.
- Batch or rate-limit diagnostic output so logging cannot itself cause a slowdown or disk flood.
- Ensure memory, native handles, event subscriptions, worker count, and queued work remain bounded over time.
- Do not cache unbounded data or retain obsolete objects through callbacks or closures.
- Reuse resources only when ownership and cleanup remain clear and safe.
- Keep dependencies lean. Do not add a large framework or several packages to solve a localized issue without a demonstrated need.
- Avoid premature micro-optimizations. Preserve readability, correctness, and maintainability.
- Do not lower reliability, skip validation, suppress meaningful errors, or disable existing functions to reduce CPU or memory use.
- Do not change release configuration in a way that conceals crashes or removes necessary diagnostics. Release builds must fail safely and remain diagnosable.

Measure before and after, wherever tooling allows. Report CPU, memory, and other relevant resource behavior under idle use and representative activity. Look for trends over time, not only a single snapshot.

## 6. Diagnostic and Error-Handling Strategy

Add or improve diagnostics only as needed to locate and verify fixes. Diagnostics must not change the app's UI or expose sensitive information.

- Use structured, timestamped logs for startup/shutdown, worker lifecycle, backend initialization, recoverable errors, and unexpected failures.
- Avoid logging every high-frequency mouse or keyboard event by default. Prefer counters, sampling, summaries, and rate-limited warnings.
- Rotate or cap diagnostic files so they cannot grow indefinitely.
- Include exception type, relevant operation, and available stack trace, but do not log passwords, tokens, or unrelated personal data.
- Make diagnostic output configurable or appropriately limited in production builds.
- Report failures clearly through the existing error-reporting path; do not silently swallow exceptions that hide a broken subsystem.
- Do not create a feedback loop where logging an error repeatedly creates more errors or consumes all CPU/disk bandwidth.
- Do not show success unless the relevant operation actually succeeded.

## 7. Preserve User Data and Compatibility

- Keep existing configuration keys, data structures, defaults, profile data, and file formats compatible unless a verified defect makes a migration essential.
- Never delete or overwrite existing user settings during testing or startup.
- If a migration is unavoidable, make it explicit, versioned, backward-compatible where feasible, and protected by a backup or rollback path.
- Write configuration updates atomically where the current storage mechanism supports it. A crash during a save must not leave the entire configuration corrupted.
- Do not reset user preferences to the repository defaults simply to simplify testing.
- Keep existing installers, portable builds, Linux packages, and release automation functional.
- Do not introduce undocumented system dependencies or require administrator/root access unless an existing, necessary capability genuinely requires it. Explain any unavoidable permission requirement.

## 8. Regression Testing and Verification Matrix

Create tests based on the current implementation and reported defects. Use automated tests wherever practical, and manual tests only where OS integration requires them. Do not claim a test was run unless it was actually run.

### Functional regression tests

Verify that all previously working features still behave as they did before the maintenance changes, including:

- The full existing configuration and profile data.
- Operator and weapon selection, ordering, and wraparound where applicable.
- All existing buttons, shortcuts, mappings, controls, and settings.
- Existing enable/disable behavior and valid input-processing modes.
- Saving, loading, reset, and validation of settings.
- Existing platform-specific behavior and supported integrations.
- Existing theme, appearance, navigation, and visual states without design changes.
- Startup, restart, normal shutdown, and recovery after a recoverable error.

### Stability tests

Test, as applicable to the actual implementation:

- Idle operation after startup.
- Repeated user actions and normal sustained activity.
- Repeated enable/disable transitions.
- Repeated application startup and shutdown.
- Repeated settings changes and configuration loads.
- Input release, cancellation, device disconnection, and permission loss.
- Sleep/resume or display-session changes where the application depends on them.
- Recoverable backend errors and repeated failure signals.
- Long-running operation using simulated or safely controlled input.
- Correct cleanup of threads, handles, hooks, listeners, and queued work.

Because the failure reportedly appears after roughly 2–3 minutes, a quick smoke test is not sufficient. First reproduce the failure safely if possible; then run a soak test that lasts substantially longer than the reported failure window—preferably at least 30 minutes—and include repeated representative actions. Use a longer test if evidence suggests the defect occurs only after more time. Do not use uncontrolled real input or risk a system-wide freeze to satisfy the test duration.

### Platform verification

- **Windows:** build and test on Windows when the environment permits, with particular attention to system responsiveness, input hooks, native handles, CPU/memory trends, and clean process exit.
- **Linux:** build and test the supported Linux targets available to the project. Verify relevant desktop/input backends and clean permission/error handling.
- If a target platform cannot be tested in the current environment, run all feasible static/unit/build checks and clearly report exactly what remains unverified. Never claim full cross-platform verification based on one operating system.

### Performance acceptance criteria

- No repeatable crash, application hang, system-wide freeze, runaway input loop, or uncontrolled background processing during the verified test scenarios.
- No unexplained, steadily growing memory, handle, thread, event-queue, or log usage during sustained operation.
- No persistent high CPU usage while idle, unless a measured and documented platform requirement explains it.
- The UI remains responsive during normal use and recovery paths.
- Resources are released during disable, cancellation, shutdown, and error handling.
- Existing feature behavior remains intact, and the UI remains visually equivalent to the pre-fix baseline.

Do not invent precise resource targets without taking a baseline. If meaningful numeric targets can be derived from measurements, record them and use them in regression testing.

## 9. Protect the Existing UI and Verify No Unintended Visual Changes

The existing UI is frozen for this task. Do not apply the UI/UX design skills to redesign the interface during this maintenance pass. Design skills may be consulted only if needed to understand existing UI behavior, not to change its visual direction.

- Do not edit styles, layout definitions, color tokens, typography, icon assets, spacing, sizing, animation, or visible copy unless an actual defect cannot otherwise be fixed.
- Prefer fixes in underlying logic, lifecycle management, state handling, resource ownership, concurrency, and platform adapters.
- If shared UI code must be touched to fix a thread-affinity or lifecycle bug, preserve its rendered appearance and interaction model.
- Capture before/after screenshots at equivalent window sizes and theme settings when screenshot tooling is available.
- Compare the screenshots and correct any unintentional changes to spacing, color, typography, placement, window sizing, and visual hierarchy.
- Confirm that existing UI controls still invoke the same operations and that no existing controls have been removed, renamed, or repurposed.

A visually improved or redesigned UI does not count as success for this task. Success means the **same UI, features, specifications, and expected behavior work reliably with lower or better-controlled resource usage**.

## 10. Required Implementation Workflow

### Phase 1 — Baseline and diagnosis

Inspect the existing code and collect baseline results. Reproduce the issue safely. Identify the specific root cause or causes supported by evidence. Record the affected files and explain why the failure can happen after a short period of use.

### Phase 2 — Minimal targeted fixes

Implement the smallest reliable changes that eliminate the identified causes. Avoid unrelated refactoring, dependency changes, visual edits, and feature additions. If several defects exist, fix each in a controlled sequence and run focused tests after each meaningful change.

### Phase 3 — Regression protection

Add unit, integration, stress, lifecycle, or platform-specific regression tests that would fail under the original defective behavior. Keep tests deterministic wherever possible and isolate hazardous system-integration paths behind mocks or controlled test adapters.

### Phase 4 — Resource profiling and optimization

Measure the app under idle and representative workloads, optimize only confirmed bottlenecks, then repeat the measurements. Confirm the optimization has not altered expected functionality.

### Phase 5 — Cross-platform validation

Run builds, tests, and integration checks for Windows and Linux wherever infrastructure is available. Verify installation/package integrity and application shutdown. Clearly disclose unavailable hardware, OS, or backend tests.

### Phase 6 — Final review

Inspect the diff for accidental UI changes, feature removals, modified defaults, altered mappings, unrelated dependency changes, and generated files. Revert unrelated changes. Rerun the most relevant tests after the final edit.

Do not stop after providing a plan. Perform the actual fixes in the repository, within the available environment and permissions.

## 11. Required Final Report

When implementation is complete, provide a concise but specific report containing:

1. **Root causes:** each confirmed defect and the evidence used to identify it. Separate confirmed causes from remaining hypotheses.
2. **Fixes made:** the files or subsystems changed and how each fix prevents the failure.
3. **Performance results:** measured before/after CPU, memory, thread/handle counts, or other useful metrics where available. State when a metric could not be measured.
4. **Tests performed:** exact builds, tests, operating systems, duration of soak tests, and results.
5. **Preservation confirmation:** state whether UI appearance, UX, features, settings, profile data, mappings, specifications, and package formats remained unchanged. Disclose any necessary exception explicitly.
6. **Remaining risks:** platform combinations, hardware, or failure conditions that could not be tested. Do not disguise unverified work as completed.
7. **How to reproduce verification:** the short, safe test sequence used to confirm the fix.

Do not say "all bugs are fixed" unless the evidence supports that statement. If the cause cannot be reproduced, document the static-analysis findings, targeted fixes, tests performed, and uncertainty honestly.

## 12. Definition of Done

This maintenance task is complete only when all applicable criteria are satisfied:

- [ ] The reported 2–3 minute crash/hang has been reproduced safely or investigated with documented, evidence-based reasoning.
- [ ] Confirmed root causes have been corrected, not merely hidden with broad exception handling or silent retries.
- [ ] Background loops, hooks, threads, timers, native resources, and event subscriptions have safe bounded lifecycles where applicable.
- [ ] CPU, memory, and other relevant resource trends have been measured and do not show unexplained runaway growth during the tested workload.
- [ ] The app remains responsive and exits cleanly in the tested scenarios.
- [ ] Regression tests cover the repaired defects and important existing workflows.
- [ ] Windows and Linux have been tested wherever genuine test infrastructure is available, with untested targets identified honestly.
- [ ] Existing features, specifications, settings, profile data, mappings, and expected behavior are preserved.
- [ ] The UI/UX and appearance remain equivalent to the original baseline; no voluntary redesign has been performed.
- [ ] Existing configuration data, installation formats, and release workflows still work.
- [ ] The final report accurately distinguishes verified results from remaining uncertainty.

**Final priority:** fix the root causes, improve stability, and reduce unnecessary resource use. Do not redesign, remove, rename, simplify, or alter any existing user-facing feature or visual element as part of this task. Stability must improve while the product the user sees and the functionality they rely on remain the same.

---

# Implementation Plan — Standalone Logitech Mouse Profile App

## 0. Instruction to the AI Agent

Read this document completely before making changes. Treat it as the project's persistent product specification, not as a loose suggestion. First inspect the provided Logitech Lua script and the current repository, then implement the application end to end. Do not stop after producing a mockup, a plan, or a visually convincing but non-functional prototype.

Before coding, inspect the skills, design references, project instructions, and documentation available in your environment. Explicitly use the strongest relevant skills for **desktop product design, UI/UX, visual design, accessibility, platform integration, and software architecture**. If your environment supports project memory, save the durable requirements in its supported project-instructions file (for example, `AGENTS.md` or the equivalent) and point it back to this file. If true persistent memory is not available, do not claim that you have saved anything to hidden memory: keep this document as the source of truth and reread it at each major phase.

The goal is to turn the supplied Logitech mouse Lua profile into a **standalone desktop application** with the original profile's features and behavior preserved, while replacing the Logitech-software-only workflow with an independent, maintainable application and a carefully designed interface.

## 1. Product Goal

Build a polished desktop utility that lets the user select an operator, select a weapon/loadout, view and edit the profile's settings, enable or disable the profile, and use the original mouse-button/keyboard workflow where the operating system permits it.

The provided Lua script is the **canonical source of truth** for the initial profile data and behavior. Do not omit operators, weapons, settings, recoil values, rapid-fire flags, timing ranges, navigation rules, status information, or input mappings. Do not silently rebalance, normalize, or "improve" the profile numbers. If any source behavior is unclear, document the ambiguity and preserve the closest faithful interpretation rather than inventing new behavior.

This is a standalone application, not a web page wrapped in a window and not a thin GUI that merely displays hard-coded sample data.

## 2. First Steps: Inspect Before Implementing

1. Locate and read the entire supplied Lua/text file, including the complete `OPERATORS` table and event handler.
2. Inspect the repository, existing build system, supported project conventions, installed design/engineering skills, and the target operating system.
3. Identify the platform APIs required for global mouse/keyboard observation and relative pointer movement. Confirm what is actually possible on the current OS and desktop session before promising support.
4. Produce a short internal architecture note and a design brief before implementation. Record key constraints, permissions, chosen stack, design direction, and trade-offs.
5. Create a source-to-feature checklist and a data-integrity test plan. Keep the original Lua/text source unchanged as a reference fixture.

Do not assume Logitech G HUB/LGS APIs are available outside Logitech software. Build a platform adapter for the equivalent supported OS input APIs, and clearly communicate platform limitations and required permissions. Do not bypass anti-cheat systems, hide the app from security software, or claim compatibility with every game or operating system without verification. The app must not interfere with input when disabled and must provide an obvious emergency-disable path.

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

The app does not need to imitate terminal ASCII alignment. Present the same information using readable, accessible interface components.

## 4. Settings and Persistence

Provide a real settings workflow, not decorative controls:

- Editable global settings for recoil multiplier, vertical variance, horizontal jitter, regular-mode delay min/max, rapid-fire key, rapid-fire hold min/max, and rapid-fire gap min/max.
- Clear unit labels beside timing values.
- Restore-defaults action based on the exact source defaults, with confirmation if user-edited values would be lost.
- Save settings between restarts using an established, safe local configuration mechanism.
- Restore the last selected operator and weapon on launch only if this is consistent with the app's intended behavior; document the choice. The first-run defaults must still match the source.
- Validate edits inline and provide understandable error messages.
- Apply changes atomically so invalid or half-saved configurations cannot leave the input engine in an unknown state.
- Include import/export of the app's own profile/config format if practical. Never overwrite the canonical source or another profile without explicit user action.

## 5. UI/UX: High-Quality, Deliberate Product Design

**The visual result is a first-class requirement. Do not produce a generic AI-generated-looking dashboard.** The app must look like a real desktop utility designed by an experienced product designer, with a coherent visual system and purposeful interaction design.

### 5.1 Design process required before UI implementation

1. Use the available frontend/design/UI-UX skills and any relevant design-system or platform-guideline references in the agent environment. Do not merely mention a skill; actually apply it.
2. Establish the app's information architecture, user journeys, hierarchy, spacing scale, typography, color tokens, component states, and interaction rules before coding the UI.
3. Create a compact design brief and wireframes or a page/component map. If the environment supports visual preview, inspect the actual rendered interface and iterate on it.
4. Build a small, consistent design system with reusable components and tokens. Avoid styling every screen independently.
5. Test the UI at realistic desktop window sizes and at a narrower/resized window size. Prevent clipped controls, awkward empty space, overflow, inconsistent alignment, and unreadable dense lists.

### 5.2 Visual direction

Aim for a **premium, restrained desktop control utility**: precise, calm, purposeful, and easy to scan. It may take cues from well-crafted professional hardware utilities, but must have its own identity and must not depend on copied proprietary assets.

- Use a coherent neutral base palette with one restrained accent color, meaningful status colors, and accessible foreground/background contrast.
- Establish clear type hierarchy, carefully tuned spacing, consistent alignment, useful labels, and disciplined icon usage.
- Prefer clean surfaces, subtle borders, and purposeful depth over decorative effects.
- Use motion only where it communicates a state change; keep transitions fast, subtle, and respectful of reduced-motion preferences.
- Support a polished dark theme. Add a light theme if this can be done consistently rather than as an afterthought.
- Use real icons from a consistent icon set; do not use emoji as interface icons.
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

Choose the best maintainable stack for a standalone desktop utility after inspecting the environment and available skills. Prefer established, documented libraries over custom low-level machinery when they satisfy the requirements. Record the reason for the stack choice before implementation.

Use a separation of concerns similar to:

- **UI layer:** screens, components, user feedback, settings forms, theme, accessibility.
- **Domain/profile layer:** typed operator/weapon schema, validation, source-derived profile data, calculations, navigation rules.
- **Input engine:** event loop, press/release lifecycle, timing, movement and key-emission behavior.
- **Platform adapter:** OS-specific mouse/keyboard APIs, permissions, input-device detection and capability reporting.
- **Persistence layer:** settings load/save, migrations, profile import/export.
- **Diagnostics layer:** structured, user-safe error reporting and optional debug logs.

The input engine must not depend on UI component state. UI interactions should call a well-defined application service, and the engine should publish state changes back to the UI using a safe, typed event/state mechanism.

### Platform support rules

- Detect the current OS, desktop session, and available input capabilities rather than assuming one universal implementation.
- Implement the current target platform completely and test it on that platform. Add additional platform adapters only when their APIs and permission model can be handled correctly.
- If targeting Linux, explicitly account for differences between X11 and Wayland, input permissions, virtual input facilities, and distribution packaging. Do not assume that a mechanism working under X11 will work under Wayland or that Fedora, Ubuntu, and Arch have identical defaults.
- If targeting Windows, use supported input APIs and handle focus/input lifecycle correctly.
- If targeting macOS, assess accessibility/input-monitoring permissions and relevant restrictions before claiming support.
- Request only required permissions and explain why they are needed. Do not silently require administrator/root access if a safer supported path exists.
- Fail safely when permissions are missing: disable automation, present actionable guidance, and keep the rest of the interface usable.
- Do not implement stealth, anti-cheat evasion, process injection, or hidden input behavior intended to evade security controls.

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

Use automated tests wherever possible and manual tests for OS-level input behavior. At minimum, add:

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
- Dark/light themes, where supported, remain readable.
- Resizing the window does not clip essential controls.
- Review screenshots of the real running app at representative sizes; iterate on spacing, hierarchy, contrast, selected states, and empty/error states.

## 9. Build, Packaging, and Documentation

- Provide a reproducible development setup and clear build commands.
- **Release downloadable, platform-native packages for both Windows and Linux.** Do not make users install a development environment, compiler, package manager, or runtime just to use a published release.
- Treat Windows and Linux as separate release targets: build, package, and test each platform on an appropriate native CI runner (or a documented equivalent). Shared source code alone is not proof of platform compatibility.
- Do not publish an artifact for a platform until its relevant input backend, permissions flow, startup/shutdown behavior, and package have been tested on that platform.

### 9.1 Required Release Artifacts

For each versioned release, produce these artifacts whenever the corresponding target has passed its platform tests:

**Windows x86-64**
- A straightforward installer, preferably `ProductName-Setup-x64.exe`, with a clear install/uninstall flow and Start Menu shortcut where appropriate.
- A portable `ProductName-Portable-x64.zip` build if the selected framework can support it reliably. The portable build must not silently bypass required permissions or system dependencies.

**Linux x86-64**
- A `ProductName-x86_64.AppImage` for a simple download-and-run experience, provided it works with the app's actual native input dependencies and target Linux environments.
- A Debian package, `ProductName-amd64.deb`, for Debian/Ubuntu-based systems.
- An RPM package, `ProductName-x86_64.rpm`, for Fedora/RHEL-compatible systems.
- If a format cannot be made reliable for a specific backend or distribution, document the reason and offer the tested alternative rather than publishing a broken or misleading package.

Use consistent product names, version numbers, architecture labels, and file extensions. Clearly identify which download is intended for each operating system and distribution. If ARM builds are added later, label them separately and test them independently; do not label an x86-64 build as universal.

### 9.2 Download and Release Experience

- Set up a release workflow (for example, CI-triggered builds from a version tag) that creates the platform artifacts reproducibly and attaches them to a clearly organized release page, such as GitHub Releases if the project is hosted there.
- Publish a short release note and SHA-256 checksums for each downloadable file. Do not claim code signing or verified signatures unless they were actually configured and checked.
- Make the README's **Download** section easy to scan: separate Windows and Linux, explain which file most users should choose, and include brief installation and first-launch instructions.
- For Linux, explain executable permissions, AppImage prerequisites if any, package installation commands for `.deb` and `.rpm`, and any distribution-specific input permission/session caveats. Explicitly discuss the differences between X11 and Wayland where relevant.
- For Windows, explain SmartScreen or unsigned-build warnings honestly if the release is not code-signed, and provide uninstall instructions.
- Include dependency/runtime handling notes. Bundle application-owned dependencies where licensing and the chosen packaging format permit it, but do not bundle system components that should remain managed by the OS.
- Test the artifacts from a clean environment or clean virtual machine where feasible; do not validate packages only from the developer's existing environment.
- Keep build instructions available for contributors, but make clear that end users should normally download a release package rather than build from source.

### 9.3 Documentation and Repository Hygiene

- Include a concise README with features, supported platforms, prerequisites, permissions, installation/build steps, usage, default controls, configuration storage location, known limitations, troubleshooting, and uninstall instructions.
- Include a short note documenting how the original Lua semantics were mapped to the standalone application.
- Keep secrets and unrelated user data out of the repository. Do not add network telemetry or account sign-in for a local utility unless a future requirement genuinely needs it.

## 10. Implementation Phases and Deliverables

### Phase 1 — Source audit and design
- Read the complete source.
- Record the full feature/input mapping and profile-data integrity counts.
- Inspect available design/engineering skills and select the stack/platform adapter.
- Produce the design brief, interaction map, component inventory, and initial wireframes.

**Deliverable:** source-to-feature checklist, architecture note, and design specification.

### Phase 2 — Domain model and migration
- Create the typed operator/weapon schema and migrate the complete catalogue.
- Add validation and automated comparison/integrity tests against the source.
- Implement selection/navigation and calculation logic independently of UI.

**Deliverable:** tested domain layer with source-faithful profile data.

### Phase 3 — Functional input backend
- Implement the platform adapter for the verified target OS.
- Add input lifecycle handling, default mappings, cancellation, emergency stop, and permission reporting.
- Test disabled, active, stop, failure, and shutdown conditions before connecting the full UI.

**Deliverable:** operational, safely stoppable input engine and documented platform constraints.

### Phase 4 — Designed UI and persistence
- Implement the design system and screens from the approved design brief.
- Connect every control to the real domain/backend services.
- Add persistence, validation, restore defaults, status updates, and troubleshooting.
- Iterate using screenshots of the actual running app rather than trusting code inspection alone.

**Deliverable:** complete standalone application with no placeholder data or inert controls.

### Phase 5 — Verification and handoff
- Run automated tests, build/package checks, manual input lifecycle tests, and UI review at several window sizes.
- Build Windows and Linux release packages independently using the appropriate build environments or CI runners.
- Test each published installer/package in a clean environment where feasible, including first launch, permissions, normal use, update/reinstall behavior where applicable, and uninstall.
- Generate SHA-256 checksums and verify the release files before publishing.
- Resolve high-impact visual issues before declaring completion.
- Update README, download guidance, release notes, and known-limitations documentation.

**Deliverable:** runnable source code and tests, build instructions, tested Windows installer and Linux packages (only for platforms actually verified), clear downloadable release assets, checksums, and a completion report listing what was implemented, what was tested, and anything not supported.

## 11. Definition of Done

The task is complete only when all of the following are true:

- [ ] The app runs independently of Logitech G HUB/LGS on the verified target platform.
- [ ] All operators and weapon entries in the source are migrated and automatically verified.
- [ ] Original defaults, values, modes, calculations, navigation, modifier behavior, and status information are preserved.
- [ ] The profile can be enabled/disabled and the actual input backend responds to real state changes.
- [ ] All settings are editable, validated, saved, and restorable to source defaults.
- [ ] Every visible UI control is functional; there are no mock actions or placeholder catalogue entries.
- [ ] The interface demonstrates deliberate product design, a reusable design system, accessible states, consistent spacing/type/color, and careful visual iteration.
- [ ] The interface avoids the common generic "vibe-coded" look described in Section 5.5.
- [ ] Emergency stop, shutdown, release handling, permission failure, and backend failure have been tested.
- [ ] Windows x86-64 has a tested, downloadable installer (`.exe`) with correct install/uninstall behavior, or the release explicitly states why it is not yet supported.
- [ ] Linux x86-64 has tested, downloadable packages: AppImage where reliable, plus `.deb` and `.rpm` where their dependencies and input backend are supported; unsupported formats are clearly identified rather than falsely advertised.
- [ ] Windows and Linux artifacts were built and tested independently; package filenames clearly state OS, architecture, and format.
- [ ] Release files include verified SHA-256 checksums and straightforward end-user download/install instructions.
- [ ] The actual target platform build and package have been tested; unsupported platforms are clearly identified.
- [ ] Documentation explains setup, permissions, usage, default mappings, limitations, troubleshooting, and how to choose the correct Windows/Linux download.
- [ ] The final report is honest about any gaps. Do not mark any checklist item complete without evidence.

## 12. Final Agent Response

When implementation is finished, report:

1. The chosen stack and the reason for choosing it.
2. The verified target platform(s) and packaging output, with exact downloadable filenames for Windows and Linux and a clear distinction between tested and unsupported formats.
3. The preserved features and data-integrity test results.
4. The automated and manual tests actually performed.
5. The UI/UX and accessibility work completed, including which design skills or reference guidelines were applied.
6. Any platform limitations, permission requirements, or incomplete items.
7. Exact commands to run, test, build, and package the app.

Be precise and evidence-based. Do not report a build, test, permission check, UI review, or platform as successful unless it was actually verified.
