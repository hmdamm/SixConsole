using SixConsole.App.Services;
using SixConsole.Profile;

namespace SixConsole.Tests;

/// <summary>
/// Persistence behavior: JSON round-trip, corrupted-file fallback to source defaults, and the
/// AppServices selection clamp. Every test uses a redirected data directory so real user
/// settings under %LOCALAPPDATA%\SixConsole are never touched.
/// </summary>
public sealed class SettingsPersistenceTests : IDisposable
{
    private readonly string _dir;

    public SettingsPersistenceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "sixconsole-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* transient test dir */ }
    }

    [Fact]
    public void Save_then_load_round_trips_every_field()
    {
        var store = new SettingsStore(_dir);
        var original = new SettingsStore.PersistedSettings
        {
            RecoilMultiplier = 1.25,
            VertVariance = 2,
            HorizJitter = 3,
            DelayMin = 3,
            DelayMax = 7,
            RfKey = "k",
            HoldMin = 12,
            HoldMax = 34,
            GapMin = 56,
            GapMax = 78,
            ThemePreference = "Dark",
            LastOperatorIndex = 40,
            LastWeaponIndex = 1,
            RestoreLastSelection = false,
        };
        store.Save(original);

        var loaded = new SettingsStore(_dir).Load();

        Assert.Equal(original.RecoilMultiplier, loaded.RecoilMultiplier);
        Assert.Equal(original.VertVariance, loaded.VertVariance);
        Assert.Equal(original.HorizJitter, loaded.HorizJitter);
        Assert.Equal(original.DelayMin, loaded.DelayMin);
        Assert.Equal(original.DelayMax, loaded.DelayMax);
        Assert.Equal(original.RfKey, loaded.RfKey);
        Assert.Equal(original.HoldMin, loaded.HoldMin);
        Assert.Equal(original.HoldMax, loaded.HoldMax);
        Assert.Equal(original.GapMin, loaded.GapMin);
        Assert.Equal(original.GapMax, loaded.GapMax);
        Assert.Equal(original.ThemePreference, loaded.ThemePreference);
        Assert.Equal(original.LastOperatorIndex, loaded.LastOperatorIndex);
        Assert.Equal(original.LastWeaponIndex, loaded.LastWeaponIndex);
        Assert.Equal(original.RestoreLastSelection, loaded.RestoreLastSelection);
    }

    [Fact]
    public void Missing_file_loads_source_defaults()
    {
        var store = new SettingsStore(_dir);  // no file written
        var loaded = store.Load();

        Assert.Equal(ProfileSettings.DefaultRecoilMultiplier, loaded.RecoilMultiplier);
        Assert.Equal(ProfileSettings.DefaultRfKey, loaded.RfKey);
        Assert.Equal(0, loaded.LastOperatorIndex);
    }

    [Fact]
    public void Corrupted_file_loads_source_defaults_instead_of_crashing()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ not valid json at all");

        var loaded = new SettingsStore(_dir).Load();

        Assert.Equal(ProfileSettings.DefaultRecoilMultiplier, loaded.RecoilMultiplier);
        Assert.Equal("System", loaded.ThemePreference);
    }

    [Fact]
    public void AppServices_declamps_out_of_range_saved_selection()
    {
        var store = new SettingsStore(_dir);
        store.Save(new SettingsStore.PersistedSettings { LastOperatorIndex = 9999, LastWeaponIndex = 8888 });

        var services = new AppServices(store);

        var ops = Catalogue.Operators;
        Assert.Equal(ops.Count - 1, services.Selection.OperatorIndex);
        Assert.Equal(ops[^1].Weapons[0].Id, services.Selection.Weapon.Id); // weapon reset to 0 on clamp
    }

    [Fact]
    public void ApplySettings_persists_and_uses_new_values_immediately()
    {
        var services = new AppServices(new SettingsStore(_dir));
        ProfileSettings fromFile = default!;

        services.SettingsApplied += () => fromFile = services.Settings;
        services.ApplySettings(ProfileSettings.Create(1.5, 4, 5, 6, 9, "q", 30, 50, 70, 95));

        Assert.Equal(1.5, services.Settings.RecoilMultiplier);
        var persisted = new SettingsStore(_dir).Load();
        Assert.Equal(1.5, persisted.RecoilMultiplier);
        Assert.Equal("q", persisted.RfKey);
        Assert.Equal(6, persisted.DelayMin);
        Assert.Equal(9, persisted.DelayMax);
    }

    [Fact]
    public void ApplySettings_attempts_not_persisted_when_invalid()
    {
        // Atomicity: an invalid draft never touches live settings or the saved file.
        var services = AppServices_withSeeded(new SettingsStore(_dir));
        double before = services.Settings.RecoilMultiplier;

        Assert.Throws<SettingsValidationException>(() =>
            services.ApplySettings(ProfileSettings.Create(9.9, 0, 0, 2, 5, "p", 25, 40, 65, 90)));

        Assert.Equal(before, services.Settings.RecoilMultiplier);
        var persisted = new SettingsStore(_dir).Load();
        Assert.Equal(before, persisted.RecoilMultiplier);
    }

    private static AppServices AppServices_withSeeded(SettingsStore store) => new(store);
}
