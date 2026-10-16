using Microsoft.UI.Xaml;
using SixConsole.App.Input;
using SixConsole.App.Views;

namespace SixConsole.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        try
        {
            InitializeComponent();
            App.Log("MainWindow InitializeComponent OK");
            Title = "Six Console";
            // Merge client area with the caption area (custom integrated titlebar; Fluent chrome)
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Standard;
            var controller = App.Instance?.Controller;
            if (controller != null)
            {
                controller.StateChanged += OnEngineState;
                // The engine may already be running when this window subscribes; pull current state.
                App.DispatchToUi(() => ApplyStatus(controller.Snapshot()));
            }
            // Clean shutdown: unhook, cancel loops, release held keys (hooks are process-relative,
            // but the rapid-fire key can remain stuck if the loop thread is dropped).
            Closed += (_, _) =>
            {
                try { App.Instance?.Controller?.Dispose(); }
                catch (Exception ex) { App.Log($"controller dispose on close FAILED: {ex}"); }
            };
        }
        catch (Exception ex)
        {
            App.Log($"MainWindow init FAILED: {ex}");
            throw;
        }
    }

    private void OnEngineState(object? sender, EngineEventArgs e)
    {
        App.DispatchToUi(() => ApplyStatus(e.Snapshot));
    }

    private void ApplyStatus(EngineSnapshot snap)
    {
        StatusPillText.Text = snap.Status switch
        {
            EngineStatus.Running when snap.Enabled => "ACTIVE",
            EngineStatus.Running => "DISABLED",
            EngineStatus.NotStarted => "INITIALIZING",
            EngineStatus.StoppedAfterError => "ENGINE ERROR",
            EngineStatus.StoppedByUser => "STOPPED",
            EngineStatus.StoppedForShutdown => "SHUTTING DOWN",
            _ => "UNKNOWN",
        };
        var (ok, muted, err) = snap.Status switch
        {
            EngineStatus.Running when snap.Enabled => (true, false, false),
            EngineStatus.Running => (false, true, false),
            EngineStatus.StoppedAfterError => (false, false, true),
            _ => (false, false, true),
        };
        PillOk.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        PillMuted.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
        PillErr.Visibility = err ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        App.Instance!.Controller?.EmergencyStop();
        Shell.SyncFromEngine();
    }
}

