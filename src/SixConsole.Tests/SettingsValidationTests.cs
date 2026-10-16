using SixConsole.Profile;

namespace SixConsole.Tests;

public sealed class SettingsValidationTests
{
    [Fact]
    public void ValidDrafts_construct_without_error()
    {
        var s = ProfileSettings.Create(0.9, 1, 1, 2, 5, "p", 25, 40, 65, 90);
        Assert.Equal(0.9, s.RecoilMultiplier);
        Assert.Equal(2, s.Delay.Min);
        Assert.Equal(5, s.Delay.Max);
        Assert.Equal("p", s.RfKey);
    }

    [Fact]
    public void MinGreaterThanMax_rejected_with_message()
    {
        var ex = Assert.Throws<SettingsValidationException>(() =>
            ProfileSettings.Create(0.9, 1, 1, 5, 2, "p", 25, 40, 65, 90));
        Assert.Contains("Delay minimum must not be greater than delay maximum.", ex.Errors);
    }

    [Fact]
    public void MinGreaterThanMax_hold_rejected()
    {
        var ex = Assert.Throws<SettingsValidationException>(() =>
            ProfileSettings.Create(0.9, 1, 1, 2, 5, "p", 40, 25, 65, 90));
        Assert.Contains("Hold minimum must not be greater than hold maximum.", ex.Errors);
    }

    [Fact]
    public void NegativeValues_rejected()
    {
        var ex = Assert.Throws<SettingsValidationException>(() =>
            ProfileSettings.Create(0.9, -1, 1, 2, 5, "p", 25, 40, 65, 90));
        Assert.NotEmpty(ex.Errors);
    }

    [Fact]
    public void ExcessiveSensitivity_rejected()
    {
        var ex = Assert.Throws<SettingsValidationException>(() =>
            ProfileSettings.Create(6.0, 1, 1, 2, 5, "p", 25, 40, 65, 90));
        Assert.Contains("Recoil sensitivity", ex.Errors[0]);
    }

    [Fact]
    public void BadRfKey_rejected()
    {
        var ex1 = Assert.Throws<SettingsValidationException>(() =>
            ProfileSettings.Create(0.9, 1, 1, 2, 5, "", 25, 40, 65, 90));
        Assert.Contains("Rapid-fire key", ex1.Errors[0]);
        var ex2 = Assert.Throws<SettingsValidationException>(() =>
            ProfileSettings.Create(0.9, 1, 1, 2, 5, "ab", 25, 40, 65, 90));
        Assert.Contains("Rapid-fire key", ex2.Errors[0]);
    }

    [Fact]
    public void Validate_reports_all_problems_at_once()
    {
        var errors = ProfileSettings.Validate(9.9, -5, -5, -1, -1, "ab", -1, -1, -1, -1);
        // one error per group (7 groups: sensitivity, variance, jitter, delay, key, hold, gap)
        Assert.Equal(7, errors.Count);
    }

    [Fact]
    public void InvalidSettings_never_reach_a_live_controller()
    {
        // Simulates the atomicity rule: build a draft first; the controller would only be
        // handed a valid instance, so a rejected edit cannot corrupt the running engine.
        var drafts = new List<Func<ProfileSettings?>>
        {
            () => TryCreate(0.9, 0, 0, 5, 2, "p", 25, 40, 65, 90),
            () => TryCreate(0.9, 0, 0, 2, 5001, "p", 25, 40, 65, 90),
        };
        foreach (var draft in drafts)
        {
            Assert.Null(draft());
        }
    }

    private static ProfileSettings? TryCreate(double rm, int vv, int hj, int dmin, int dmax, string key, int hmin, int hmax, int gmin, int gmax)
    {
        try { return ProfileSettings.Create(rm, vv, hj, dmin, dmax, key, hmin, hmax, gmin, gmax); }
        catch (SettingsValidationException) { return null; }
    }
}

public sealed class CatalogueQuirkTests
{
    // Preserved verbatim quirks — these assertions document that they are intentional.
    [Fact]
    public void Solis_smg11_pull_is_48_as_in_source()
    {
        var solis = Catalogue.Operators.First(o => o.Name == "Solis");
        var smg11 = solis.Weapons.Single(w => w.Name == "SMG-11 (Sec)");
        Assert.Equal(48, smg11.Pull);
    }

    [Fact]
    public void Tachanka_bearing9_pull_is_34_while_others_use_49()
    {
        var tachanka = Catalogue.Operators.First(o => o.Name == "Tachanka");
        Assert.Equal(34, tachanka.Weapons.Single(w => w.Name == "Bearing 9 (Sec)").Pull);
        var echo = Catalogue.Operators.First(o => o.Name == "Echo");
        Assert.Equal(49, echo.Weapons.Single(w => w.Name == "Bearing 9 (Sec)").Pull);
    }

    [Fact]
    public void Repeated_weapon_names_kept_as_separate_entries()
    {
        // SR-25 on Striker, Blackbeard and Flores are distinct catalogue entries.
        var shooters = Catalogue.Operators
            .Where(o => o.Weapons.Any(w => w.Name == "SR-25"))
            .SelectMany(o => o.Weapons.Where(w => w.Name == "SR-25"))
            .ToList();
        Assert.Equal(3, shooters.Count);
        Assert.All(shooters, w => Assert.Equal(155, w.Pull));
        Assert.Equal(3, shooters.Select(w => w.Id).Distinct().Count());
    }
}
