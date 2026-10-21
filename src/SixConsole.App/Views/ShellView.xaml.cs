using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SixConsole.App.Input;
using SixConsole.App.Services;
using SixConsole.App.Views;

namespace SixConsole.App.Views;

public sealed partial class ShellView : UserControl
{
    public ShellView()
    {
        InitializeComponent();
        var services = App.Instance?.Services;
        var controller = App.Instance?.Controller;
        if (controller != null)
        {
            controller.StateChanged += OnEngineState;
        }
        if (services != null)
        {
            // Selection changes from the UI (tiles/weapon list) never produce engine events;
            // the status bar must still reflect them immediately. Same for Settings Save.
            services.Selection.Changed += RefreshStatusBar;
            services.SettingsApplied += RefreshStatusBar;
        }
        RefreshStatusBar();
    }

    private void RefreshStatusBar()
    {
        var snap = App.Instance?.Controller?.Snapshot();
        if (snap != null)
        {
            App.DispatchToUi(() => StatusBar.Update(snap));
        }
    }

    private void OnEngineState(object? sender, EngineEventArgs e)
    {
        App.DispatchToUi(() =>
        {
            StatusBar.Update(e.Snapshot);
        });
    }

    /// <summary>Forces one status refresh (used after in-place interactions like STOP).</summary>
    public void SyncFromEngine() => RefreshStatusBar();

    public void NavigateTo(string tag)
    {
        App.DispatchToUi(() =>
        {
            foreach (var item in Nav.MenuItems)
            {
                if (item is NavigationViewItem nvi && (nvi.Tag as string) == tag)
                {
                    Nav.SelectedItem = nvi;
                    break;
                }
            }
        });
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItemContainer?.Tag as string) ?? "profile";
        switch (tag)
        {
            case "settings":
                if (ContentFrame.Content is not SettingsView) ContentFrame.Content = new SettingsView();
                break;
            case "help":
                if (ContentFrame.Content is not HelpView) ContentFrame.Content = new HelpView();
                break;
            default:
                if (ContentFrame.Content is not ProfileView) ContentFrame.Content = new ProfileView();
                break;
        }
    }
}
