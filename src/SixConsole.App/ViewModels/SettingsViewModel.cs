using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SixConsole.App.Services;
using SixConsole.Profile;

namespace SixConsole.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppServices _services;

    // Draft values (edits stay local until Save)
    [ObservableProperty] private double recoilMultiplier;
    [ObservableProperty] private int vertVariance;
    [ObservableProperty] private int horizJitter;
    [ObservableProperty] private int delayMin;
    [ObservableProperty] private int delayMax;
    [ObservableProperty] private string rfKey = "";
    [ObservableProperty] private int holdMin;
    [ObservableProperty] private int holdMax;
    [ObservableProperty] private int gapMin;
    [ObservableProperty] private int gapMax;

    [ObservableProperty]
    private string validationSummary = "";

    [ObservableProperty]
    private string theme;

    [ObservableProperty]
    private string dataDirectory;

    [ObservableProperty]
    private string statusBanner = "";

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        var s = _services.Settings;
        RecoilMultiplier = s.RecoilMultiplier;
        VertVariance = s.VertVariance;
        HorizJitter = s.HorizJitter;
        DelayMin = s.Delay.Min;
        DelayMax = s.Delay.Max;
        RfKey = s.RfKey;
        HoldMin = s.Hold.Min;
        HoldMax = s.Hold.Max;
        GapMin = s.Gap.Min;
        GapMax = s.Gap.Max;
        theme = SettingsStore.ThemeToString(_services.Persisted.ThemePreference);
        DataDirectory = SettingsStore.DataDirectory;
    }

    [RelayCommand]
    private void Validate()
    {
        RunValidation();
    }

    private void RunValidation()
    {
        var errors = ProfileSettings.Validate(
            RecoilMultiplier, VertVariance, HorizJitter, DelayMin, DelayMax,
            RfKey, HoldMin, HoldMax, GapMin, GapMax);
        ValidationSummary = string.Join(" ", errors);
    }

    private ProfileSettings Draft()
    {
        return ProfileSettings.Create(
            RecoilMultiplier, VertVariance, HorizJitter, DelayMin, DelayMax,
            RfKey, HoldMin, HoldMax, GapMin, GapMax);
    }

    [RelayCommand]
    private void Save()
    {
        RunValidation();
        if (ValidationSummary.Length > 0)
        {
            StatusBanner = "Errors — nothing was saved or applied.";
            return;
        }
        try
        {
            var draft = Draft(); // throws if invalid (belt and braces)
            _services.ApplySettings(draft);
            StatusBanner = "Applied and saved.";
        }
        catch (SettingsValidationException ex)
        {
            StatusBanner = "Errors — nothing was saved or applied.";
            ValidationSummary = string.Join(" ", ex.Errors);
        }
    }

    [RelayCommand]
    private void Revert()
    {
        var s = _services.Settings;
        RecoilMultiplier = s.RecoilMultiplier;
        VertVariance = s.VertVariance;
        HorizJitter = s.HorizJitter;
        DelayMin = s.Delay.Min;
        DelayMax = s.Delay.Max;
        RfKey = s.RfKey;
        HoldMin = s.Hold.Min;
        HoldMax = s.Hold.Max;
        GapMin = s.Gap.Min;
        GapMax = s.Gap.Max;
        RunValidation();
        StatusBanner = "Reverted to the applied values.";
    }

    /// <summary>Called by the view after the user confirms the restore-defaults dialog.</summary>
    public void RestoreDefaults()
    {
        var d = ProfileSettings.Defaults();
        _services.ApplySettings(d);
        Revert();
        StatusBanner = "Defaults from the original script restored.";
    }

    /// <summary>Theme is applied immediately by the view; this records the choice.</summary>
    public void SetTheme(string themeChoice)
    {
        Theme = themeChoice;
        _services.Persisted.ThemePreference = themeChoice;
        _services.SettingsStore.Save(_services.Persisted);
    }

    partial void OnRecoilMultiplierChanged(double value) => RunValidation();
    partial void OnVertVarianceChanged(int value) => RunValidation();
    partial void OnHorizJitterChanged(int value) => RunValidation();
    partial void OnDelayMinChanged(int value) => RunValidation();
    partial void OnDelayMaxChanged(int value) => RunValidation();
    partial void OnRfKeyChanged(string value) => RunValidation();
    partial void OnHoldMinChanged(int value) => RunValidation();
    partial void OnHoldMaxChanged(int value) => RunValidation();
    partial void OnGapMinChanged(int value) => RunValidation();
    partial void OnGapMaxChanged(int value) => RunValidation();
}
