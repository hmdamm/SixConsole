using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SixConsole.App.Services;
using SixConsole.App.Services.Shortcuts;
using SixConsole.Profile;
using System.Collections.ObjectModel;

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

    [ObservableProperty]
    private string shortcutValidationMessage = "";

    [ObservableProperty]
    private string importStatusMessage = "";

    [ObservableProperty]
    private bool isImportError;

    public ObservableCollection<ShortcutItemViewModel> Shortcuts { get; } = new();
    public ShortcutItemViewModel? RecordingItem { get; private set; }

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

        LoadShortcuts();
    }

    private void LoadShortcuts()
    {
        Shortcuts.Clear();
        var persisted = _services.Persisted.Shortcuts;
        foreach (var p in persisted)
        {
            Shortcuts.Add(new ShortcutItemViewModel(p.ToBinding()));
        }
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
        CancelRecording();
        RunValidation();
        if (ValidationSummary.Length > 0)
        {
            StatusBanner = "Errors — nothing was saved or applied.";
            return;
        }

        // Validate shortcuts
        var candidateBindings = Shortcuts.Select(s => s.ToBinding()).ToList();
        foreach (var b in candidateBindings)
        {
            if (!ShortcutValidator.ValidateBinding(b, out var bindError))
            {
                ShortcutValidationMessage = $"{b.ActionName}: {bindError}";
                StatusBanner = "Errors in shortcuts — nothing was saved or applied.";
                return;
            }
        }

        if (ShortcutValidator.CheckForConflicts(candidateBindings, out var conflictError))
        {
            ShortcutValidationMessage = conflictError;
            StatusBanner = "Shortcut conflict — nothing was saved or applied.";
            return;
        }

        try
        {
            var draft = Draft(); // throws if invalid (belt and braces)
            _services.ApplySettings(draft);

            if (!_services.ApplyShortcuts(candidateBindings, out var applyErr))
            {
                ShortcutValidationMessage = applyErr;
                StatusBanner = "Errors applying shortcuts.";
                return;
            }

            ShortcutValidationMessage = "";
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
        CancelRecording();
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

        LoadShortcuts();
        ShortcutValidationMessage = "";
        StatusBanner = "Reverted to the applied values.";
    }

    /// <summary>Called by the view after the user confirms the restore-defaults dialog.</summary>
    public void RestoreDefaults()
    {
        CancelRecording();
        var d = ProfileSettings.Defaults();
        _services.ApplySettings(d);

        var defaultShortcuts = ShortcutDefaults.CreateDefaults();
        _services.ApplyShortcuts(defaultShortcuts, out _);

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

    // --- Shortcut editor management ---

    public void StartRecording(ShortcutItemViewModel item)
    {
        if (RecordingItem != null && RecordingItem != item)
        {
            RecordingItem.IsRecording = false;
        }

        RecordingItem = item;
        item.IsRecording = true;
        _services.Shortcuts.IsRecording = true;
        ShortcutValidationMessage = "";
        StatusBanner = $"Recording shortcut for '{item.ActionName}'... Press key combination (or Escape to cancel).";
    }

    public void CancelRecording()
    {
        if (RecordingItem != null)
        {
            RecordingItem.IsRecording = false;
            RecordingItem = null;
        }
        _services.Shortcuts.IsRecording = false;
        ShortcutValidationMessage = "";
    }

    public bool RecordKey(int virtualKey, KeyModifiers modifiers)
    {
        if (RecordingItem == null) return false;

        // Escape cancels recording without saving partial binding
        if (virtualKey == 0x1B && modifiers == KeyModifiers.None)
        {
            CancelRecording();
            StatusBanner = "Shortcut recording canceled.";
            return true;
        }

        if (ShortcutValidator.IsReservedKeyCombination(virtualKey, modifiers, out var reservedReason))
        {
            ShortcutValidationMessage = reservedReason;
            return false;
        }

        var keyName = ShortcutBinding.FormatKeyName(virtualKey, "");
        var candidate = new ShortcutBinding(
            RecordingItem.ActionId,
            RecordingItem.ActionName,
            virtualKey,
            keyName,
            modifiers,
            RecordingItem.Scope,
            isEnabled: true);

        // Check conflicts against other items
        var testList = Shortcuts.Select(s => s == RecordingItem ? candidate : s.ToBinding()).ToList();
        if (ShortcutValidator.CheckForConflicts(testList, out var conflictError))
        {
            ShortcutValidationMessage = conflictError;
            return false;
        }

        RecordingItem.UpdateFromBinding(candidate);
        RecordingItem.IsRecording = false;
        RecordingItem = null;
        _services.Shortcuts.IsRecording = false;
        ShortcutValidationMessage = "";
        StatusBanner = $"Captured {candidate.ActionName}: {candidate.DisplayString}. Click Save to apply.";
        return true;
    }

    public void ResetShortcut(ShortcutItemViewModel item)
    {
        CancelRecording();
        var def = ShortcutDefaults.CreateDefaults().FirstOrDefault(d => string.Equals(d.ActionId, item.ActionId, StringComparison.OrdinalIgnoreCase));
        if (def != null)
        {
            item.UpdateFromBinding(def);
            ShortcutValidationMessage = "";
            StatusBanner = $"Reset {item.ActionName} to default ({def.DisplayString}). Click Save to apply.";
        }
    }

    public void ResetAllShortcuts()
    {
        CancelRecording();
        var defaults = ShortcutDefaults.CreateDefaults().ToDictionary(d => d.ActionId, StringComparer.OrdinalIgnoreCase);
        foreach (var item in Shortcuts)
        {
            if (defaults.TryGetValue(item.ActionId, out var def))
            {
                item.UpdateFromBinding(def);
            }
        }
        ShortcutValidationMessage = "";
        StatusBanner = "All shortcuts reset to defaults. Click Save to apply.";
    }

    // --- Safe configuration import ---

    public SafeConfigImporter.ValidationResult ValidateImportContent(string jsonContent)
    {
        return SafeConfigImporter.Validate(jsonContent);
    }

    public bool ApplyImport(SafeConfigImporter.ValidationResult validation)
    {
        if (!SafeConfigImporter.Apply(validation, _services.SettingsStore, _services, out var error))
        {
            ImportStatusMessage = error;
            IsImportError = true;
            return false;
        }

        // Reload updated settings and shortcuts
        _services.ReloadPersisted();
        Theme = SettingsStore.ThemeToString(_services.Persisted.ThemePreference);
        LoadShortcuts();
        ImportStatusMessage = "Configuration imported successfully.";
        IsImportError = false;
        StatusBanner = "Imported configuration applied.";
        return true;
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
