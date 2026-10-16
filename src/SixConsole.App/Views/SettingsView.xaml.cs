using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SixConsole.App.Services;
using SixConsole.App.ViewModels;

namespace SixConsole.App.Views;

public sealed partial class SettingsView : UserControl
{
    public SettingsViewModel ViewModel { get; }

    public SettingsView()
    {
        ViewModel = new SettingsViewModel(App.Instance!.Services);
        InitializeComponent();
        SelectThemeRadioButton(App.Instance!.ThemeService.Current.ToString());
    }

    private void SelectThemeRadioButton(string themeChoice)
    {
        for (int i = 0; i < ThemePicker.Items.Count; i++)
        {
            if ((ThemePicker.Items[i] as RadioButton)?.Tag as string == themeChoice)
            {
                ThemePicker.SelectedIndex = i;
                return;
            }
        }
    }

    private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemePicker.SelectedItem is RadioButton rb && rb.Tag is string tag)
        {
            // Skip when the change comes from the constructor's initial selection: applying the
            // same theme again is harmless, but re-saving settings on every Settings-open is not.
            if (tag == ThemeService.Label(App.Instance!.ThemeService.Current) && ViewModel.Theme == tag) return;
            App.Instance!.ThemeService.Apply(ThemeService.ChoiceFrom(tag), static (_, _) => { });
            ViewModel.SetTheme(tag);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => ViewModel.SaveCommand.Execute(null);

    private void Revert_Click(object sender, RoutedEventArgs e) => ViewModel.RevertCommand.Execute(null);

    private async void Defaults_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Restore original script defaults?",
            Content = "This overwrites your current settings: sensitivity 0.9×, variance 1px, jitter 1px, delay 2–5 ms, rapid-fire key P, hold 25–40 ms, gap 65–90 ms.",
            PrimaryButtonText = "Restore defaults",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.RestoreDefaults();
        }
    }
}
