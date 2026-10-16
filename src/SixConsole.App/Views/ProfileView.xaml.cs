using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SixConsole.App.ViewModels;

namespace SixConsole.App.Views;

public sealed partial class ProfileView : UserControl
{
    public ProfileViewModel ViewModel { get; }

    public ProfileView()
    {
        ViewModel = new ProfileViewModel(App.Instance!.Services, App.Instance.Controller!);
        InitializeComponent();
        Unloaded += (_, _) => ViewModel.Detach();
    }

    private void EnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        var isOn = ((ToggleSwitch)sender).IsOn;
        if (isOn != ViewModel.IsEnabled)
        {
            // Engine confirms through its StateChanged event, which re-applies the snapshot.
            App.Instance.Controller!.Toggle();
        }
    }

    private void Operator_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string opId })
        {
            ViewModel.SelectOperatorCommand.Execute(opId);
        }
    }

    private void Weapon_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WeaponRow row)
        {
            ViewModel.SelectWeaponCommand.Execute(row);
        }
    }

    private void Weapon_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView { SelectedItem: WeaponRow row })
        {
            ViewModel.SelectWeaponCommand.Execute(row);
        }
    }
}
