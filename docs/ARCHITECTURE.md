# Architecture Note — Six Console (standalone mouse-profile utility)

## Source of truth
`message (2).txt` (the supplied Logitech G HUB Lua script) is the canonical profile source.
`Implementation_Plan (1).md` is the product specification. The Lua file stays in the repo as an
unchanged reference fixture (`SixConsole.Tests/SourceFixtures/lua-source.txt`).

## Platform / stack decision (evidence)
**Choice: WinUI 3 + Windows App SDK 1.8.x + C#/.NET 10, packaged (single-project MSIX).**

Evidence gathered on this machine before choosing:
- OS: Windows 11 Pro x64, build 10.0.26200, Intel Iris Xe graphics.
- .NET SDK 10.0.300 installed; plain `dotnet` NuGet restore verified working (Newtonsoft probe restored).
- Visual Studio 2026 Community installed (MSBuild at
  `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`).
- Windows App Runtime system packages installed (1.4/1.5/1.6/1.7/**1.8**/2.x lines), so a
  framework-dependent packaged app can deploy without bundling the runtime.
- Windows SDK bin tools present: `makeappx.exe` / `signtool.exe`
  (`C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64`).
- No WinUI project templates and no workloads installed (`dotnet workload list` is empty), so the
  WinUI csproj is authored by hand from the documented template shape rather than via a VS template.

WinUI 3 (not WPF, not a web wrapper) matches the plan's default preference, gives Fluent controls,
Light/Dark/System theming, and the Mica window chrome without any web technology.

## Target
- Windows 11 x64 first (validated on this machine; Win10 19041+ is the compile-in floor, support for
  other configs is only claimed after testing).
- Linux/macOS: explicitly out of scope.

## Architecture layers (as required by plan §6)
```
┌─────────────────────────────────────────────────────────┐
│ UI layer (WinUI 3): ProfileView, SettingsView, HelpView │
│   → talks ONLY to AppServices (MVVM, CommunityToolkit)  │
├─────────────────────────────────────────────────────────┤
│ AppServices: ProfileController, SettingsStore, Theme    │
│   observes InputEngine events (DispatcherQueue-safe)    │
├─────────────────────────────────────────────────────────┤
│ Domain/profile library (SixConsole.Profile):            │
│   Operator/Weapon schema, Catalogue (from JSON),        │
│   navigation rules (wraparound, weapon reset),          │
│   calc.EffectivePull = Round(pull × multiplier)         │
├───────────────────┬─────────────────────────────────────┤
│ InputEngine       │ Platform adapter (Win32)            │
│ (owns state, no   │  WH_MOUSE_LL/WH_KEYBOARD_LL hooks   │
│  UI deps)         │  SendInput for synthetic movement & │
│                   │  rfKey press/release (INPUT_MOUSE/  │
│                   │  INPUT_KEYBOARD, NONINJECT flag so  │
│                   │  synthetic events are self-         │
│                   │  distinguishable)                   │
│                   │  GetKeyState(VK_CAPITAL) for caps   │
├───────────────────┴─────────────────────────────────────┤
│ Persistence: %LOCALAPPDATA%\SixConsole\settings.json     │
│ Diagnostics: local file log + status surface, no network │
└─────────────────────────────────────────────────────────┘
```
The input thread is a dedicated dedicated-message-pump thread that owns `SetWindowsHookEx(WH_MOUSE_LL)`.
Synthetic events are filtered out via the `LLMHF_INJECTED` flag so the engine cannot recurse on its
own `SendInput` output. All loops are cancellation-token aware; every stop path (toggle off, release
of LMB, app exit, emergency stop, hook failure, device death) releases any held key immediately.

## Behavior mapping (Lua → app)
| Lua | App |
|---|---|
| `isEnabled` global | toggled by Mouse Button 4**\*** and a UI toggle; starts `false` each run |
| `MOUSE_BUTTON_PRESSED arg==3` toggle | Mouse XButton2 (button 5) is "next" mapping; button 3 (middle) toggles enabled |
| `arg==5` + caps → next weapon / else next op | hook forwards to `ProfileNavigator.Next…()` |
| `arg==4` + caps → prev weapon / else prev op | same, previous direction |
| `IsMouseButtonPressed(1)` loops | LMB-down starts loop, LMB-up cancels loop |
| `Sleep(math.random(min,max))` | `Random.Shared.Next(min, max+1)` (inclusive like Lua `math.random`) + `Task.Delay` |
| `Round(num)=floor(num+0.5)` | same formula, tests cover it |
| `PressKey/ReleaseKey(rfKey)` | `SendInput` synthetic key with the **L**LMHF_INJECTED-filtered hook; the rfKey emits are real synthetic keys the user can re-map in Settings |
| `IsKeyLockOn("capslock")` | `GetKeyState(VK_CAPITAL) & 1` on the hook input |
| `LogStatus()` console output | always-visible status panel + status bar in ProfileView |

Button numbers: Logitech `arg` is its LGS numbering; Windows reports hardware mouse buttons as
`VK_XBUTTON1` (usually LGS 4) and `VK_XBUTTON2` (usually LGS 5), `VK_MBUTTON` (LGS 3). The app
models *logical* actions (Toggle / Next / Prev) with an editable mapping layer instead of assuming
meaningless raw numbers; defaults follow the physical-buttons equivalent of the Lua workflow.

## Permissions & safety
- LowLevel mouse/keyboard hooks (`SetWindowsHookEx`) need **no admin rights** on stock Windows.
- `SendInput` needs no admin for ordinary desktop usage; some secure/anti-cheat games ignore
  synthetic input — that is documented, never defeated.
- No process injection, no hiding from security software, no anti-cheat evasion. The plan forbids it
  and we agree.
- Emergency stop: a dedicated always-visible **Stop** control (+ `Esc` hotkey when neither the game
  nor the app owns Esc) unconditionally cancels the loop and unpresses the rfKey.

## Known semantic notes (from reading the source)
- `Solis` has `SMG-11 (Sec) pull = 48` while other copies of that gun use 45/47: preserved verbatim.
- `Bearing 9 (Sec)` uses pull 34 under Tachanka but 49 elsewhere: preserved verbatim.
- These are source-faithful, not data-entry "improvements".
- Names like `Reaper Mk2 (Sec)` (e.g. Sledge AND Ying) and `SR-25` (Striker & Blackbeard & Flores)
  repeat by design; stable IDs (`opId`, `weaponId`) are position-derived, dedupe is never global.
