using SixConsole.Profile;

namespace SixConsole.App.Services;

/// <summary>Read/write JSON settings persisted under %LOCALAPPDATA%\SixConsole\settings.json.
/// A directory override (tests / auxiliary runs) redirects the file without touching user data.</summary>
public sealed class SettingsStore
{
    private static readonly string DefaultDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SixConsole");

    private readonly string _dir;
    private readonly string _filePath;

    public SettingsStore(string? directoryOverride = null)
    {
        _dir = directoryOverride ?? DefaultDir;
        _filePath = Path.Combine(_dir, "settings.json");
    }

    public static string LogFilePath => Path.Combine(DefaultDir, "log.txt");
    public static string DataDirectory => DefaultDir;
    public string LocalDataDirectory => _dir;

    public sealed class PersistedSettings
    {
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
        public bool RestoreLastSelection { get; set; } = true; // documented choice, see README
    }

    public PersistedSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var loaded = System.Text.Json.JsonSerializer.Deserialize<PersistedSettings>(File.ReadAllText(_filePath));
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // fall through to defaults on any malformed or unreadable file
        }
        return new PersistedSettings();
    }

    // All file access is serialized: saves can originate from the UI thread, the input hook
    // thread, and background save workers concurrently; IOException from interleaved writes
    // must never surface to users.
    private static readonly object FileGate = new();

    public void Save(PersistedSettings settings)
    {
        try
        {
            lock (FileGate)
            {
                Directory.CreateDirectory(_dir);
                var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
        }
        catch
        {
            // diagnostics-only: never let persistence failure break the interaction it supports
        }
    }

    public static string ThemeToString(string themePreference) => themePreference switch
    {
        "Dark" => "Dark",
        "Light" => "Light",
        _ => "System",
    };
}
