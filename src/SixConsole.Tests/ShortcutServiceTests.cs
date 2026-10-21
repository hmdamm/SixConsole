using SixConsole.App.Services.Shortcuts;

namespace SixConsole.Tests;

public sealed class ShortcutServiceTests
{
    [Fact]
    public void Default_shortcuts_contain_expected_benign_actions()
    {
        var service = new ShortcutService();
        var bindings = service.CurrentBindings;

        Assert.NotEmpty(bindings);
        Assert.Contains(bindings, b => b.ActionId == ShortcutActionIds.NavigateToSettings);
        Assert.Contains(bindings, b => b.ActionId == ShortcutActionIds.NavigateToProfile);
        Assert.Contains(bindings, b => b.ActionId == ShortcutActionIds.NavigateToHelp);
        Assert.Contains(bindings, b => b.ActionId == ShortcutActionIds.ToggleProfileActive);
        Assert.Contains(bindings, b => b.ActionId == ShortcutActionIds.NextOperator);
        Assert.Contains(bindings, b => b.ActionId == ShortcutActionIds.PreviousOperator);
        Assert.Contains(bindings, b => b.ActionId == ShortcutActionIds.NextWeapon);
        Assert.Contains(bindings, b => b.ActionId == ShortcutActionIds.PreviousWeapon);
    }

    [Fact]
    public void Dispatch_executes_registered_handler_when_key_and_modifiers_match()
    {
        using var service = new ShortcutService();
        bool executed = false;
        service.RegisterAction(ShortcutActionIds.NavigateToSettings, () => executed = true);

        // Find the binding for NavigateToSettings (default Ctrl+Comma, VK 0xBC)
        var settingsBinding = service.CurrentBindings.First(b => b.ActionId == ShortcutActionIds.NavigateToSettings);

        bool handled = service.Dispatch(settingsBinding.VirtualKey, settingsBinding.Modifiers, settingsBinding.Scope);

        Assert.True(handled);
        Assert.True(executed);
    }

    [Fact]
    public void Dispatch_ignores_when_modifiers_do_not_match()
    {
        using var service = new ShortcutService();
        bool executed = false;
        service.RegisterAction(ShortcutActionIds.NavigateToSettings, () => executed = true);

        var settingsBinding = service.CurrentBindings.First(b => b.ActionId == ShortcutActionIds.NavigateToSettings);

        // Send wrong modifier (Alt instead of Ctrl)
        bool handled = service.Dispatch(settingsBinding.VirtualKey, KeyModifiers.Alt, settingsBinding.Scope);

        Assert.False(handled);
        Assert.False(executed);
    }

    [Fact]
    public void Dispatch_does_not_fire_when_recording()
    {
        using var service = new ShortcutService();
        bool executed = false;
        service.RegisterAction(ShortcutActionIds.NavigateToSettings, () => executed = true);
        var settingsBinding = service.CurrentBindings.First(b => b.ActionId == ShortcutActionIds.NavigateToSettings);

        service.IsRecording = true;
        bool handled = service.Dispatch(settingsBinding.VirtualKey, settingsBinding.Modifiers, settingsBinding.Scope);

        Assert.False(handled);
        Assert.False(executed);
    }

    [Fact]
    public void Conflicting_bindings_are_rejected_with_clear_error()
    {
        using var service = new ShortcutService();
        var bindings = service.CurrentBindings.ToList();

        // Assign same combination to NavigateToHelp as NavigateToSettings
        var settingsBinding = bindings.First(b => b.ActionId == ShortcutActionIds.NavigateToSettings);
        var helpBinding = bindings.First(b => b.ActionId == ShortcutActionIds.NavigateToHelp);
        helpBinding.VirtualKey = settingsBinding.VirtualKey;
        helpBinding.Modifiers = settingsBinding.Modifiers;
        helpBinding.Scope = settingsBinding.Scope;

        bool updated = service.UpdateBindings(bindings, out var error);

        Assert.False(updated);
        Assert.Contains("Conflicting shortcut", error);
    }

    [Fact]
    public void Reserved_operating_system_shortcuts_are_rejected()
    {
        // Reserved Escape
        Assert.True(ShortcutValidator.IsReservedKeyCombination(0x1B, KeyModifiers.None, out var errEsc));
        Assert.Contains("Escape is reserved", errEsc);

        // Reserved Alt+Tab (0x09)
        Assert.True(ShortcutValidator.IsReservedKeyCombination(0x09, KeyModifiers.Alt, out var errTab));
        Assert.Contains("Alt+Tab", errTab);

        // Reserved Alt+F4 (0x73)
        Assert.True(ShortcutValidator.IsReservedKeyCombination(0x73, KeyModifiers.Alt, out var errF4));
        Assert.Contains("Alt+F4", errF4);

        // Reserved Ctrl+Alt+Del (0x2E)
        Assert.True(ShortcutValidator.IsReservedKeyCombination(0x2E, KeyModifiers.Control | KeyModifiers.Alt, out var errDel));
        Assert.Contains("Ctrl+Alt+Del", errDel);

        // Standalone modifier (Ctrl 0x11)
        Assert.True(ShortcutValidator.IsReservedKeyCombination(0x11, KeyModifiers.Control, out var errMod));
        Assert.Contains("modifier key alone", errMod);
    }

    [Fact]
    public void ResetToDefaults_restores_original_bindings()
    {
        using var service = new ShortcutService();
        var modified = service.CurrentBindings.ToList();
        var profile = modified.First(b => b.ActionId == ShortcutActionIds.NavigateToProfile);
        profile.VirtualKey = 0x50; // 'P'
        profile.Modifiers = KeyModifiers.Alt;
        service.UpdateBindings(modified, out _);

        Assert.Equal("Alt+P", service.CurrentBindings.First(b => b.ActionId == ShortcutActionIds.NavigateToProfile).DisplayString);

        service.ResetToDefaults();

        Assert.Equal("Ctrl+1", service.CurrentBindings.First(b => b.ActionId == ShortcutActionIds.NavigateToProfile).DisplayString);
    }

    [Fact]
    public void UnregisterAction_stops_dispatch()
    {
        using var service = new ShortcutService();
        int callCount = 0;
        service.RegisterAction(ShortcutActionIds.NavigateToHelp, () => callCount++);

        var help = service.CurrentBindings.First(b => b.ActionId == ShortcutActionIds.NavigateToHelp);
        service.Dispatch(help.VirtualKey, help.Modifiers, help.Scope);
        Assert.Equal(1, callCount);

        service.UnregisterAction(ShortcutActionIds.NavigateToHelp);
        service.Dispatch(help.VirtualKey, help.Modifiers, help.Scope);
        Assert.Equal(1, callCount);
    }
}
