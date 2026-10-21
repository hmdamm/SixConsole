namespace SixConsole.App.Services.Shortcuts;

public static class ShortcutValidator
{
    public static bool IsReservedKeyCombination(int virtualKey, KeyModifiers modifiers, out string reason)
    {
        reason = "";
        if (virtualKey == 0x1B && modifiers == KeyModifiers.None)
        {
            reason = "Escape is reserved for canceling shortcut recording and navigation.";
            return true;
        }

        if (virtualKey == 0x09 && (modifiers & KeyModifiers.Alt) != 0)
        {
            reason = "Alt+Tab is reserved by the operating system.";
            return true;
        }

        if (virtualKey == 0x73 && (modifiers & KeyModifiers.Alt) != 0)
        {
            reason = "Alt+F4 is reserved by the operating system.";
            return true;
        }

        if (virtualKey == 0x2E && (modifiers & (KeyModifiers.Control | KeyModifiers.Alt)) == (KeyModifiers.Control | KeyModifiers.Alt))
        {
            reason = "Ctrl+Alt+Del is reserved by the operating system.";
            return true;
        }

        if ((modifiers & KeyModifiers.Windows) != 0)
        {
            if (virtualKey == 0x4C || virtualKey == 0x44)
            {
                reason = "Windows+L and Windows+D are reserved by the operating system.";
                return true;
            }
        }

        // Check if virtualKey is a standalone modifier key
        if (virtualKey is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5)
        {
            reason = "A modifier key alone cannot be used as a primary shortcut key.";
            return true;
        }

        return false;
    }

    public static bool ValidateBinding(ShortcutBinding binding, out string error)
    {
        error = "";
        if (!binding.IsEnabled) return true;

        if (binding.VirtualKey == 0)
        {
            error = "Shortcut key cannot be empty.";
            return false;
        }

        if (IsReservedKeyCombination(binding.VirtualKey, binding.Modifiers, out var reservedReason))
        {
            error = reservedReason;
            return false;
        }

        return true;
    }

    public static bool CheckForConflicts(IEnumerable<ShortcutBinding> bindings, out string conflictError)
    {
        conflictError = "";
        var active = bindings.Where(b => b.IsEnabled && b.VirtualKey != 0).ToList();
        for (int i = 0; i < active.Count; i++)
        {
            for (int j = i + 1; j < active.Count; j++)
            {
                var a = active[i];
                var b = active[j];
                if (a.VirtualKey == b.VirtualKey && a.Modifiers == b.Modifiers && a.Scope == b.Scope)
                {
                    conflictError = $"Conflicting shortcut: '{a.ActionName}' and '{b.ActionName}' share the same key combination ({a.DisplayString}).";
                    return true;
                }
            }
        }
        return false;
    }
}
