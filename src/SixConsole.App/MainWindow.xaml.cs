using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using SixConsole.App.Input;
using SixConsole.App.Services;
using SixConsole.App.Services.Shortcuts;
using SixConsole.App.Views;
using SixConsole.Profile;

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

            var services = App.Instance?.Services;
            var controller = App.Instance?.Controller;

            if (controller != null)
            {
                controller.StateChanged += OnEngineState;
                // The engine may already be running when this window subscribes; pull current state.
                App.DispatchToUi(() => ApplyStatus(controller.Snapshot()));
            }

            // Register application shortcut actions
            var shortcuts = services?.Shortcuts;
            if (shortcuts != null)
            {
                shortcuts.RegisterAction(ShortcutActionIds.NavigateToProfile, () => Shell.NavigateTo("profile"));
                shortcuts.RegisterAction(ShortcutActionIds.NavigateToSettings, () => Shell.NavigateTo("settings"));
                shortcuts.RegisterAction(ShortcutActionIds.NavigateToHelp, () => Shell.NavigateTo("help"));
                shortcuts.RegisterAction(ShortcutActionIds.ToggleProfileActive, () => App.Instance?.Controller?.Toggle());
                shortcuts.RegisterAction(ShortcutActionIds.NextOperator, () => services?.Selection.NavigateOperator(NavigationDirection.Next));
                shortcuts.RegisterAction(ShortcutActionIds.PreviousOperator, () => services?.Selection.NavigateOperator(NavigationDirection.Previous));
                shortcuts.RegisterAction(ShortcutActionIds.NextWeapon, () => services?.Selection.NavigateWeapon(NavigationDirection.Next));
                shortcuts.RegisterAction(ShortcutActionIds.PreviousWeapon, () => services?.Selection.NavigateWeapon(NavigationDirection.Previous));
            }

            // Route key events through the single shortcut dispatcher service
            if (Content is UIElement rootElement)
            {
                rootElement.PreviewKeyDown += OnRootPreviewKeyDown;
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

    private void OnRootPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var shortcuts = App.Instance?.Services?.Shortcuts;
        if (shortcuts == null || shortcuts.IsRecording) return;

        var modifiers = KeyModifiers.None;
        if ((Win32.GetKeyState(0x11) & 0x8000) != 0) modifiers |= KeyModifiers.Control;
        if ((Win32.GetKeyState(0x12) & 0x8000) != 0) modifiers |= KeyModifiers.Alt;
        if ((Win32.GetKeyState(0x10) & 0x8000) != 0) modifiers |= KeyModifiers.Shift;
        if (((Win32.GetKeyState(0x5B) | Win32.GetKeyState(0x5C)) & 0x8000) != 0) modifiers |= KeyModifiers.Windows;

        int vk = (int)e.Key;
        if (shortcuts.Dispatch(vk, modifiers, ShortcutScope.AppScoped))
        {
            e.Handled = true;
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
