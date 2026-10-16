using System.Reflection;
using System.Text.RegularExpressions;
using SixConsole.Profile;

namespace SixConsole.Tests;

/// <summary>
/// Loads the canonical Lua fixture and parses it with a regex implementation that is independent
/// from the Python generator (tools/generate-catalogue.py), then asserts the embedded catalogue
/// matches it 1:1 in list order.
/// </summary>
public sealed class SourceFixture
{
    public static readonly string LuaSource = ReadLua();

    private static string ReadLua()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SourceFixtures", "lua-source.txt");
        return File.ReadAllText(path);
    }
}

public sealed class SourceIntegrityTests
{
    private static IReadOnlyList<Operator> Operators => Catalogue.Operators;

    private static (IReadOnlyList<(string Name, string Side, IReadOnlyList<(string Name, int Pull, bool RapidFire)> Guns)> ops, int opCount, int weaponCount)
        ParseLua()
    {
        var lua = SourceFixture.LuaSource;
        var opRe = new Regex(
            @"name\s*=\s*""(?<name>[^""]+)""\s*,\s*side\s*=\s*""(?<side>ATK|DEF)""\s*,\s*guns\s*=\s*\{",
            RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        var gunRe = new Regex(
            @"\{\s*name\s*=\s*""(?<name>[^""]+)""\s*,\s*pull\s*=\s*(?<pull>\d+)\s*,\s*rapidFire\s*=\s*(?<rf>true|false)\s*\}",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var ops = new List<(string Name, string Side, IReadOnlyList<(string, int, bool)> Guns)>();
        var opMatches = opRe.Matches(lua).Cast<Match>().ToList();
        var gunMatches = gunRe.Matches(lua).Cast<Match>().ToList();
        int cursor = 0;
        for (int i = 0; i < opMatches.Count; i++)
        {
            int opPos = opMatches[i].Index;
            int endPos = i + 1 < opMatches.Count ? opMatches[i + 1].Index : lua.Length - 1;
            var guns = new List<(string, int, bool)>();
            while (cursor < gunMatches.Count && gunMatches[cursor].Index < endPos)
            {
                if (gunMatches[cursor].Index >= opPos)
                    guns.Add((
                        gunMatches[cursor].Groups["name"].Value,
                        int.Parse(gunMatches[cursor].Groups["pull"].Value),
                        gunMatches[cursor].Groups["rf"].Value == "true"));
                cursor++;
            }
            ops.Add((opMatches[i].Groups["name"].Value, opMatches[i].Groups["side"].Value, guns));
        }
        return (ops, opMatches.Count, gunMatches.Count);
    }

    private static IReadOnlyList<(string Name, string Side, IReadOnlyList<(string, int, bool)> Guns)> ParsedOps()
    {
        var (ops, _, _) = ParseLua();
        return ops;
    }

    [Fact]
    public void OperatorCount_matches_source_exactly()
    {
        var (_, opCount, _) = ParseLua();
        Assert.Equal(opCount, Operators.Count);
    }

    [Fact]
    public void WeaponCount_matches_source_exactly()
    {
        var (_, _, weaponCount) = ParseLua();
        Assert.Equal(weaponCount, Operators.Sum(o => o.Weapons.Count));
    }

    [Fact]
    public void SideTotals_match_source_distribution()
    {
        var (_, opCount, _) = ParseLua();
        var parsed = ParsedOps();
        var expectedAtk = parsed.Count(o => o.Side == "ATK");
        var expectedDef = parsed.Count(o => o.Side == "DEF");
        Assert.Equal(expectedAtk, Operators.Count(o => o.Side == OperatorSide.Attacker));
        Assert.Equal(expectedDef, Operators.Count(o => o.Side == OperatorSide.Defender));
        Assert.Equal(opCount, expectedAtk + expectedDef);
    }

    [Fact]
    public void EveryOperatorName_side_and_weaponData_match_source_in_order()
    {
        var parsed = ParsedOps();
        Assert.Equal(parsed.Count, Operators.Count);
        for (int i = 0; i < parsed.Count; i++)
        {
            var expected = parsed[i];
            var actual = Operators[i];
            int opNumber = i + 1;
            Assert.Equal($"op-{opNumber:D3}", actual.Id);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(OperatorSideExtensions.FromLabel(expected.Side), actual.Side);
            Assert.Equal(expected.Guns.Count, actual.Weapons.Count);
            for (int j = 0; j < expected.Guns.Count; j++)
            {
                var expectedGun = expected.Guns[j];
                var actualGun = actual.Weapons[j];
                Assert.Equal($"{actual.Id}-w{j + 1}", actualGun.Id);
                Assert.Equal(expectedGun.Item1, actualGun.Name);
                Assert.Equal(expectedGun.Item2, actualGun.Pull);
                Assert.Equal(expectedGun.Item3, actualGun.RapidFire);
            }
        }
    }

    [Fact]
    public void RepeatedWeaponNames_are_not_deduplicated()
    {
        var sledge = Operators.First(o => o.Name == "Sledge");
        var ying = Operators.First(o => o.Name == "Ying");
        // Same gun name appears under two different operators as separate catalogue entries.
        Assert.Equal(sledge.Weapons.Single(w => w.Name == "Reaper Mk2 (Sec)").Pull,
                     ying.Weapons.Single(w => w.Name == "Reaper Mk2 (Sec)").Pull);
        Assert.NotSame(sledge.Weapons[1], ying.Weapons[1]);
    }
}

public sealed class DefaultsAndCalculationTests
{
    [Fact]
    public void DefaultsMatch_source_values()
    {
        var s = ProfileSettings.Defaults();
        Assert.Equal(0.9, s.RecoilMultiplier);
        Assert.Equal(1, s.VertVariance);
        Assert.Equal(1, s.HorizJitter);
        Assert.Equal(2, s.Delay.Min);
        Assert.Equal(5, s.Delay.Max);
        Assert.Equal("p", s.RfKey);
        Assert.Equal(25, s.Hold.Min);
        Assert.Equal(40, s.Hold.Max);
        Assert.Equal(65, s.Gap.Min);
        Assert.Equal(90, s.Gap.Max);
        Assert.True(s.Delay.IsValid && s.Hold.IsValid && s.Gap.IsValid);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.4, 0)]
    [InlineData(0.5, 1)]
    [InlineData(0.9, 1)]
    [InlineData(1.0, 1)]
    [InlineData(1.4, 1)]
    [InlineData(1.5, 2)]
    [InlineData(38.0, 38)]
    [InlineData(38.6, 39)]
    public void Rounding_matches_floor_plus_half(double input, int expected)
    {
        Assert.Equal(expected, PullCalculation.Round(input));
    }

    [Theory]
    [InlineData(38, 0.9, 34)]       // Round(38 × 0.9) = Round(34.2) = 34
    [InlineData(360, 0.9, 324)]     // Round(360 × 0.9) = Round(324)
    [InlineData(155, 0.9, 140)]     // Round(155 × 0.9) = Round(139.5) = 140 (floor+0.5)
    public void EffectivePull_uses_round_formula(int pull, double multiplier, int expected)
    {
        var s = new ProfileSettings
        {
            RecoilMultiplier = multiplier,
            VertVariance = 0,
            HorizJitter = 0,
            Delay = new TimingRange { Min = 2, Max = 5 },
            RfKey = "p",
            Hold = new TimingRange { Min = 25, Max = 40 },
            Gap = new TimingRange { Min = 65, Max = 90 },
        };
        var weapon = new Weapon { Id = "x", Name = "X", Pull = pull, RapidFire = false };
        Assert.Equal(expected, s.EffectivePull(weapon));
    }
}

public sealed class SelectionNavigationTests
{
    private static IReadOnlyList<Operator> Operators => Catalogue.Operators;

    private static int CountOfSide(OperatorSide side) => Operators.Count(o => o.Side == side);

    [Fact]
    public void NextOnLastOperator_wraps_to_first_and_resets_weapon()
    {
        var sel = new SelectionModel(Operators, Operators.Count - 1, Operators[^1].Weapons.Count - 1);
        sel.NavigateOperator(NavigationDirection.Next);
        Assert.Equal(0, sel.OperatorIndex);
        Assert.Equal(0, sel.WeaponIndex);
    }

    [Fact]
    public void PreviousOnFirstOperator_wraps_to_last_and_resets_weapon()
    {
        var last = Operators[^1];
        var sel = new SelectionModel(Operators, 0, 0);
        sel.NavigateOperator(NavigationDirection.Previous);
        Assert.Equal(Operators.Count - 1, sel.OperatorIndex);
        Assert.Equal(last, sel.Operator);
        Assert.Equal(0, sel.WeaponIndex);
    }
    [Fact]
    public void NextOnLastWeapon_wraps_to_first_of_same_operator()
    {
        var sel = new SelectionModel(Operators, 0, 0); // Striker, M4
        sel.NavigateWeapon(NavigationDirection.Next);
        sel.NavigateWeapon(NavigationDirection.Next);
        Assert.Equal(2, sel.WeaponIndex);
        Assert.Equal("SR-25", sel.Weapon.Name);
        sel.NavigateWeapon(NavigationDirection.Next);
        Assert.Equal(0, sel.WeaponIndex);
        Assert.Equal("M4", sel.Weapon.Name);
    }

    [Fact]
    public void PreviousOnFirstWeapon_wraps_to_last_of_same_operator()
    {
        var sel = new SelectionModel(Operators, 0, 0); // Striker, M4
        sel.NavigateWeapon(NavigationDirection.Previous);
        Assert.Equal(2, sel.WeaponIndex);
        Assert.Equal("SR-25", sel.Weapon.Name);
    }

    [Fact]
    public void NavigateWeapon_does_not_change_operator()
    {
        var sel = new SelectionModel(Operators, 0, 0);
        sel.NavigateWeapon(NavigationDirection.Next);
        Assert.Equal("Striker", sel.Operator.Name);
        Assert.Equal(1, sel.OperatorIndexOneBased); // operator unchanged
    }

    [Fact]
    public void SelectOperatorById_resets_weapon_to_first()
    {
        var sel = new SelectionModel(Operators, 0, 2); // start off-first
        var target = Operators.First(o => o.Side == OperatorSide.Defender); // "Sentry"
        sel.SelectOperator(target.Id);
        Assert.Equal(target, sel.Operator);
        Assert.Equal(0, sel.WeaponIndex);
        Assert.Equal("Tcsg12", sel.Weapon.Name);
    }

    [Fact]
    public void SelectWeapon_oneBased_sets_weapon_and_keeps_operator()
    {
        var op = Operators[0];
        var sel = new SelectionModel(Operators, 0, 0);
        sel.SelectWeapon(op, op.Weapons.Count);
        Assert.Equal("SR-25", sel.Weapon.Name);
        Assert.Equal(0, sel.OperatorIndex);
    }
}

