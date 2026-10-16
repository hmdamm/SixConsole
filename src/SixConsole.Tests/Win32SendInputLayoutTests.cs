using System.Runtime.InteropServices;
using SixConsole.App.Input;

namespace SixConsole.Tests;

/// <summary>
/// Regression tests for the Win32 SendInput marshaling. This exact area shipped broken once:
/// a managed byte[] field inside the explicit-layout INPUTUNION union made the type loader
/// reject the struct (TypeLoadException), crashing the recoil/rapid-fire worker thread on the
/// first synthetic output. Both tests force the runtime layout checks, so a future layout
/// regression fails here instead of in the field.
/// </summary>
public sealed class Win32SendInputLayoutTests
{
    [Fact]
    public void Struct_sizes_match_win64_abi()
    {
        Assert.Equal(Win32.SizeOfMouseInput, Marshal.SizeOf<Win32.MOUSEINPUT>());
        Assert.Equal(Win32.SizeOfKeybdInput, Marshal.SizeOf<Win32.KEYBDINPUT>());
        Assert.Equal(Win32.SizeOfInput, Marshal.SizeOf<Win32.INPUT>());
    }

    [Fact]
    public void Union_with_both_kinds_layouts_without_loader_error()
    {
        // Constructing an INPUTUNION with overlapping MOUSEINPUT/KEYBDINPUT fields forces the
        // loader to validate the explicit layout (the exact step that crashed historically).
        var keyUnion = new Win32.INPUTUNION
        {
            ki = new Win32.KEYBDINPUT { wVk = 0x50, dwFlags = Win32.KEYEVENTF_KEYUP },
        };
        Assert.Equal((ushort)0x50, keyUnion.ki.wVk);

        var mouseUnion = new Win32.INPUTUNION
        {
            mi = new Win32.MOUSEINPUT { dx = 3, dy = 5, dwFlags = Win32.MOUSEEVENTF_MOVE },
        };
        Assert.Equal(3, mouseUnion.mi.dx);
        Assert.Equal(5, mouseUnion.mi.dy);
        // Both assignments share storage: mouseData (first 4 bytes of mi) overlaps wVk+wScan.
    }

    [Fact]
    public void Injector_builds_marshallable_key_and_move_inputs()
    {
        // Exercises the real construction pipeline (JIT + struct init) without injecting input.
        var key = InputInjector.BuildKeyInput((byte)'P', down: true);
        Assert.Single(key);
        Assert.Equal(Win32.INPUT_KEYBOARD, key[0].type);
        Assert.Equal((byte)'P', key[0].u.ki.wVk);
        Assert.Equal(Win32.INJECTED_EXTRAINFO, key[0].u.ki.dwExtraInfo);

        var up = InputInjector.BuildKeyInput((byte)'P', down: false);
        Assert.Equal(Win32.KEYEVENTF_KEYUP, up[0].u.ki.dwFlags);

        var move = InputInjector.BuildMoveInput(1, 2);
        Assert.Equal(Win32.INPUT_MOUSE, move[0].type);
        Assert.Equal(Win32.MOUSEEVENTF_MOVE, move[0].u.mi.dwFlags);
        Assert.Equal(1, move[0].u.mi.dx);
        Assert.Equal(2, move[0].u.mi.dy);

        // Marshal round-trip: the constructed structures must be serializable by the interop
        // layer to the exact byte size SendInput expects.
        Assert.Equal(Marshal.SizeOf<Win32.INPUT>(), Marshal.SizeOf(key[0]));
        Assert.Equal(Marshal.SizeOf<Win32.INPUT>(), Marshal.SizeOf(move[0]));
    }
}
