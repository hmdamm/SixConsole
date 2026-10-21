namespace SixConsole.App.Services.Shortcuts;

public interface IShortcutService : IDisposable
{
    IReadOnlyList<ShortcutBinding> CurrentBindings { get; }
    void RegisterAction(string actionId, Action handler);
    void UnregisterAction(string actionId);
    bool UpdateBindings(IEnumerable<ShortcutBinding> bindings, out string error);
    bool Dispatch(int virtualKey, KeyModifiers modifiers, ShortcutScope scope = ShortcutScope.AppScoped);
    void ResetToDefaults();
    bool IsRecording { get; set; }
}

public sealed class ShortcutService : IShortcutService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Action> _handlers = new(StringComparer.OrdinalIgnoreCase);
    private List<ShortcutBinding> _bindings;
    private bool _disposed;

    public bool IsRecording { get; set; }

    public IReadOnlyList<ShortcutBinding> CurrentBindings
    {
        get
        {
            lock (_gate)
            {
                return _bindings.Select(b => b.Clone()).ToList();
            }
        }
    }

    public ShortcutService(IEnumerable<ShortcutBinding>? initialBindings = null)
    {
        var list = initialBindings?.Select(b => b.Clone()).ToList();
        if (list == null || list.Count == 0)
        {
            list = ShortcutDefaults.CreateDefaults().Select(b => b.Clone()).ToList();
        }
        _bindings = list;
    }

    public void RegisterAction(string actionId, Action handler)
    {
        ArgumentNullException.ThrowIfNull(actionId);
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            if (_disposed) return;
            _handlers[actionId] = handler;
        }
    }

    public void UnregisterAction(string actionId)
    {
        ArgumentNullException.ThrowIfNull(actionId);
        lock (_gate)
        {
            if (_disposed) return;
            _handlers.Remove(actionId);
        }
    }

    public bool UpdateBindings(IEnumerable<ShortcutBinding> bindings, out string error)
    {
        error = "";
        var list = bindings.Select(b => b.Clone()).ToList();

        // 1. Validate each binding
        foreach (var b in list)
        {
            if (!ShortcutValidator.ValidateBinding(b, out var bindError))
            {
                error = $"{b.ActionName}: {bindError}";
                return false;
            }
        }

        // 2. Check for conflicts
        if (ShortcutValidator.CheckForConflicts(list, out var conflictError))
        {
            error = conflictError;
            return false;
        }

        lock (_gate)
        {
            if (_disposed)
            {
                error = "Shortcut service is disposed.";
                return false;
            }
            _bindings = list;
        }
        return true;
    }

    public void ResetToDefaults()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _bindings = ShortcutDefaults.CreateDefaults().Select(b => b.Clone()).ToList();
        }
    }

    public bool Dispatch(int virtualKey, KeyModifiers modifiers, ShortcutScope scope = ShortcutScope.AppScoped)
    {
        if (IsRecording || virtualKey == 0 || _disposed) return false;

        Action? handlerToExecute = null;
        lock (_gate)
        {
            if (_disposed) return false;
            foreach (var b in _bindings)
            {
                if (b.IsEnabled && b.Scope == scope && b.VirtualKey == virtualKey && b.Modifiers == modifiers)
                {
                    if (_handlers.TryGetValue(b.ActionId, out var h))
                    {
                        handlerToExecute = h;
                        break;
                    }
                }
            }
        }

        if (handlerToExecute != null)
        {
            try
            {
                handlerToExecute();
                return true;
            }
            catch
            {
                // Action failures must never crash the dispatcher
            }
        }
        return false;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _handlers.Clear();
            _bindings.Clear();
        }
    }
}
