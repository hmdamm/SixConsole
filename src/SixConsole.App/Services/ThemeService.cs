using Microsoft.UI.Xaml;

namespace SixConsole.App.Services;

/// <summary>Theme preference model: System / Light / Dark, applied immediately, persisted locally.</summary>
public sealed class ThemeService
{
    private readonly App _app;
    private readonly SettingsStore _store;

    public ThemeService(App app, SettingsStore store)
    {
        _app = app;
        _store = store;
    }

    public enum ThemeChoice
    {
        System,
        Light,
        Dark,
    }

    public static string Label(ThemeChoice choice) => choice switch
    {
        ThemeChoice.Dark => "Dark",
        ThemeChoice.Light => "Light",
        _ => "System",
    };

    public static ThemeChoice ChoiceFrom(string label) => label switch
    {
        "Dark" => ThemeChoice.Dark,
        "Light" => ThemeChoice.Light,
        _ => ThemeChoice.System,
    };

    public ThemeChoice Current { get; private set; } = ThemeChoice.System;

    private FrameworkElement? ContentRoot => _app.MainWindowInstance.Content as FrameworkElement;

    public void Load() => Current = Parse(_store.Load().ThemePreference);

    public void Apply() => Apply(Current, static (_, _) => { });

    public void Apply(ThemeChoice choice, Action<ThemeChoice, ElementTheme> onApplied)
    {
        Current = choice;
        // Persistence is owned by SettingsViewModel.SetTheme (which saves the JSON); this method
        // only applies the visual theme to the content root.
        var preferred = choice switch
        {
            ThemeChoice.Dark => ElementTheme.Dark,
            ThemeChoice.Light => ElementTheme.Light,
            _ => ElementTheme.Default, // System: follow Windows
        };
        var applied = preferred;
        if (ContentRoot is { } root)
        {
            root.RequestedTheme = preferred;
            applied = root.RequestedTheme;
        }
        onApplied(choice, applied);
    }

    private static ThemeChoice Parse(string label) => label switch
    {
        "Dark" => ThemeChoice.Dark,
        "Light" => ThemeChoice.Light,
        _ => ThemeChoice.System,
    };
}
