using SixConsole.App.Services.Shortcuts;
using SixConsole.Profile;
using System.Text.Json;

namespace SixConsole.App.Services;

/// <summary>Read/write JSON settings persisted under %LOCALAPPDATA%\SixConsole\settings.json.
/// A directory override (tests / auxiliary runs) redirects the file without touching user data.</summary>
public sealed class SettingsStore
{
    public const int CurrentSchemaVersion = 2;

    private static readonly string DefaultDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SixConsole");

    private readonly string _dir;
    private readonly string _filePath;
    private readonly string _backupFilePath;
    private readonly string _tempFilePath;

    public SettingsStore(string? directoryOverride = null)
    {
        _dir = directoryOverride ?? DefaultDir;
        _filePath = Path.Combine(_dir, "settings.json");
        _backupFilePath = Path.Combine(_dir, "settings.json.bak");
        _tempFilePath = Path.Combine(_dir, "settings.json.tmp");
    }

    public static string LogFilePath => Path.Combine(DefaultDir, "log.txt");
    public static string DataDirectory => DefaultDir;
    public string LocalDataDirectory => _dir;

    public sealed class PersistedShortcut
    {
        public string ActionId { get; set; } = "";
        public string ActionName { get; set; } = "";
        public string Key { get; set; } = "";
        public int VirtualKey { get; set; }
        public int Modifiers { get; set; }
        public int Scope { get; set; }
        public bool IsEnabled { get; set; } = true;

        public static PersistedShortcut FromBinding(ShortcutBinding b) => new()
        {
            ActionId = b.ActionId,
            ActionName = b.ActionName,
            Key = b.Key,
            VirtualKey = b.VirtualKey,
            Modifiers = (int)b.Modifiers,
            Scope = (int)b.Scope,
            IsEnabled = b.IsEnabled,
        };

        public ShortcutBinding ToBinding() => new(
            ActionId,
            ActionName,
            VirtualKey,
            Key,
            (KeyModifiers)Modifiers,
            (ShortcutScope)Scope,
            IsEnabled);
    }

    public sealed class PersistedSettings
    {
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public double RecoilMultiplier { get; set; } = ProfileSettings.DefaultRecoilMultiplier;
        public int VertVariance { get; set; } = ProfileSettings.DefaultVertVariance;
        public int HorizJitter { get; set; } = ProfileSettings.DefaultHorizJitter;
        public int DelayMin { get; set; } = ProfileSettings.DefaultMinDelay;
        public int DelayMax { get; set; } = ProfileSettings.DefaultMaxDelay;
        public string RfKey { get; set; } = ProfileSettings.DefaultRfKey;
        public int HoldMin { get; set; } = ProfileSettings.DefaultMinHold;
        public int HoldMax { get; set; } = ProfileSettings.DefaultMaxHold;
        public int GapMin { get; set; } = ProfileSettings.DefaultMinGap;
        public int GapMax { get; set; } = ProfileSettings.DefaultMaxGap;
        public string ThemePreference { get; set; } = "System";
        public int LastOperatorIndex { get; set; } = 0;
        public int LastWeaponIndex { get; set; } = 0;
        public bool RestoreLastSelection { get; set; } = true;
        public List<PersistedShortcut> Shortcuts { get; set; } = new();

        public PersistedSettings()
        {
            // By default, initialize with standard shortcuts
            Shortcuts = ShortcutDefaults.CreateDefaults()
                .Select(PersistedShortcut.FromBinding)
                .ToList();
        }
    }

    public PersistedSettings Load()
    {
        lock (FileGate)
        {
            // 1. Try reading the primary settings file
            if (File.Exists(_filePath))
            {
                var loaded = TryDeserializeFile(_filePath);
                if (loaded != null)
                {
                    return EnsureMigrated(loaded);
                }
            }

            // 2. Fall back to the backup file if primary was corrupt/missing
            if (File.Exists(_backupFilePath))
            {
                var fromBackup = TryDeserializeFile(_backupFilePath);
                if (fromBackup != null)
                {
                    return EnsureMigrated(fromBackup);
                }
            }

            // 3. Fall through to defaults if no readable file exists
            return new PersistedSettings();
        }
    }

    private static PersistedSettings? TryDeserializeFile(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return null;
            return JsonSerializer.Deserialize<PersistedSettings>(text, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static PersistedSettings EnsureMigrated(PersistedSettings settings)
    {
        // Migration from SchemaVersion 1 (or unversioned) to SchemaVersion 2:
        if (settings.SchemaVersion < 2 || settings.Shortcuts == null || settings.Shortcuts.Count == 0)
        {
            var defaults = ShortcutDefaults.CreateDefaults();
            var existingByAction = (settings.Shortcuts ?? new List<PersistedShortcut>())
                .ToDictionary(s => s.ActionId, StringComparer.OrdinalIgnoreCase);

            var merged = new List<PersistedShortcut>();
            foreach (var def in defaults)
            {
                if (existingByAction.TryGetValue(def.ActionId, out var existing))
                {
                    merged.Add(existing);
                }
                else
                {
                    merged.Add(PersistedShortcut.FromBinding(def));
                }
            }

            settings.Shortcuts = merged;
            settings.SchemaVersion = CurrentSchemaVersion;
        }

        return settings;
    }

    private static readonly object FileGate = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public void Save(PersistedSettings settings)
    {
        lock (FileGate)
        {
            try
            {
                Directory.CreateDirectory(_dir);
                var json = JsonSerializer.Serialize(settings, JsonOptions);

                // Atomic write via temp file
                File.WriteAllText(_tempFilePath, json);

                // Create or update backup of previous known-good file
                if (File.Exists(_filePath))
                {
                    try
                    {
                        File.Copy(_filePath, _backupFilePath, overwrite: true);
                    }
                    catch
                    {
                        // Backup failure should not block primary save
                    }
                }

                // Replace primary file atomically
                File.Move(_tempFilePath, _filePath, overwrite: true);
            }
            catch
            {
                // Clean up temp file on failure
                try { if (File.Exists(_tempFilePath)) File.Delete(_tempFilePath); } catch { }
            }
        }
    }

    public static string ThemeToString(string themePreference) => themePreference switch
    {
        "Dark" => "Dark",
        "Light" => "Light",
        _ => "System",
    };
}
