using SixConsole.App.Services;
using SixConsole.App.ViewModels;
using SixConsole.Profile;

namespace SixConsole.Tests;

/// <summary>
/// Verifies that option (operator and weapon) selection responds immediately and updates state in-place.
/// </summary>
public sealed class OptionSelectionTests : IDisposable
{
    private readonly string _dir;

    public OptionSelectionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "sixconsole-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void OperatorRow_notifies_property_changed_when_selection_changes()
    {
        var row = new OperatorRow("op-1", "TestOp", "ATK", 3, false);
        string? changedProp = null;
        row.PropertyChanged += (_, e) => changedProp = e.PropertyName;

        row.IsSelected = true;

        Assert.True(row.IsSelected);
        Assert.Equal(nameof(OperatorRow.IsSelected), changedProp);
    }

    [Fact]
    public void SelectOperator_updates_selection_in_place_without_clearing_collections()
    {
        var store = new SettingsStore(_dir);
        var services = new AppServices(store);
        using var controller = new ProfileController(services);
        var vm = new ProfileViewModel(services, controller);

        // Verify initial state
        Assert.NotEmpty(vm.Attackers);
        Assert.NotEmpty(vm.Defenders);
        var initialFirstRow = vm.Attackers[0];
        Assert.True(initialFirstRow.IsSelected);

        // Capture references of existing rows
        var initialAttackers = vm.Attackers.ToList();
        var targetRow = vm.Attackers[1];

        // Select the second attacker
        vm.SelectOperatorCommand.Execute(targetRow.OperatorId);

        // Verify the selection was applied to SelectionModel and Snapshot
        Assert.Equal(targetRow.OperatorId, services.Selection.Operator.Id);
        Assert.Equal(targetRow.OperatorId, vm.SelectedOperatorId);

        // In-place update: the collections were NOT cleared/reallocated
        Assert.Equal(initialAttackers.Count, vm.Attackers.Count);
        for (int i = 0; i < initialAttackers.Count; i++)
        {
            Assert.Same(initialAttackers[i], vm.Attackers[i]); // exact same object references
        }

        // Selection states updated correctly
        Assert.False(initialFirstRow.IsSelected);
        Assert.True(targetRow.IsSelected);
    }

    [Fact]
    public void SelectWeapon_updates_active_weapon_immediately()
    {
        var store = new SettingsStore(_dir);
        var services = new AppServices(store);
        using var controller = new ProfileController(services);
        var vm = new ProfileViewModel(services, controller);

        Assert.True(vm.Weapons.Count > 1, "Expected operator to have multiple weapons");
        Assert.Equal(0, services.Selection.WeaponIndex);

        var secondWeapon = vm.Weapons[1];
        vm.SelectWeaponCommand.Execute(secondWeapon);

        Assert.Equal(1, services.Selection.WeaponIndex);
        Assert.Equal(1, vm.SelectedWeaponIndex);
    }
}
