using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SixConsole.App.Input;
using SixConsole.App.Services;
using SixConsole.App.Services.Shortcuts;
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

        Loaded += (_, _) =>
        {
            if (App.Instance?.MainWindowInstance.Content is UIElement root)
            {
                root.PreviewKeyDown += OnRootPreviewKeyDown;
            }
        };

        Unloaded += (_, _) =>
        {
            ViewModel.CancelRecording();
            if (App.Instance?.MainWindowInstance.Content is UIElement root)
            {
                root.PreviewKeyDown -= OnRootPreviewKeyDown;
            }
        };
    }

    private void OnRootPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel.RecordingItem == null) return;

        var modifiers = KeyModifiers.None;
        if ((Win32.GetKeyState(0x11) & 0x8000) != 0) modifiers |= KeyModifiers.Control;
        if ((Win32.GetKeyState(0x12) & 0x8000) != 0) modifiers |= KeyModifiers.Alt;
        if ((Win32.GetKeyState(0x10) & 0x8000) != 0) modifiers |= KeyModifiers.Shift;
        if (((Win32.GetKeyState(0x5B) | Win32.GetKeyState(0x5C)) & 0x8000) != 0) modifiers |= KeyModifiers.Windows;

        int vk = (int)e.Key;
        // Don't commit on standalone modifier key presses
        if (vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5)
        {
            return;
        }

        if (ViewModel.RecordKey(vk, modifiers))
        {
            e.Handled = true;
        }
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
            Content = "This overwrites your current settings and resets shortcuts to default: sensitivity 0.9×, variance 1px, jitter 1px, delay 2–5 ms, rapid-fire key P, hold 25–40 ms, gap 65–90 ms.",
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

    private void RecordShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ShortcutItemViewModel item })
        {
            ViewModel.StartRecording(item);
        }
    }

    private void ResetShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ShortcutItemViewModel item })
        {
            ViewModel.ResetShortcut(item);
        }
    }

    private void ResetAllShortcuts_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetAllShortcuts();
    }

    private async void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var inputTextBox = new TextBox
        {
            AcceptsReturn = true,
            Height = 120,
            PlaceholderText = "Paste safe JSON configuration here (e.g. {\"schemaVersion\": 2, \"themePreference\": \"Dark\"})",
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 11,
            CornerRadius = new CornerRadius(4),
        };

        var previewTextBlock = new TextBlock
        {
            Style = (Style)Application.Current.Resources["BaseText"],
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
        };

        var validateButton = new Button
        {
            Content = "Validate & Preview",
            Style = (Style)Application.Current.Resources["ToolButton"],
            Margin = new Thickness(0, 6, 0, 0),
        };

        var browseButton = new Button
        {
            Content = "Browse file…",
            Style = (Style)Application.Current.Resources["ToolButton"],
            Margin = new Thickness(0, 0, 8, 0),
        };

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { browseButton, validateButton }
        };

        var contentPanel = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = "Safe import accepts non-executable preferences (Theme and UI shortcuts). Any executable scripts, gameplay automation, or recoil values are strictly rejected.",
                    Style = (Style)Application.Current.Resources["SecondaryText"],
                    TextWrapping = TextWrapping.Wrap,
                },
                buttonsPanel,
                inputTextBox,
                previewTextBlock,
            }
        };

        SafeConfigImporter.ValidationResult? lastValidation = null;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Safe Configuration Import",
            Content = contentPanel,
            PrimaryButtonText = "Apply import",
            CloseButtonText = "Cancel",
            IsPrimaryButtonEnabled = false,
            DefaultButton = ContentDialogButton.Close,
        };

        browseButton.Click += async (_, _) =>
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.FileTypeFilter.Add(".json");
                picker.FileTypeFilter.Add(".txt");
                picker.FileTypeFilter.Add("*");

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Instance!.MainWindowInstance);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    var text = await Windows.Storage.FileIO.ReadTextAsync(file);
                    inputTextBox.Text = text;
                    validateButton.Command?.Execute(null);
                }
            }
            catch (Exception ex)
            {
                previewTextBlock.Text = $"Could not open file: {ex.Message}";
                previewTextBlock.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ErrorBrush"];
            }
        };

        void RunValidation()
        {
            lastValidation = ViewModel.ValidateImportContent(inputTextBox.Text);
            if (lastValidation.IsValid)
            {
                previewTextBlock.Text = "Accepted changes:\n• " + string.Join("\n• ", lastValidation.AcceptedChanges);
                previewTextBlock.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SuccessBrush"];
                dialog.IsPrimaryButtonEnabled = true;
            }
            else
            {
                previewTextBlock.Text = "Validation errors:\n• " + string.Join("\n• ", lastValidation.Errors);
                previewTextBlock.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ErrorBrush"];
                dialog.IsPrimaryButtonEnabled = false;
            }
        }

        validateButton.Click += (_, _) => RunValidation();
        inputTextBox.TextChanged += (_, _) =>
        {
            dialog.IsPrimaryButtonEnabled = false;
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && lastValidation != null && lastValidation.IsValid)
        {
            ViewModel.ApplyImport(lastValidation);
        }
    }
}
