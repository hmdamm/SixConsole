using SixConsole.App.Services;
using SixConsole.App.Services.Shortcuts;
using System.Text.Json;

namespace SixConsole.Tests;

public sealed class SettingsMigrationTests : IDisposable
{
    private readonly string _dir;

    public SettingsMigrationTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "sixconsole-migration-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void V1_unversioned_settings_migrate_cleanly_to_v2_preserving_all_user_customizations()
    {
        Directory.CreateDirectory(_dir);
        string v1Json = """
        {
          "RecoilMultiplier": 1.45,
          "VertVariance": 3,
          "HorizJitter": 4,
          "DelayMin": 4,
          "DelayMax": 8,
          "RfKey": "k",
          "HoldMin": 20,
          "HoldMax": 35,
          "GapMin": 60,
          "GapMax": 85,
          "ThemePreference": "Dark",
          "LastOperatorIndex": 5,
          "LastWeaponIndex": 1,
          "RestoreLastSelection": true
        }
        """;

        File.WriteAllText(Path.Combine(_dir, "settings.json"), v1Json);

        var store = new SettingsStore(_dir);
        var loaded = store.Load();

        // 1. Schema version must be updated to 2
        Assert.Equal(2, loaded.SchemaVersion);

        // 2. All user customizations must be preserved exactly
        Assert.Equal(1.45, loaded.RecoilMultiplier);
        Assert.Equal(3, loaded.VertVariance);
        Assert.Equal(4, loaded.HorizJitter);
        Assert.Equal(4, loaded.DelayMin);
        Assert.Equal(8, loaded.DelayMax);
        Assert.Equal("k", loaded.RfKey);
        Assert.Equal(20, loaded.HoldMin);
        Assert.Equal(35, loaded.HoldMax);
        Assert.Equal(60, loaded.GapMin);
        Assert.Equal(85, loaded.GapMax);
        Assert.Equal("Dark", loaded.ThemePreference);
        Assert.Equal(5, loaded.LastOperatorIndex);
        Assert.Equal(1, loaded.LastWeaponIndex);

        // 3. New shortcuts must be initialized with default shortcuts
        Assert.NotEmpty(loaded.Shortcuts);
        Assert.Contains(loaded.Shortcuts, s => s.ActionId == ShortcutActionIds.NavigateToSettings);
        Assert.Contains(loaded.Shortcuts, s => s.ActionId == ShortcutActionIds.NavigateToProfile);
    }

    [Fact]
    public void Atomic_save_creates_recoverable_backup_file()
    {
        var store = new SettingsStore(_dir);
        var initial = new SettingsStore.PersistedSettings
        {
            RecoilMultiplier = 1.1,
            ThemePreference = "Light"
        };
        store.Save(initial);

        var backupPath = Path.Combine(_dir, "settings.json.bak");
        var primaryPath = Path.Combine(_dir, "settings.json");

        Assert.True(File.Exists(primaryPath));

        // Second save triggers backup creation
        var updated = new SettingsStore.PersistedSettings
        {
            RecoilMultiplier = 1.8,
            ThemePreference = "Dark"
        };
        store.Save(updated);

        Assert.True(File.Exists(backupPath), "Backup file should be created on save");
        var backupText = File.ReadAllText(backupPath);
        Assert.Contains("1.1", backupText); // Contains previous known-good state
    }

    [Fact]
    public void Corrupted_primary_settings_file_recovers_from_backup()
    {
        var store = new SettingsStore(_dir);
        var good = new SettingsStore.PersistedSettings
        {
            RecoilMultiplier = 1.35,
            ThemePreference = "Dark"
        };
        store.Save(good);

        // Second save ensures .bak exists
        store.Save(good);

        var primaryPath = Path.Combine(_dir, "settings.json");
        // Corrupt primary file
        File.WriteAllText(primaryPath, "{ corrupt json not valid at all !!!");

        var recovered = store.Load();
        Assert.Equal(1.35, recovered.RecoilMultiplier);
        Assert.Equal("Dark", recovered.ThemePreference);
    }
}
