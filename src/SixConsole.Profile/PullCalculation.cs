namespace SixConsole.Profile;

/// <summary>Static per-weapon settings matching source defaults (documented in Implementation Plan §7).
/// These do not currently change; profile values come from the catalogue and PullBehavior.</summary>
public static class PullCalculation
{
    /// <summary>Replicates Lua <c>Round(num) = math.floor(num + 0.5)</c>.</summary>
    public static int Round(double num) => (int)Math.Floor(num + 0.5);
}
