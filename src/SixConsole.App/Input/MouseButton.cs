namespace SixConsole.App.Input;

/// <summary>Logical mouse buttons consumed by the engine (source arg numbers in brackets).</summary>
public enum MouseButton
{
    Left,          // 1
    Right,         // 2
    Middle,        // 3 — toggle enabled
    XButton1Back,  // 4 — previous (weapon if caps on, else operator)
    XButton2Forward, // 5 — next (weapon if caps on, else operator)
}

/// <summary>Buttons observed by the hook that the app reacts to.</summary>
internal static class MouseButtonExtensions
{
    public static bool IsActionable(this MouseButton b) => b is MouseButton.Middle or MouseButton.XButton1Back or MouseButton.XButton2Forward;
}
