using SixConsole.Profile;

namespace SixConsole.App.Services;

/// <summary>
/// Composition root: holds the profile selection, the live settings, and the input engine
/// handle for the UI layer. UI talks only to this; the engine talks back via events.
/// </summary>
public sealed class AppServices
{
    public SettingsStore SettingsStore { get; }
    public SelectionModel Selection { get; }
    public ProfileSettings Settings { get; private set; }
    public SettingsStore.PersistedSettings Persisted { get; private set; }

    /// <summary>Raised after <see cref="ApplySettings"/> so open views can refresh without
    /// waiting for engine events (engine events don't fire on Save).</summary>
    public event Action? SettingsApplied;

    private readonly object _persistGate = new();
    private int _selectionSaveInFlight; // 0 = no background save pending; 1 = pending

    public AppServices(SettingsStore settingsStore)
    {
        SettingsStore = settingsStore;
        Persisted = SettingsStore.Load();
        var ops = Operators;
        int opIndex = Clamp(Persisted.LastOperatorIndex, ops.Count);
        int weaponIndex = Clamp(Persisted.LastWeaponIndex, ops[opIndex].Weapons.Count);
        Selection = new SelectionModel(ops, opIndex, weaponIndex);
        Settings = BuildSettingsOrDefaults();
        Selection.Changed += OnSelectionChanged;
    }

    private static IReadOnlyList<Operator> Operators => Catalogue.Operators;

    private ProfileSettings BuildSettingsOrDefaults()
    {
        try
        {
            return ProfileSettings.Create(
                Persisted.RecoilMultiplier, Persisted.VertVariance, Persisted.HorizJitter,
                Persisted.DelayMin, Persisted.DelayMax, Persisted.RfKey,
                Persisted.HoldMin, Persisted.HoldMax, Persisted.GapMin, Persisted.GapMax);
        }
        catch (SettingsValidationException)
        {
            // malformed saved settings: fall back to source defaults (documented; user-visible via Settings validation)
            return ProfileSettings.Defaults();
        }
    }

    private static int Clamp(int idx, int count) => Math.Clamp(idx, 0, count - 1);

    private SettingsStore.PersistedSettings SnapshotPersisted()
    {
        lock (_persistGate)
        {
            return Clone(Persisted);
        }
    }

    /// <summary>Defensive copy — writers (view-models, engine thread) never share the live instance.</summary>
    private static SettingsStore.PersistedSettings Clone(SettingsStore.PersistedSettings s) => new()
    {
        RecoilMultiplier = s.RecoilMultiplier,
        VertVariance = s.VertVariance,
        HorizJitter = s.HorizJitter,
        DelayMin = s.DelayMin,
        DelayMax = s.DelayMax,
        RfKey = s.RfKey,
        HoldMin = s.HoldMin,
        HoldMax = s.HoldMax,
        GapMin = s.GapMin,
        GapMax = s.GapMax,
        ThemePreference = s.ThemePreference,
        LastOperatorIndex = s.LastOperatorIndex,
        LastWeaponIndex = s.LastWeaponIndex,
        RestoreLastSelection = s.RestoreLastSelection,
    };

    private void OnSelectionChanged()
    {
        lock (_persistGate)
        {
            Persisted.LastOperatorIndex = Selection.OperatorIndex;
            Persisted.LastWeaponIndex = Selection.WeaponIndex;
        }

        // This handler can run inside the low-level mouse hook callback (engine-driven
        // navigation); synchronous file I/O there risks hook timeouts, so it is serialized
        // to a worker and coalesced into one write per burst.
        if (Interlocked.CompareExchange(ref _selectionSaveInFlight, 1, 0) != 0) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(120).ConfigureAwait(false); // coalesce rapid navigation
                SettingsStore.Save(SnapshotPersisted());
            }
            finally
            {
                Volatile.Write(ref _selectionSaveInFlight, 0);
            }
        });
    }

    public ProfileSettings PreviewSettings(double recoilMultiplier, int vertVariance, int horizJitter, int delayMin, int delayMax, string rfKey, int holdMin, int holdMax, int gapMin, int gapMax)
        => ProfileSettings.Create(recoilMultiplier, vertVariance, horizJitter, delayMin, delayMax, rfKey, holdMin, holdMax, gapMin, gapMax);

    public void ApplySettings(ProfileSettings newSettings)
    {
        Settings = newSettings;
        lock (_persistGate)
        {
            Persisted.RecoilMultiplier = newSettings.RecoilMultiplier;
            Persisted.VertVariance = newSettings.VertVariance;
            Persisted.HorizJitter = newSettings.HorizJitter;
            Persisted.DelayMin = newSettings.Delay.Min;
            Persisted.DelayMax = newSettings.Delay.Max;
            Persisted.RfKey = newSettings.RfKey;
            Persisted.HoldMin = newSettings.Hold.Min;
            Persisted.HoldMax = newSettings.Hold.Max;
            Persisted.GapMin = newSettings.Gap.Min;
            Persisted.GapMax = newSettings.Gap.Max;
        }
        SettingsStore.Save(SnapshotPersisted());
        SettingsApplied?.Invoke();
    }
}
