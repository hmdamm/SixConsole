using SixConsole.App.Services.Shortcuts;
using System.Text.Json;

namespace SixConsole.App.Services;

/// <summary>
/// Safe, non-executable configuration import.
/// Strictly validates JSON-based benign preferences (Theme, UI shortcuts, Profile name)
/// and absolutely rejects any executable scripts, gameplay automation, recoil tuning,
/// rapid-fire definitions, or input injection.
/// </summary>
public sealed class SafeConfigImporter
{
    public const int MaxFileSizeBytes = 64 * 1024; // 64 KB limit

    private static readonly HashSet<string> AllowedActionIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ShortcutActionIds.NavigateToProfile,
        ShortcutActionIds.NavigateToSettings,
        ShortcutActionIds.NavigateToHelp,
        ShortcutActionIds.ToggleProfileActive,
        ShortcutActionIds.NextOperator,
        ShortcutActionIds.PreviousOperator,
        ShortcutActionIds.NextWeapon,
        ShortcutActionIds.PreviousWeapon,
    };

    private static readonly string[] ProhibitedPatterns =
    {
        "function", "repeat", "until", "while", "end",
        "dofile", "require", "loadstring", "load", "eval",
        "PressKey", "ReleaseKey", "SendInput", "MoveMouseRelative",
        "OutputLogMessage", "IsMouseButtonPressed", "EnablePrimaryMouseButtonEvents",
        "recoilMultiplier", "vertVariance", "horizJitter",
        "minDelay", "maxDelay", "minHold", "maxHold", "minGap", "maxGap",
        "rfKey", "pull", "rapidFire"
    };

    public sealed class SafeImportShortcut
    {
        public string ActionId { get; set; } = "";
        public int VirtualKey { get; set; }
        public int Modifiers { get; set; }
        public string Key { get; set; } = "";
        public int Scope { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    public sealed class SafeAppConfig
    {
        public int SchemaVersion { get; set; } = 2;
        public string ProfileName { get; set; } = "";
        public string? ThemePreference { get; set; }
        public bool? RestoreLastSelection { get; set; }
        public List<SafeImportShortcut>? Shortcuts { get; set; }
    }

    public sealed class ValidationResult
    {
        public bool IsValid => Errors.Count == 0 && AcceptedChanges.Count > 0;
        public List<string> AcceptedChanges { get; } = new();
        public List<string> Errors { get; } = new();
        public SafeAppConfig? Config { get; set; }
    }

    public static ValidationResult Validate(string content)
    {
        var result = new ValidationResult();

        if (content.Length > MaxFileSizeBytes)
        {
            result.Errors.Add($"Configuration file exceeds safe size limit ({MaxFileSizeBytes} bytes).");
            return result;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            result.Errors.Add("Configuration content is empty.");
            return result;
        }

        // 1. Proactive security check: reject executable scripts and gameplay automation
        foreach (var pattern in ProhibitedPatterns)
        {
            if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add($"The configuration contains prohibited automation or script token '{pattern}'. Safe import only accepts non-executable benign UI preferences.");
                return result;
            }
        }

        // 2. Strict JSON parsing
        SafeAppConfig? config;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                MaxDepth = 4,
            };
            config = JsonSerializer.Deserialize<SafeAppConfig>(content, options);
            if (config == null)
            {
                result.Errors.Add("Could not parse configuration as valid JSON.");
                return result;
            }
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"Invalid JSON formatting: {ex.Message}");
            return result;
        }

        // 3. Schema version verification
        if (config.SchemaVersion is < 1 or > SettingsStore.CurrentSchemaVersion)
        {
            result.Errors.Add($"Unsupported schema version {config.SchemaVersion}. Supported versions: 1 to {SettingsStore.CurrentSchemaVersion}.");
            return result;
        }

        // 4. Validate benign profile name if provided
        if (!string.IsNullOrWhiteSpace(config.ProfileName))
        {
            if (config.ProfileName.Length > 64)
            {
                result.Errors.Add("ProfileName exceeds the 64 character limit.");
            }
            else
            {
                result.AcceptedChanges.Add($"Profile name: \"{config.ProfileName.Trim()}\"");
            }
        }

        // 5. Validate ThemePreference if provided
        if (!string.IsNullOrEmpty(config.ThemePreference))
        {
            if (config.ThemePreference is "System" or "Light" or "Dark")
            {
                result.AcceptedChanges.Add($"Theme preference: {config.ThemePreference}");
            }
            else
            {
                result.Errors.Add($"Invalid theme preference \"{config.ThemePreference}\". Allowed values: System, Light, Dark.");
            }
        }

        // 6. Validate RestoreLastSelection if provided
        if (config.RestoreLastSelection.HasValue)
        {
            result.AcceptedChanges.Add($"Restore last selection: {config.RestoreLastSelection.Value}");
        }

        // 7. Validate Shortcuts if provided
        if (config.Shortcuts != null && config.Shortcuts.Count > 0)
        {
            var bindings = new List<ShortcutBinding>();
            foreach (var s in config.Shortcuts)
            {
                if (!AllowedActionIds.Contains(s.ActionId))
                {
                    result.Errors.Add($"Unknown or prohibited shortcut action '{s.ActionId}'.");
                    continue;
                }

                var binding = new ShortcutBinding(
                    s.ActionId,
                    s.ActionId,
                    s.VirtualKey,
                    s.Key,
                    (KeyModifiers)s.Modifiers,
                    (ShortcutScope)s.Scope,
                    s.IsEnabled);

                if (!ShortcutValidator.ValidateBinding(binding, out var bindError))
                {
                    result.Errors.Add($"Shortcut for '{s.ActionId}': {bindError}");
                    continue;
                }

                bindings.Add(binding);
            }

            if (ShortcutValidator.CheckForConflicts(bindings, out var conflictError))
            {
                result.Errors.Add(conflictError);
            }
            else
            {
                foreach (var b in bindings)
                {
                    result.AcceptedChanges.Add($"Shortcut {b.ActionId}: {b.DisplayString}");
                }
            }
        }

        if (result.AcceptedChanges.Count == 0 && result.Errors.Count == 0)
        {
            result.Errors.Add("No valid benign configuration settings were found in the file.");
        }

        result.Config = config;
        return result;
    }

    public static bool Apply(ValidationResult validation, SettingsStore store, AppServices services, out string error)
    {
        error = "";
        if (!validation.IsValid || validation.Config == null)
        {
            error = "Cannot apply invalid configuration.";
            return false;
        }

        var config = validation.Config;
        var original = services.SnapshotPersisted();

        try
        {
            // Transactional update on a clone
            var updated = services.SnapshotPersisted();

            if (!string.IsNullOrEmpty(config.ThemePreference))
            {
                updated.ThemePreference = config.ThemePreference;
            }

            if (config.RestoreLastSelection.HasValue)
            {
                updated.RestoreLastSelection = config.RestoreLastSelection.Value;
            }

            if (config.Shortcuts != null && config.Shortcuts.Count > 0)
            {
                var existingShortcuts = updated.Shortcuts.ToDictionary(s => s.ActionId, StringComparer.OrdinalIgnoreCase);
                foreach (var s in config.Shortcuts)
                {
                    if (AllowedActionIds.Contains(s.ActionId))
                    {
                        var p = new SettingsStore.PersistedShortcut
                        {
                            ActionId = s.ActionId,
                            ActionName = s.ActionId,
                            Key = s.Key,
                            VirtualKey = s.VirtualKey,
                            Modifiers = s.Modifiers,
                            Scope = s.Scope,
                            IsEnabled = s.IsEnabled,
                        };
                        existingShortcuts[s.ActionId] = p;
                    }
                }
                updated.Shortcuts = existingShortcuts.Values.ToList();
            }

            // Save to store (this creates a backup automatically)
            store.Save(updated);

            // Update live services
            if (config.Shortcuts != null && config.Shortcuts.Count > 0)
            {
                services.Shortcuts.UpdateBindings(updated.Shortcuts.Select(s => s.ToBinding()), out _);
            }

            if (!string.IsNullOrEmpty(config.ThemePreference) && App.Instance != null)
            {
                App.Instance.ThemeService.Apply(ThemeService.ChoiceFrom(config.ThemePreference), static (_, _) => { });
            }

            return true;
        }
        catch (Exception ex)
        {
            // Rollback on failure
            try { store.Save(original); } catch { }
            error = $"Import failed and was rolled back: {ex.Message}";
            return false;
        }
    }
}
