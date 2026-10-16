using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SixConsole.App.Input;
using SixConsole.App.Services;
using SixConsole.Profile;

namespace SixConsole.App.ViewModels;

public sealed partial class ProfileViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly ProfileController _controller;
    private readonly EventHandler<EngineEventArgs> _onEngineState;
    private string _search = "";

    // Rebuild guards: engine events fire frequently (every press/release); rebuild the
    // roster/weapon lists only when something they render actually changed.
    private string _lastRosterSelectedId = "\0";
    private string _lastRosterSearch = "\0";
    private string _lastWeaponOperatorId = "\0";
    private string _lastWeaponSignature = "\0";

    public ObservableCollection<OperatorRow> Attackers { get; } = new();
    public ObservableCollection<OperatorRow> Defenders { get; } = new();
    public ObservableCollection<WeaponRow> Weapons { get; } = new();

    [ObservableProperty]
    private bool isEnabled;

    [ObservableProperty]
    private string statusText = "DISABLED";

    [ObservableProperty]
    private string statusCode = "muted"; // muted | success | warning | error

    [ObservableProperty]
    private string engineStatusText = "Engine initializing…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastErrorVisible))]
    private string lastError = "";

    public bool LastErrorVisible => !string.IsNullOrEmpty(LastError);    [ObservableProperty]
    private string selectedOperatorId = "";

    [ObservableProperty]
    private int selectedWeaponIndex; // one-based index minus one; drives the ListView highlight

    public string Search
    {
        get => _search;
        set
        {
            if (SetProperty(ref _search, value))
            {
                RebuildRoster(SelectedOperatorId);
            }
        }
    }

    public ProfileViewModel(AppServices services, ProfileController controller)
    {
        _services = services;
        _controller = controller;
        _onEngineState = (_, e) => App.DispatchToUi(() => ApplySnapshot(e.Snapshot));
        controller.StateChanged += _onEngineState;
        services.SettingsApplied += OnSettingsApplied;
        ApplySnapshot(controller.Snapshot());
    }

    /// <summary>Detaches engine/service subscriptions; called when the hosting view unloads.
    /// Without this, every visit to the Profile page would leave a permanent ghost subscription.</summary>
    public void Detach()
    {
        _controller.StateChanged -= _onEngineState;
        _services.SettingsApplied -= OnSettingsApplied;
    }

    private void OnSettingsApplied() => App.DispatchToUi(RefreshFromEngineNow);

    public void RefreshFromEngineNow() => ApplySnapshot(_controller.Snapshot());

    public void ApplySnapshot(EngineSnapshot snap)
    {
        IsEnabled = snap.Enabled;
        StatusText = SnapToStatus(snap);
        StatusCode = snap.Status switch
        {
            EngineStatus.Running => snap.Enabled ? "success" : "muted",
            EngineStatus.StoppedAfterError => "error",
            EngineStatus.NotStarted => "warning",
            _ => "warning",
        };
        EngineStatusText = $"Engine: {snap.Status}";
        LastError = snap.LastError ?? "";
        SelectedOperatorId = snap.Operator?.Id ?? "";
        RefreshWeaponList(snap.Operator, snap.WeaponIndex, _services.Settings);
        RebuildRoster(SelectedOperatorId);
        ApplyWeaponSelection(snap.WeaponIndex);
        OnPropertyChanged(nameof(SelectedOperatorName));
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void ApplyWeaponSelection(int weaponIndex)
    {
        if (Weapons.Count == 0)
        {
            if (SelectedWeaponIndex != -1) SelectedWeaponIndex = -1;
            return;
        }
        // Collection was rebuilt: re-push the value even when unchanged so the ListView
        // re-applies its selection after the reset that Clear/Add performs.
        if (SelectedWeaponIndex == weaponIndex) OnPropertyChanged(nameof(SelectedWeaponIndex));
        else SelectedWeaponIndex = weaponIndex;
    }

    private static string SnapToStatus(EngineSnapshot s) => s.Status switch
    {
        EngineStatus.Running when s.Enabled => "ACTIVE",
        EngineStatus.Running => "DISABLED",
        EngineStatus.NotStarted => "INITIALIZING",
        EngineStatus.StoppedAfterError => "ENGINE ERROR",
        EngineStatus.StoppedByUser => "STOPPED",
        EngineStatus.StoppedForShutdown => "SHUTTING DOWN",
        _ => "UNKNOWN",
    };

    public string SelectedOperatorName
    {
        get
        {
            var op = _services.Selection.Operator;
            return $"{op.Side.ToLabel()} · {op.Name}";
        }
    }

    public string SelectionSummary
    {
        get
        {
            var sel = _services.Selection;
            var op = sel.Operator;
            var w = sel.Weapon;
            var settings = _services.Settings;
            var mode = settings.ModeFor(w);
            var range = mode == InputMode.RapidFire ? settings.Hold : settings.Delay;
            var label = mode == InputMode.RapidFire ? "Rapid-fire hold" : "Full-auto delay";
            return
                $"Operator {sel.OperatorIndexOneBased}/{Operators.Count} · Weapon {sel.WeaponIndexOneBased}/{op.Weapons.Count} · " +
                $"{ModeLabel(mode)} · Pull {settings.EffectivePull(w)} px · {label} {range.Min}–{range.Max} ms";
        }
    }

    [RelayCommand]
    private void ToggleEnabled()
    {
        _controller.Toggle();
    }

    [RelayCommand]
    private void EmergencyStop() => _controller.EmergencyStop();

    [RelayCommand]
    private void SelectOperator(string operatorId)
    {
        if (string.IsNullOrEmpty(operatorId) || !Operators.Any(o => o.Id == operatorId)) return;
        if (_services.Selection.Operator.Id == operatorId) return;
        try
        {
            _services.Selection.SelectOperator(operatorId);
        }
        catch (ArgumentOutOfRangeException)
        {
            // unknown id: ignore, the roster simply keeps its current selection
            return;
        }
        ApplySnapshot(_controller.Snapshot());
    }

    [RelayCommand]
    private void SelectWeapon(WeaponRow row)
    {
        if (_services.Selection.WeaponIndex == row.OneBased - 1) return;
        _services.Selection.SelectWeapon(_services.Selection.Operator, row.OneBased);
        ApplySnapshot(_controller.Snapshot());
    }

    [RelayCommand]
    private void NextOperator() => _services.Selection.NavigateOperator(NavigationDirection.Next);

    [RelayCommand]
    private void PreviousOperator() => _services.Selection.NavigateOperator(NavigationDirection.Previous);

    /// <summary>Rebuilds roster; search filters names and never mutates underlying data ordering.</summary>
    private void RosterCore(string selectedId)
    {
        Attackers.Clear();
        Defenders.Clear();
        foreach (var op in Operators)
        {
            if (_search.Length > 0 && !op.Name.Contains(_search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var row = new OperatorRow(op.Id, op.Name, op.Side.ToLabel(), op.Weapons.Count, op.Id == selectedId);
            if (op.Side == OperatorSide.Attacker) Attackers.Add(row);
            else Defenders.Add(row);
        }
    }

    private void RebuildRoster(string selectedId)
    {
        if (string.Equals(_search, _lastRosterSearch, StringComparison.Ordinal)
            && (Attackers.Count + Defenders.Count) > 0)
        {
            if (selectedId == _lastRosterSelectedId) return;
            _lastRosterSelectedId = selectedId;
            foreach (var r in Attackers) r.IsSelected = r.OperatorId == selectedId;
            foreach (var r in Defenders) r.IsSelected = r.OperatorId == selectedId;
            return;
        }
        _lastRosterSelectedId = selectedId;
        _lastRosterSearch = _search;
        RosterCore(selectedId);
    }

    private void RefreshWeaponList(Operator? op, int weaponIndex, ProfileSettings settings)
    {
        var signature =
            $"{settings.RecoilMultiplier}:{settings.VertVariance}:{settings.HorizJitter}:" +
            $"{settings.Delay.Min}-{settings.Delay.Max}:{settings.RfKey}:" +
            $"{settings.Hold.Min}-{settings.Hold.Max}:{settings.Gap.Min}-{settings.Gap.Max}";
        var opId = op?.Id ?? "";
        if (opId == _lastWeaponOperatorId && signature == _lastWeaponSignature && Weapons.Count == (op?.Weapons.Count ?? 0))
        {
            return; // same operator, same settings: rows already correct
        }
        _lastWeaponOperatorId = opId;
        _lastWeaponSignature = signature;
        Weapons.Clear();
        if (op is null) return;
        for (int i = 0; i < op.Weapons.Count; i++)
        {
            var w = op.Weapons[i];
            var mode = settings.ModeFor(w);
            var timing = mode == InputMode.RapidFire
                ? $"Hold {settings.Hold.Min}–{settings.Hold.Max} ms"
                : $"{settings.Delay.Min}–{settings.Delay.Max} ms";
            Weapons.Add(new WeaponRow(i + 1, w, settings.EffectivePull(w), ModeLabel(mode), timing));
        }
    }

    private static IReadOnlyList<Operator> Operators => Catalogue.Operators;

    public static string ModeLabel(InputMode mode) => mode == InputMode.RapidFire ? "Rapid fire" : "Full-auto recoil";
}
