namespace SixConsole.App.Services.Shortcuts;

[Flags]
public enum KeyModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Windows = 8,
}

public enum ShortcutScope
{
    AppScoped = 0,
    Global = 1,
}

public static class ShortcutActionIds
{
    public const string NavigateToProfile = "NavigateToProfile";
    public const string NavigateToSettings = "NavigateToSettings";
    public const string NavigateToHelp = "NavigateToHelp";
    public const string ToggleProfileActive = "ToggleProfileActive";
    public const string NextOperator = "NextOperator";
    public const string PreviousOperator = "PreviousOperator";
    public const string NextWeapon = "NextWeapon";
    public const string PreviousWeapon = "PreviousWeapon";
}

public sealed class ShortcutBinding
{
    public string ActionId { get; set; } = "";
    public string ActionName { get; set; } = "";
    public string Key { get; set; } = "";
    public int VirtualKey { get; set; }
    public KeyModifiers Modifiers { get; set; } = KeyModifiers.None;
    public ShortcutScope Scope { get; set; } = ShortcutScope.AppScoped;
    public bool IsEnabled { get; set; } = true;

    public ShortcutBinding() { }

    public ShortcutBinding(string actionId, string actionName, int virtualKey, string keyName, KeyModifiers modifiers, ShortcutScope scope = ShortcutScope.AppScoped, bool isEnabled = true)
    {
        ActionId = actionId;
        ActionName = actionName;
        VirtualKey = virtualKey;
        Key = keyName;
        Modifiers = modifiers;
        Scope = scope;
        IsEnabled = isEnabled;
    }

    public string DisplayString => ToDisplayString();

    public string ToDisplayString()
    {
        if (!IsEnabled || VirtualKey == 0) return "None";

        var parts = new List<string>();
        if ((Modifiers & KeyModifiers.Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & KeyModifiers.Alt) != 0) parts.Add("Alt");
        if ((Modifiers & KeyModifiers.Shift) != 0) parts.Add("Shift");
        if ((Modifiers & KeyModifiers.Windows) != 0) parts.Add("Win");

        parts.Add(FormatKeyName(VirtualKey, Key));
        return string.Join("+", parts);
    }

    public ShortcutBinding Clone() => new()
    {
        ActionId = ActionId,
        ActionName = ActionName,
        Key = Key,
        VirtualKey = VirtualKey,
        Modifiers = Modifiers,
        Scope = Scope,
        IsEnabled = IsEnabled,
    };

    public static string FormatKeyName(int vk, string fallbackName)
    {
        return vk switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            0x20 => "Space",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2E => "Delete",
            0xBC => "Comma",
            0xBE => "Period",
            0xBF => "Slash",
            0xBA => "Semicolon",
            0xDE => "Quote",
            0xDB => "BracketLeft",
            0xDD => "BracketRight",
            0xBD => "Minus",
            0xBB => "Equals",
            >= 0x70 and <= 0x87 => $"F{vk - 0x70 + 1}",
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),
            _ => !string.IsNullOrEmpty(fallbackName) ? fallbackName : $"Key(0x{vk:X2})",
        };
    }
}

public static class ShortcutDefaults
{
    public static IReadOnlyList<ShortcutBinding> CreateDefaults() => new List<ShortcutBinding>
    {
        new(ShortcutActionIds.NavigateToProfile, "Open Profile", 0x31, "1", KeyModifiers.Control),
        new(ShortcutActionIds.NavigateToSettings, "Open Settings", 0xBC, "Comma", KeyModifiers.Control),
        new(ShortcutActionIds.NavigateToHelp, "Open Help", 0x70, "F1", KeyModifiers.None),
        new(ShortcutActionIds.ToggleProfileActive, "Toggle Profile On/Off", 0x54, "T", KeyModifiers.Control | KeyModifiers.Shift),
        new(ShortcutActionIds.NextOperator, "Next Operator", 0x27, "Right", KeyModifiers.Control),
        new(ShortcutActionIds.PreviousOperator, "Previous Operator", 0x25, "Left", KeyModifiers.Control),
        new(ShortcutActionIds.NextWeapon, "Next Weapon", 0x28, "Down", KeyModifiers.Control),
        new(ShortcutActionIds.PreviousWeapon, "Previous Weapon", 0x26, "Up", KeyModifiers.Control),
    };
}
