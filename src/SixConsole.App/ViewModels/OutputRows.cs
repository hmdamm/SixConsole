using CommunityToolkit.Mvvm.ComponentModel;
using SixConsole.App.Input;
using SixConsole.Profile;

namespace SixConsole.App.ViewModels;

/// <summary>Display row for a weapon of the selected operator.</summary>
public sealed record WeaponRow(int OneBased, Weapon Weapon, int EffectivePull, string Mode, string Timing)
{
    public string PullText { get; } = $"Pull {EffectivePull} px";

    /// <summary>Accessible name (ListView items surface ToString to UIA/screen readers).</summary>
    public override string ToString() => $"{OneBased}. {Weapon.Name} — {Mode}, {PullText}, {Timing}";
}

/// <summary>Display row for an operator tile in the roster.</summary>
public sealed partial class OperatorRow : ObservableObject
{
    public string OperatorId { get; }
    public string Name { get; }
    public string Side { get; }
    public int WeaponCount { get; }

    [ObservableProperty]
    private bool isSelected;

    public OperatorRow(string operatorId, string name, string side, int weaponCount, bool isSelected)
    {
        OperatorId = operatorId;
        Name = name;
        Side = side;
        WeaponCount = weaponCount;
        this.isSelected = isSelected;
    }

    public string WeaponCountText => $"{WeaponCount}";
    public string ToolTip_ => $"{Side} · {Name} · {WeaponCount} weapon(s)";

    /// <summary>Accessible name for the roster tile.</summary>
    public override string ToString() => $"{Name} ({Side}, {WeaponCount} weapons)";
}
