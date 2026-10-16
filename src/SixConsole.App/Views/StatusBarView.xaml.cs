using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SixConsole.App.Input;
using SixConsole.Profile;

namespace SixConsole.App.Views;

/// <summary>
/// Bottom status bar (pro-utility-monochrome 22px strip): engine health, Caps Lock state,
/// current selection and last engine error. Dot colors come from ThemeResource brushes declared
/// in XAML so Light/Dark/System all resolve correctly.
/// </summary>
public sealed partial class StatusBarView : UserControl
{
    public StatusBarView()
    {
        InitializeComponent();
    }

    public void Update(EngineSnapshot snap)
    {
        HealthText.Text = snap.Status switch
        {
            EngineStatus.Running => "hook OK",
            EngineStatus.NotStarted => "starting…",
            EngineStatus.StoppedAfterError => "hook failed",
            _ => snap.Status.ToString().ToLowerInvariant(),
        };
        var (success, muted, error) =
            snap.Status switch
            {
                EngineStatus.Running when snap.Enabled => (true, false, false),
                EngineStatus.Running => (false, true, false),
                EngineStatus.StoppedAfterError => (false, false, true),
                _ => (false, false, true),
            };
        DotSuccess.Visibility = success ? Visibility.Visible : Visibility.Collapsed;
        DotMuted.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
        DotError.Visibility = error ? Visibility.Visible : Visibility.Collapsed;
        CapsText.Text = snap.CapsLockOn ? "CAPS ON" : "caps off";
        SelectionText.Text = snap.Operator is null ? "no selection" :
            $"{snap.Operator.Side.ToLabel()} {snap.Operator.Name} · op {snap.OperatorIndex + 1}/{snap.OperatorCount} · " +
            $"weapon {snap.WeaponIndex + 1}/{snap.WeaponCount} · " +
            (snap.Mode == InputMode.RapidFire ? "rapid fire" : "full-auto") + $" · pull {snap.EffectivePull} px";
        ErrorText.Text = snap.LastError ?? "";
    }
}
