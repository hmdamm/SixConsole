namespace SixConsole.Profile;

public enum OperatorSide
{
    Attacker,
    Defender,
}

public static class OperatorSideExtensions
{
    public static string ToLabel(this OperatorSide side) => side == OperatorSide.Attacker ? "ATK" : "DEF";

    public static OperatorSide FromLabel(string label) => label == "ATK" ? OperatorSide.Attacker : OperatorSide.Defender;
}

public sealed class Operator
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required OperatorSide Side { get; init; }
    public required IReadOnlyList<Weapon> Weapons { get; init; }
}

public sealed class Weapon
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Pull { get; init; }
    public required bool RapidFire { get; init; }
}

public enum InputMode
{
    FullAutoRecoil,
    RapidFire,
}
