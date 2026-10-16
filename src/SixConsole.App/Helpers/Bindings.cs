using Microsoft.UI.Xaml;

namespace SixConsole.App.Helpers;

/// <summary>x:Bind helpers for bool→Visibility and status-code→brush decisions.</summary>
public static class Bindings
{
    public static Visibility BoolToVis(bool b) => b ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility NullOrEmptyToCollapsed(string? s) => string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;
}
