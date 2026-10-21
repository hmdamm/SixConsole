using CommunityToolkit.Mvvm.ComponentModel;
using SixConsole.App.Services.Shortcuts;

namespace SixConsole.App.ViewModels;

public sealed partial class ShortcutItemViewModel : ObservableObject
{
    public string ActionId { get; }
    public string ActionName { get; }
    public int VirtualKey { get; set; }
    public KeyModifiers Modifiers { get; set; }
    public ShortcutScope Scope { get; set; }
    public string KeyName { get; set; } = "";

    [ObservableProperty]
    private string displayKey = "";

    [ObservableProperty]
    private bool isRecording;

    [ObservableProperty]
    private bool isEnabled;

    public ShortcutItemViewModel(ShortcutBinding binding)
    {
        ActionId = binding.ActionId;
        ActionName = binding.ActionName;
        VirtualKey = binding.VirtualKey;
        Modifiers = binding.Modifiers;
        Scope = binding.Scope;
        KeyName = binding.Key;
        isEnabled = binding.IsEnabled;
        displayKey = binding.DisplayString;
    }

    public ShortcutBinding ToBinding() => new(
        ActionId,
        ActionName,
        VirtualKey,
        KeyName,
        Modifiers,
        Scope,
        IsEnabled);

    public void UpdateFromBinding(ShortcutBinding binding)
    {
        VirtualKey = binding.VirtualKey;
        Modifiers = binding.Modifiers;
        Scope = binding.Scope;
        KeyName = binding.Key;
        IsEnabled = binding.IsEnabled;
        DisplayKey = binding.DisplayString;
    }

    public static string FormatRecordingButton(bool isRecording) => isRecording ? "Press keys… (Esc)" : "Record";
}
