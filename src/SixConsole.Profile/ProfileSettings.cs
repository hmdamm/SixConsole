namespace SixConsole.Profile;

public sealed class TimingRange
{
    public required int Min { get; init; }
    public required int Max { get; init; }

    public bool IsValid => Min <= Max && Min >= 0;
}

/// <summary>
/// Fully validated per-run profile settings. Values map to the Lua globals in
/// Implementation_Plan §3.1 / docs/SOURCE_CHECKLIST.md. Immutable snapshots; invalid edits never
/// reach the engine because construction throws.
/// </summary>
public sealed class ProfileSettings
{
    public const double DefaultRecoilMultiplier = 0.9;
    public const int DefaultVertVariance = 1;
    public const int DefaultHorizJitter = 1;
    public const int DefaultMinDelay = 2;
    public const int DefaultMaxDelay = 5;
    public const string DefaultRfKey = "p";
    public const int DefaultMinHold = 25;
    public const int DefaultMaxHold = 40;
    public const int DefaultMinGap = 65;
    public const int DefaultMaxGap = 90;

    /// <summary>Globally constant (source-per-model): minimum/maximum allowed bounds.</summary>
    public const double RecoilMultiplierFloor = 0.1;
    public const double RecoilMultiplierCeiling = 5.0;
    public const int VarianceCeiling = 100;
    public const int DelayCeiling = 5000;   // ms; generous ceiling, validated at input time
    public const int HoldCeiling = 5000;    // ms
    public const int GapCeiling = 5000;     // ms

    public required double RecoilMultiplier { get; init; }
    public required int VertVariance { get; init; }
    public required int HorizJitter { get; init; }
    public required TimingRange Delay { get; init; }        // full-auto mode
    public required string RfKey { get; init; }
    public required TimingRange Hold { get; init; }          // rapid-fire mode
    public required TimingRange Gap { get; init; }           // rapid-fire mode

    public static ProfileSettings Defaults() => new()
    {
        RecoilMultiplier = DefaultRecoilMultiplier,
        VertVariance = DefaultVertVariance,
        HorizJitter = DefaultHorizJitter,
        Delay = new TimingRange { Min = DefaultMinDelay, Max = DefaultMaxDelay },
        RfKey = DefaultRfKey,
        Hold = new TimingRange { Min = DefaultMinHold, Max = DefaultMaxHold },
        Gap = new TimingRange { Min = DefaultMinGap, Max = DefaultMaxGap },
    };

    /// <summary>Precise check used both by the Settings UI (dry-run diagnostics) and by loading saved settings.</summary>
    public static IReadOnlyList<string> Validate(double recoilMultiplier, int vertVariance, int horizJitter,
        int delayMin, int delayMax, string rfKey, int holdMin, int holdMax, int gapMin, int gapMax)
    {
        var errors = new List<string>();
        if (!double.IsFinite(recoilMultiplier) || recoilMultiplier < RecoilMultiplierFloor || recoilMultiplier > RecoilMultiplierCeiling)
            errors.Add($"Recoil sensitivity must be a number between {RecoilMultiplierFloor} and {RecoilMultiplierCeiling}.");
        if (vertVariance < 0 || vertVariance > VarianceCeiling) errors.Add($"Vertical variance must be between 0 and {VarianceCeiling}.");
        if (horizJitter < 0 || horizJitter > VarianceCeiling) errors.Add($"Horizontal jitter must be between 0 and {VarianceCeiling}.");
        if (delayMin < 0 || delayMin > DelayCeiling || delayMax < 0 || delayMax > DelayCeiling)
            errors.Add($"Delay values must be between 0 and {DelayCeiling} ms.");
        if (delayMin > delayMax) errors.Add("Delay minimum must not be greater than delay maximum.");
        if (string.IsNullOrWhiteSpace(rfKey) || rfKey.Length != 1)
            errors.Add("Rapid-fire key must be exactly one character (a letter, number, or punctuation key).");
        if (holdMin < 0 || holdMin > HoldCeiling || holdMax < 0 || holdMax > HoldCeiling)
            errors.Add($"Hold values must be between 0 and {HoldCeiling} ms.");
        if (holdMin > holdMax) errors.Add("Hold minimum must not be greater than hold maximum.");
        if (gapMin < 0 || gapMin > GapCeiling || gapMax < 0 || gapMax > GapCeiling)
            errors.Add($"Gap values must be between 0 and {GapCeiling} ms.");
        if (gapMin > gapMax) errors.Add("Gap minimum must not be greater than gap maximum.");
        return errors;
    }

    public static ProfileSettings Create(double recoilMultiplier, int vertVariance, int horizJitter,
        int delayMin, int delayMax, string rfKey, int holdMin, int holdMax, int gapMin, int gapMax)
    {
        var errors = Validate(recoilMultiplier, vertVariance, horizJitter, delayMin, delayMax, rfKey, holdMin, holdMax, gapMin, gapMax);
        if (errors.Count > 0) throw new SettingsValidationException(errors);
        return new ProfileSettings
        {
            // NumberBox round-trips the multiplier through float32 (1.1 → 1.100000023841858);
            // normalize that presentation noise so persisted JSON matches user intent. The
            // rounding (4 decimals) is far below the smallest UI step (0.05) and preserves
            // Round(pull × multiplier) outcomes for every catalogue pull value.
            RecoilMultiplier = Math.Round(recoilMultiplier, 4),
            VertVariance = vertVariance,
            HorizJitter = horizJitter,
            Delay = new TimingRange { Min = delayMin, Max = delayMax },
            RfKey = rfKey,
            Hold = new TimingRange { Min = holdMin, Max = holdMax },
            Gap = new TimingRange { Min = gapMin, Max = gapMax },
        };
    }

    /// <summary>Effective pull for a weapon, replicating source's Round(pull × multiplier).</summary>
    public int EffectivePull(Weapon weapon) => PullCalculation.Round(weapon.Pull * RecoilMultiplier);

    public InputMode ModeFor(Weapon weapon) => weapon.RapidFire ? InputMode.RapidFire : InputMode.FullAutoRecoil;

    public TimingRange RangeFor(InputMode mode) => mode == InputMode.RapidFire ? Hold : Delay;

    public TimingRange GapRange => Gap;
}

public sealed class SettingsValidationException : Exception
{
    public SettingsValidationException(IReadOnlyList<string> errors)
        : base("Settings are invalid: " + string.Join(" ", errors)) => Errors = errors;

    public IReadOnlyList<string> Errors { get; }
}
