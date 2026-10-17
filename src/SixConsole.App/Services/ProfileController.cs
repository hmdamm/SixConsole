using SixConsole.App.Input;
using SixConsole.Profile;
using SixConsole.App.Services;

namespace SixConsole.App.Services;

/// <summary>
/// UI-facing controller around <see cref="InputEngine"/>. Owns engine lifecycle and exposes
/// observable state. The engine never touches UI; the UI subscribes to <see cref="StateChanged"/>
/// and marshals through App.DispatchToUi.
/// </summary>
public sealed class ProfileController : IDisposable
{
    private readonly InputEngine _engine;

    public event EventHandler<EngineEventArgs>? StateChanged;

    private bool? _lastLoggedEnabled;
    private EngineStatus? _lastLoggedStatus;
    private string? _lastLoggedError;
    private string? _lastLoggedOp;
    private string? _lastLoggedWeapon;

    public ProfileController(AppServices services)
    {
        _engine = new InputEngine(services.Selection, () => services.Settings, new InputEngineOptions { CheckPhysicalKeyState = true });
        _engine.StateChanged += OnEngineStateChanged;
    }

    private void OnEngineStateChanged(object? sender, EngineEventArgs e)
    {
        var snap = e.Snapshot;
        if (_lastLoggedEnabled != snap.Enabled ||
            _lastLoggedStatus != snap.Status ||
            _lastLoggedError != snap.LastError ||
            _lastLoggedOp != snap.Operator?.Id ||
            _lastLoggedWeapon != snap.Weapon?.Name)
        {
            _lastLoggedEnabled = snap.Enabled;
            _lastLoggedStatus = snap.Status;
            _lastLoggedError = snap.LastError;
            _lastLoggedOp = snap.Operator?.Id;
            _lastLoggedWeapon = snap.Weapon?.Name;
            Log($"engine: enabled={snap.Enabled} status={snap.Status} op={snap.Operator?.Name} weapon={snap.Weapon?.Name} err={snap.LastError}");
        }
        StateChanged?.Invoke(this, e);
    }

    public bool IsEnabled => _engine.IsEnabled;
    public EngineStatus Status => _engine.Status;
    public string? LastError => _engine.LastError;

    public EngineSnapshot Snapshot() => _engine.Snapshot();

    public void Start()
    {
        try
        {
            _engine.Start();
        }
        catch (Exception ex)
        {
            _engine.Disable($"Could not start the input engine: {ex.Message}");
        }
        Log($"engine started: status={_engine.Status} err={_engine.LastError ?? "none"}");
    }

    public void Toggle()
    {
        _engine.ToggleEnabled();
        Log($"toggle -> enabled={_engine.IsEnabled}");
    }

    public void EmergencyStop()
    {
        _engine.EmergencyStop();
        Log("EMERGENCY STOP");
    }

    public void Dispose() => _engine.Dispose();

    private static readonly object LogGate = new();
    private const long MaxLogSizeBytes = 512 * 1024; // 512 KB hard limit

    private static void Log(string line)
    {
        Task.Run(() =>
        {
            try
            {
                lock (LogGate)
                {
                    var file = new FileInfo(SettingsStore.LogFilePath);
                    if (file.Exists && file.Length > MaxLogSizeBytes)
                    {
                        var backup = SettingsStore.LogFilePath + ".old";
                        try { File.Delete(backup); File.Move(SettingsStore.LogFilePath, backup); }
                        catch { File.Delete(SettingsStore.LogFilePath); }
                    }
                    File.AppendAllText(SettingsStore.LogFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}");
                }
            }
            catch
            {
                // diagnostics must never break the app
            }
        });
    }
}

