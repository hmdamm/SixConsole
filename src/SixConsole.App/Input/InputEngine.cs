using SixConsole.Profile;

using System.Runtime.InteropServices;

namespace SixConsole.App.Input;

public enum EngineStatus
{
    NotStarted,
    Running,
    StoppedAfterError,
    StoppedByUser,
    StoppedForShutdown,
}

public sealed record EngineSnapshot(
    bool Enabled,
    EngineStatus Status,
    Operator? Operator,
    Weapon? Weapon,
    int OperatorIndex,
    int WeaponIndex,
    int OperatorCount,
    int WeaponCount,
    bool CapsLockOn,
    string? LastError,
    InputMode Mode,
    int EffectivePull);

/// <summary>Fired on every engine state change (already marshaled onto a worker; consumers marshal to UI).</summary>
public sealed class EngineEventArgs : EventArgs
{
    public required EngineSnapshot Snapshot { get; init; }
}

/// <summary>
/// Owns the input lifecycle: hooking, button semantics, timers for rapid fire/hold/gap, and
/// synthetic movement/key emission. Physics and loop logic run on a dedicated thread with no
/// UI references; the UI subscribes to <see cref="StateChanged"/> and marshals itself.
/// </summary>
public sealed class InputEngine : IDisposable
{
    private readonly object _gate = new();
    private Thread? _hookThread;
    private nint _mouseHook;
    private volatile bool _disposed;
    private volatile bool _isEnabled;
    private EngineStatus _status = EngineStatus.NotStarted;
    private string? _lastError;
    private volatile CountDownCts _runCts = new();  // cancels the active LMB loop
    private volatile bool _leftButtonDown;      // last-known physical LMB state
    private readonly InputEngineOptions _options;
    private readonly SelectionModel _selection;
    private Func<ProfileSettings> _settingsProvider;
    private Win32.LowLevelMouseProc? _mouseProc;  // keep delegates alive

    public event EventHandler<EngineEventArgs>? StateChanged;

    public InputEngine(SelectionModel selection, Func<ProfileSettings> settingsProvider, InputEngineOptions? options = null)
    {
        _selection = selection;
        _settingsProvider = settingsProvider;
        _options = options ?? new InputEngineOptions();
    }

    public InputEngineOptions Options => _options;

    public bool IsEnabled
    {
        get => _isEnabled;
        private set
        {
            var changed = _isEnabled != value;
            _isEnabled = value;
            if (changed) PublishSnapshot();
        }
    }

    public EngineStatus Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                _status = value;
                PublishSnapshot();
            }
        }
    }

    public string? LastError
    {
        get => _lastError;
        private set
        {
            if (_lastError != value)
            {
                _lastError = value;
                PublishSnapshot();
            }
        }
    }

    /// <summary>Starts the hook thread. Idempotent; safe to call again.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(InputEngine));
            if (_hookThread is { IsAlive: true }) return;

            _runCts = new CountDownCts();
            _hookThread = new Thread(HookLoop)
            {
                Name = "SixConsole.InputHook",
                IsBackground = true,
            };
            // Hook installation happens on that thread; it publishes status there.
            _hookThread.Start();

            Status = EngineStatus.Running;
        }
    }

    public void ToggleEnabled()
    {
        IsEnabled = !IsEnabled; // property setter publishes on change
        if (!IsEnabled) CancelActiveLoop();
    }

    public void Disable(string reason)
    {
        if (reason.Length > 0) _lastError = reason;
        _isEnabled = false; // set directly: one consolidated publish below
        CancelActiveLoop();
        PublishSnapshot(force: true);
    }

    /// <summary>
    /// Emergency stop: unconditional cancel + rfKey release + disable. Always safe to call,
    /// even before Start, even after errors, and during engine failure.
    /// </summary>
    public void EmergencyStop()
    {
        CancelActiveLoop();
        _isEnabled = false; // set directly: one consolidated publish below
        try
        {
            _options.SendKeyUp(_options.KeyCodeOf(_settingsProvider().RfKey));
        }
        catch
        {
            // never fail the stop path; best-effort release of the configured key
        }
        PublishSnapshot(force: true);
    }

    private void CancelActiveLoop()
    {
        var cts = _runCts;
        cts.Cancel();
    }

    /// <summary>Engine shutdown; stops hooks, cancels loops, releases any held keys.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        EmergencyStop();
        try
        {
            var threadId = _hookThreadId; // 0 = hook thread not started; nothing to signal
            if (threadId != 0) Win32.PostThreadMessage(threadId, Win32.WM_QUIT_USER, 0, 0);
        }
        catch
        {
            // hook thread may have already terminated
        }
        if (_mouseHook != 0) Win32.UnhookWindowsHookEx(_mouseHook);
        Status = EngineStatus.StoppedForShutdown;
        PublishSnapshot(force: true);
    }

    private void HookLoop()
    {
        var threadId = Kernel32.GetCurrentThreadId();
        lock (_gate) { _hookThreadId = threadId; }
        try
        {
            var module = Win32.GetModuleHandleW(null);
            _mouseProc = OnMouseHook;
            _mouseHook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseProc, module, 0);
            if (_mouseHook == 0)
            {
                _lastError = $"Could not install global mouse hook (error {Marshal.GetLastWin32Error()}).";
                Status = EngineStatus.StoppedAfterError;
                return;
            }
            Status = EngineStatus.Running;

            // Message pump keeps the hook thread alive and lets PostThreadMessage control exit.
            while (Win32.GetMessage(out var msg, 0, 0, 0))
            {
                if (msg.message == Win32.WM_QUIT_USER || _disposed) break;
            }
        }
        catch (Exception ex)
        {
            _lastError = $"Input engine failure: {ex.Message}";
            Status = EngineStatus.StoppedAfterError;
        }
        finally
        {
            if (_mouseHook != 0)
            {
                Win32.UnhookWindowsHookEx(_mouseHook);
                _mouseHook = 0;
            }
            if (Status == EngineStatus.Running) Status = EngineStatus.StoppedByUser;
        }
    }

    private uint _hookThreadId;

    private nint OnMouseHook(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
            // Ignore our own synthetic output (SendInput) via marker + the injected flag;
            // this guarantees no recursion, per plan §7.
            bool injected = (info.flags & Win32.LLMHF_INJECTED) != 0;
            if (!injected)
            {
                var msg = (uint)wParam;
                switch (msg)
                {
                    case Win32.WM_LBUTTONDOWN:
                        _leftButtonDown = true;
                        OnLeftDown();
                        break;
                    case Win32.WM_LBUTTONUP:
                        _leftButtonDown = false;
                        OnLeftUp();
                        break;
                    case Win32.WM_MBUTTONDOWN:
                        HandleButtonDown(MouseButton.Middle);
                        break;
                    case Win32.WM_XBUTTONDOWN:
                        HandleButtonDown(HiWordFromMouseData(info.mouseData) == 1 ? MouseButton.XButton1Back : MouseButton.XButton2Forward);
                        break;
                }
            }
        }
        return Win32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private static int HiWordFromMouseData(uint data) => (short)(data >> 16);

    private void HandleButtonDown(MouseButton button)
    {
        if (button == MouseButton.Middle)
        {
            ToggleEnabled();
            return;
        }
        if (!IsEnabled) return;
        bool capsOn = (Win32.GetKeyState(Win32.VK_CAPITAL) & 1) != 0;
        if (button == MouseButton.XButton2Forward)
        {
            if (capsOn) _selection.NavigateWeapon(NavigationDirection.Next);
            else _selection.NavigateOperator(NavigationDirection.Next);
        }
        else if (button == MouseButton.XButton1Back)
        {
            if (capsOn) _selection.NavigateWeapon(NavigationDirection.Previous);
            else _selection.NavigateOperator(NavigationDirection.Previous);
        }
        PublishSnapshot(force: true);
    }

    private void OnLeftDown()
    {
        if (!IsEnabled) return;
        var cts = _runCts;
        cts.Cancel(); // cancel any previous (stale) loop defensively
        _runCts = new CountDownCts();
        var snapshotSettings = _settingsProvider();
        var op = _selection.Operator;
        var weapon = _selection.Weapon;
        int effectivePull = snapshotSettings.EffectivePull(weapon);
        var runCts = _runCts;
        var thread = new Thread(() => RunLoop(op, weapon, effectivePull, runCts))
        {
            Name = "SixConsole.RunLoop",
            IsBackground = true,
        };
        thread.Start();
        PublishSnapshot(force: true);
    }

    private void OnLeftUp()
    {
        if (!IsEnabled) return;
        _runCts.Cancel();
        _runCts = new CountDownCts(); // fresh handles for the next press
        PublishSnapshot(force: true);
    }

    // Regression-test entry points: mirror the physical hook path (hook → state → handler)
    // without installing a global hook inside the test runner.
    internal void TestButtonDown(MouseButton button) => HandleButtonDown(button);

    internal void TestLeftButtonDown()
    {
        _leftButtonDown = true;
        OnLeftDown();
    }

    internal void TestLeftButtonUp()
    {
        _leftButtonDown = false;
        OnLeftUp();
    }

    /// <summary>
    /// The behavioral loop. Mirrors the Lua exactly: for rapidFire weapons it presses and
    /// releases the rf key in minHold..maxHold bursts, applies one pull move per cycle with
    /// vertical variance and horizontal jitter, and sleeps minGap..maxGap between bursts;
    /// for full-auto mode it applies pull moves on a minDelay..maxDelay cadence.
    /// All timings are inclusive at both ends (math.random semantics).
    /// </summary>
    private void RunLoop(Operator op, Weapon weapon, int effectivePull, CountDownCts cts)
    {
        var settings = _settingsProvider();
        var rnd = Random.Shared;
        var mode = settings.ModeFor(weapon);
        try
        {
            if (mode == InputMode.RapidFire)
            {
                byte keyCode = _options.KeyCodeOf(settings.RfKey);
                while (IsEnabled && !cts.IsCancellationRequested && _leftButtonDown)
                {
                    _options.SendKeyDown(keyCode);
                    if (!SleepChecked(rnd.Next(settings.Hold.Min, settings.Hold.Max + 1), cts)) break;
                    _options.SendKeyUp(keyCode);

                    if (effectivePull > 0 && !cts.IsCancellationRequested)
                    {
                        var v = effectivePull + rnd.Next(-settings.VertVariance, settings.VertVariance + 1);
                        var h = rnd.Next(-settings.HorizJitter, settings.HorizJitter + 1);
                        _options.SendRelativeMove(h, v);
                    }

                    if (!SleepChecked(rnd.Next(settings.Gap.Min, settings.Gap.Max + 1), cts)) break;
                }
            }
            else
            {
                while (IsEnabled && !cts.IsCancellationRequested && _leftButtonDown)
                {
                    if (effectivePull > 0)
                    {
                        var v = effectivePull + rnd.Next(-settings.VertVariance, settings.VertVariance + 1);
                        var h = rnd.Next(-settings.HorizJitter, settings.HorizJitter + 1);
                        _options.SendRelativeMove(h, v);
                    }
                    if (!SleepChecked(rnd.Next(settings.Delay.Min, settings.Delay.Max + 1), cts)) break;
                }
            }
        }
        finally
        {
            _options.SendKeyUp(_options.KeyCodeOf(settings.RfKey)); // never leave the rf key stuck
        }
    }

    /// <summary>Returns false when cancelled (loop should stop promptly).</summary>
    private static bool SleepChecked(int ms, CountDownCts cts)
    {
        if (ms <= 0) return !cts.IsCancellationRequested;
        return !cts.WaitHandle.WaitOne(ms);
    }

    public EngineSnapshot Snapshot()
    {
        var op = _selection.Operator;
        var weapon = _selection.Weapon;
        var settings = _settingsProvider();
        bool capsOn = _hookThread is { IsAlive: true } && (Win32.GetKeyState(Win32.VK_CAPITAL) & 1) != 0;
        return new EngineSnapshot(
            _isEnabled,
            _status,
            op,
            weapon,
            _selection.OperatorIndex,
            _selection.WeaponIndex,
            _selection.OperatorCount,
            op.Weapons.Count,
            capsOn,
            _lastError,
            settings.ModeFor(weapon),
            settings.EffectivePull(weapon));
    }

    private void PublishSnapshot(bool force = false)
    {
        if (!force && !_isEnabled && _status is EngineStatus.NotStarted or EngineStatus.StoppedByUser) return;
        StateChanged?.Invoke(this, new EngineEventArgs { Snapshot = Snapshot() });
    }
}

/// <summary>A minimal countdown without threads, exposing WaitHandle semantics.</summary>
public sealed class CountDownCts
{
    private readonly ManualResetEventSlim _evt = new(false);
    private volatile bool _cancelled;

    public void Cancel()
    {
        _cancelled = true;
        _evt.Set();
    }

    public bool IsCancellationRequested => _cancelled;

    public WaitHandle WaitHandle => _evt.WaitHandle;
}

/// <summary>Point at which the platform adapter injects synthetic events. Test doubles override these.</summary>
public sealed class InputEngineOptions
{
    public Func<string, byte> KeyCodeOf { get; set; } = DefaultKeyCodeOf;
    public Action<byte> SendKeyDown { get; set; } = k => InputInjector.SendKey(k, down: true);
    public Action<byte> SendKeyUp { get; set; } = k => InputInjector.SendKey(k, down: false);
    public Action<int, int> SendRelativeMove { get; set; } = (dx, dy) => InputInjector.SendRelativeMove(dx, dy);

    public static byte DefaultKeyCodeOf(string key)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("rfKey must be one character", nameof(key));
        return (byte)char.ToUpperInvariant(key[0]);
    }
}

/// <summary>The only place that physically emits synthetic input.</summary>
internal static class InputInjector
{
    public static void SendKey(byte vk, bool down)
        => _ = Win32.SendInput(1, BuildKeyInput(vk, down), Marshal.SizeOf<Win32.INPUT>());

    public static void SendRelativeMove(int dx, int dy)
        => _ = Win32.SendInput(1, BuildMoveInput(dx, dy), Marshal.SizeOf<Win32.INPUT>());

    // Builders are exposed separately so regression tests can validate the marshaled
    // layout end-to-end without actually injecting events into the desktop session.
    public static Win32.INPUT[] BuildKeyInput(byte vk, bool down)
    {
        var input = new Win32.INPUT
        {
            type = Win32.INPUT_KEYBOARD,
            u = new Win32.INPUTUNION
            {
                ki = new Win32.KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = down ? 0 : Win32.KEYEVENTF_KEYUP,
                    dwExtraInfo = Win32.INJECTED_EXTRAINFO,
                },
            },
        };
        return new[] { input };
    }

    public static Win32.INPUT[] BuildMoveInput(int dx, int dy)
    {
        var input = new Win32.INPUT
        {
            type = Win32.INPUT_MOUSE,
            u = new Win32.INPUTUNION
            {
                mi = new Win32.MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    dwFlags = Win32.MOUSEEVENTF_MOVE,
                    dwExtraInfo = Win32.INJECTED_EXTRAINFO,
                },
            },
        };
        return new[] { input };
    }
}

internal static partial class Kernel32
{
    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();
}
