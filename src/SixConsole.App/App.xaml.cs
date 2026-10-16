using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SixConsole.App.Services;
using System;

namespace SixConsole.App;

public partial class App : Application
{
    public static App? Instance { get; private set; }

    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "opencode", "app-crash.log");

    private readonly bool _secondInstance;
    private readonly System.Threading.Mutex? _instanceMutex;

    internal SettingsStore SettingsStore { get; } = new();
    internal ThemeService ThemeService { get; }
    internal AppServices Services { get; }
    internal ProfileController? Controller { get; private set; }

    public App()
    {
        Instance = this;
        // Single-instance guard: two copies would each own a low-level mouse hook and both
        // inject recoil movement/rapid-fire key events — a physical double application.
        _instanceMutex = new System.Threading.Mutex(true, @"Local\SixConsole.SingleInstance", out var createdNew);
        _secondInstance = !createdNew;
        _uiDispatcher = DispatcherQueue.GetForCurrentThread(); // ctor runs on the UI thread
        ThemeService = new ThemeService(this, SettingsStore);
        Services = new AppServices(SettingsStore);
        Microsoft.UI.Xaml.Application.Current.UnhandledException += OnXamlUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        Log($"App ctor complete (secondInstance={_secondInstance})");
    }

    internal static void Log(string line)
    {
        try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}"); } catch { }
    }

    public static DispatcherQueue MainDispatcherQueue =>
        DispatcherQueue.GetForCurrentThread();

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        if (_secondInstance)
        {
            Log("second instance suppressed");
            MessageBoxW(0, "Six Console is already running.", "Six Console", 0x40);
            Environment.Exit(0);
        }
        Log("OnLaunched entered");
        Controller = new ProfileController(Services);
        Log("controller built");
        Controller.Start();
        Log("controller started");
        ThemeService.Load();
        _window = new MainWindow();
        Log("window constructed");
        ThemeService.Apply();
        Log("theme applied");
        _window.Activate();
        Log("window activated");
    }

    private void OnXamlUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        => Log($"XAML UNHANDLED: {e.Message}\n{e.Exception}");
    private void OnDomainUnhandledException(object? sender, System.UnhandledExceptionEventArgs e)
        => Log($"DOMAIN UNHANDLED: {e.ExceptionObject}");

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);

    private static DispatcherQueue? _uiDispatcher; // captured on the UI thread in the ctor

    private MainWindow? _window;

    internal MainWindow MainWindowInstance => _window ?? throw new InvalidOperationException("MainWindow not created yet");

    /// <summary>Marshals work onto the UI thread; safe to call from any thread.</summary>
    public static void DispatchToUi(Action action)
    {
        var dq = _uiDispatcher ?? DispatcherQueue.GetForCurrentThread();
        if (dq is null) return; // no dispatcher available: nothing to update
        dq.TryEnqueue(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                // Engine event handlers must never take the process down through the dispatcher
                Log($"DispatchToUi FAILED: {ex}");
            }
        });
    }
}
