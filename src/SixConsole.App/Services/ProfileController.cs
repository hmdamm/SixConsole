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

    public ProfileController(AppServices services)
    {
        _engine = new InputEngine(services.Selection, () => services.Settings);
        _engine.StateChanged += OnEngineStateChanged;
    }

    private void OnEngineStateChanged(object? sender, EngineEventArgs e)
    {
        Log($"engine: enabled={e.Snapshot.Enabled} status={e.Snapshot.Status} op={e.Snapshot.Operator?.Name} weapon={e.Snapshot.Weapon?.Name} err={e.Snapshot.LastError}");
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

    private void Log(string line)
    {
        try
        {
            File.AppendAllText(SettingsStore.LogFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}");
        }
        catch
        {
            // diagnostics must never break the app
        }
    }
}

