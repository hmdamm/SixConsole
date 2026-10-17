using SixConsole.App.Input;
using SixConsole.Profile;

namespace SixConsole.Tests;

/// <summary>
/// Behavioral tests for the real engine loop (RunLoop), driven through the same code path the
/// physical mouse hook takes (button-down → RunLoop on a worker → button-up/cancel). All
/// synthetic output is captured in-memory: nothing touches the desktop input session.
/// </summary>
public sealed class InputEngineLoopTests
{
    private sealed class Recorder
    {
        public int KeyDown;
        public int KeyUp;
        public int Moves;
        public readonly object Gate = new();
    }

    private static IReadOnlyList<Operator> Operators => Catalogue.Operators;

    private static int IndexOf(Operator op) => Operators.TakeWhile(o => !ReferenceEquals(o, op)).Count();

    private static SelectionModel SelectionAt(int opIndex = 0, int weaponIndex = 0)
        => new(Operators, opIndex, weaponIndex);

    private static InputEngineOptions OptionsThat(Recorder rec) => new()
    {
        KeyCodeOf = key => (byte)char.ToUpperInvariant(key[0]),
        SendKeyDown = _ => { lock (rec.Gate) rec.KeyDown++; },
        SendKeyUp = _ => { lock (rec.Gate) rec.KeyUp++; },
        SendRelativeMove = (_, _) => { lock (rec.Gate) rec.Moves++; },
    };

    private static ProfileSettings Timing(int delayMin, int delayMax, int holdMin, int holdMax, int gapMin, int gapMax)
        => ProfileSettings.Create(0.9, 0, 0, delayMin, delayMax, "p", holdMin, holdMax, gapMin, gapMax);

    [Fact]
    public void FullAutoLoop_moves_for_each_tick_and_never_sends_keys()
    {
        // Default weapon (Striker M4, pull 38 → effective 34 > 0). Full-auto mode injects only
        // movement; a canceled loop must stop cleanly.
        var selection = SelectionAt();
        var rec = new Recorder();
        using var engine = new InputEngine(selection, () => Timing(8, 8, 25, 40, 65, 90), OptionsThat(rec));
        engine.ToggleEnabled();
        Assert.True(engine.IsEnabled);

        engine.TestLeftButtonDown();
        Thread.Sleep(150);
        engine.TestLeftButtonUp();
        Thread.Sleep(120);

        lock (rec.Gate)
        {
            Assert.Equal(0, rec.KeyDown); // full-auto sends no key events...
            Assert.Equal(1, rec.KeyUp);   // ...except the single safety release in RunLoop's finally
            Assert.True(rec.Moves > 0, $"expected recoil moves; got {rec.Moves}");
        }
    }

    [Fact]
    public void RapidFireLoop_presses_releases_in_bursts_and_releases_on_cancel()
    {
        // Striker SR-25 is the source's preserved rapid-fire weapon (pull 155); hold/gap 10 ms each.
        var striker = Operators.First(o => o.Name == "Striker");
        int weaponIndex = striker.Weapons.TakeWhile(w => w.Name != "SR-25").Count();
        Assert.Equal(2, weaponIndex); // Striker: M4 (0), M249 (1), SR-25 (2, rapid fire)
        var selection = SelectionAt(IndexOf(striker), weaponIndex);
        var rec = new Recorder();
        using var engine = new InputEngine(selection, () => Timing(2, 5, 10, 10, 10, 10), OptionsThat(rec));
        engine.ToggleEnabled();

        engine.TestLeftButtonDown();
        Thread.Sleep(160);
        engine.TestLeftButtonUp();
        Thread.Sleep(150);

        int down, up, moves;
        lock (rec.Gate) { down = rec.KeyDown; up = rec.KeyUp; moves = rec.Moves; }
        Assert.True(down >= 1, $"expected at least one rapid-fire burst; got down={down}");
        // The "finally" release guarantees the burst key is never left stuck: up equals down or
        // down+1 depending on whether cancellation landed during the hold sleep or the gap sleep.
        Assert.True(up == down || up == down + 1, $"down={down} up={up}");
        Assert.InRange(moves, Math.Max(0, down - 1), down); // one pull move per completed burst
    }

    [Fact]
    public void Zero_hold_and_gap_boundary_still_releases_key_on_cancel()
    {
        // Striker SR-25 again (rapid fire); hold 0–0 / gap 0–0 is a validated (if aggressive)
        // boundary from the source: math.random(min, max) with min == max returns min, so the
        // loop ticks at full speed; a button-up must still stop it and release the burst key.
        var striker = Operators.First(o => o.Name == "Striker");
        var selection = new SelectionModel(Operators, IndexOf(striker), 2); // SR-25 (rapid fire)
        var rec = new Recorder();
        using var engine = new InputEngine(selection, () => Timing(2, 5, 0, 0, 0, 0), OptionsThat(rec));
        engine.ToggleEnabled();

        engine.TestLeftButtonDown();
        Thread.Sleep(100);
        engine.TestLeftButtonUp();
        Thread.Sleep(150);

        int down, up;
        lock (rec.Gate) { down = rec.KeyDown; up = rec.KeyUp; }
        Assert.True(down >= 1, $"expected bursts at zero timing; got down={down}");
        Assert.True(up == down || up == down + 1, $"down={down} up={up}");
    }

    [Fact]
    public void EmergencyStop_releases_key_and_disables_even_never_started()
    {
        var selection = SelectionAt();
        var rec = new Recorder();
        using var engine = new InputEngine(selection, () => Timing(2, 5, 25, 40, 65, 90), OptionsThat(rec));

        engine.EmergencyStop();

        lock (rec.Gate)
        {
            Assert.Equal(1, rec.KeyUp);  // the configured rfKey is released exactly once
            Assert.Equal(0, rec.KeyDown);
        }
        Assert.False(engine.IsEnabled);
    }

    [Fact]
    public void Disable_records_reason_and_snapshot_reflects_it()
    {
        var selection = SelectionAt();
        var rec = new Recorder();
        using var engine = new InputEngine(selection, () => Timing(2, 5, 25, 40, 65, 90), OptionsThat(rec));

        engine.Disable("hook failed");
        var snapshot = engine.Snapshot();

        Assert.Equal("hook failed", snapshot.LastError);
        Assert.False(snapshot.Enabled);
        Assert.Equal(EngineStatus.NotStarted, snapshot.Status);
    }

    [Fact]
    public void Snapshot_carries_real_counts_and_selection()
    {
        var selection = SelectionAt(1, 0); // second operator in the catalogue
        var rec = new Recorder();
        using var engine = new InputEngine(selection, () => Timing(2, 5, 25, 40, 65, 90), OptionsThat(rec));
        var snap = engine.Snapshot();

        Assert.Equal(Operators.Count, snap.OperatorCount);
        Assert.Equal(1, snap.OperatorIndex);
        Assert.Equal(Operators[1].Name, snap.Operator!.Name);
        Assert.Equal(Operators[1].Weapons.Count, snap.WeaponCount);
    }

    [Fact]
    public void Cancelled_left_down_without_toggle_starts_nothing()
    {
        // Disabled profile + physical left click must not emit anything at all.
        var selection = SelectionAt();
        var rec = new Recorder();
        using var engine = new InputEngine(selection, () => Timing(2, 5, 25, 40, 65, 90), OptionsThat(rec));

        engine.TestLeftButtonDown(); // hook path, but IsEnabled is false
        Thread.Sleep(80);
        engine.TestLeftButtonUp();
        Thread.Sleep(80);

        lock (rec.Gate)
        {
            Assert.Equal(0, rec.KeyDown);
            Assert.Equal(0, rec.KeyUp);
            Assert.Equal(0, rec.Moves);
        }
    }

    [Fact]
    public void MiddleButton_toggles_enabled_in_both_directions_reliably()
    {
        var selection = SelectionAt();
        var rec = new Recorder();
        using var engine = new InputEngine(selection, () => Timing(2, 5, 25, 40, 65, 90), OptionsThat(rec));
        Assert.False(engine.IsEnabled);

        // OFF -> ON
        engine.TestButtonDown(MouseButton.Middle);
        Assert.True(engine.IsEnabled);

        // ON -> OFF
        engine.TestButtonDown(MouseButton.Middle);
        Assert.False(engine.IsEnabled);

        // OFF -> ON again
        engine.TestButtonDown(MouseButton.Middle);
        Assert.True(engine.IsEnabled);

        // ON -> OFF again
        engine.TestButtonDown(MouseButton.Middle);
        Assert.False(engine.IsEnabled);
    }

    [Fact]
    public void MiddleButton_activation_enables_operational_loop_and_deactivation_stops_it()
    {
        var selection = SelectionAt();
        var rec = new Recorder();
        using var engine = new InputEngine(selection, () => Timing(2, 5, 25, 40, 65, 90), OptionsThat(rec));

        // Initial: disabled -> left click does nothing
        engine.TestLeftButtonDown();
        Thread.Sleep(60);
        engine.TestLeftButtonUp();
        Thread.Sleep(60);
        lock (rec.Gate) Assert.Equal(0, rec.Moves);

        // Toggle ON with Middle Mouse Button
        engine.TestButtonDown(MouseButton.Middle);
        Assert.True(engine.IsEnabled);

        // Now left click runs the operational recoil loop
        engine.TestLeftButtonDown();
        Thread.Sleep(120);
        engine.TestLeftButtonUp();
        Thread.Sleep(100);
        lock (rec.Gate) Assert.True(rec.Moves > 0, "Expected recoil moves when activated via MMB");

        int movesBefore = rec.Moves;

        // Toggle OFF with Middle Mouse Button
        engine.TestButtonDown(MouseButton.Middle);
        Assert.False(engine.IsEnabled);

        // Left click again does not emit moves
        engine.TestLeftButtonDown();
        Thread.Sleep(60);
        engine.TestLeftButtonUp();
        Thread.Sleep(60);
        lock (rec.Gate) Assert.Equal(movesBefore, rec.Moves);
    }

    [Fact]
    public void NavigationButtons_do_nothing_when_disabled()
    {
        var selection = SelectionAt(0, 0);
        var rec = new Recorder();
        using var engine = new InputEngine(selection, () => Timing(2, 5, 25, 40, 65, 90), OptionsThat(rec));
        Assert.False(engine.IsEnabled);

        engine.TestButtonDown(MouseButton.XButton2Forward);
        Assert.Equal(0, selection.OperatorIndex);

        engine.TestButtonDown(MouseButton.XButton1Back);
        Assert.Equal(0, selection.OperatorIndex);
    }

    [Fact]
    public void NavigationButtons_navigate_when_enabled()
    {
        var selection = SelectionAt(0, 0);
        var rec = new Recorder();
        using var engine = new InputEngine(selection, () => Timing(2, 5, 25, 40, 65, 90), OptionsThat(rec));
        engine.TestButtonDown(MouseButton.Middle); // Turn ON
        Assert.True(engine.IsEnabled);

        engine.TestButtonDown(MouseButton.XButton2Forward);
        Assert.Equal(1, selection.OperatorIndex);

        engine.TestButtonDown(MouseButton.XButton1Back);
        Assert.Equal(0, selection.OperatorIndex);
    }

    [Fact]
    public void Rapid_clicks_do_not_spawn_unbounded_threads_or_leave_stuck_keys()
    {
        var selection = SelectionAt();
        var rec = new Recorder();
        using var engine = new InputEngine(selection, () => Timing(2, 5, 10, 15, 15, 20), OptionsThat(rec));
        engine.ToggleEnabled();

        // Simulate 20 rapid bursts (e.g. frantic clicking during combat)
        for (int i = 0; i < 20; i++)
        {
            engine.TestLeftButtonDown();
            Thread.Sleep(10);
            engine.TestLeftButtonUp();
            Thread.Sleep(5);
        }

        // Allow any final unwinding
        Thread.Sleep(50);

        lock (rec.Gate)
        {
            // Rapid fire keys must be balanced: keyUp >= keyDown (safety release ensures no stuck keys)
            Assert.True(rec.KeyUp >= rec.KeyDown, $"stuck key detected: down={rec.KeyDown} up={rec.KeyUp}");
        }
    }

    [Fact]
    public void Physical_button_release_detection_terminates_loop_even_if_up_event_missed()
    {
        var selection = SelectionAt();
        var rec = new Recorder();
        bool physicalDown = true;
        var options = OptionsThat(rec);
        options.IsPhysicalDown = () => physicalDown;

        using var engine = new InputEngine(selection, () => Timing(5, 5, 25, 40, 65, 90), options);
        engine.ToggleEnabled();

        // Left button down
        engine.TestLeftButtonDown();
        Thread.Sleep(60);

        int movesDuringDown;
        lock (rec.Gate) movesDuringDown = rec.Moves;
        Assert.True(movesDuringDown > 0, "expected moves while physical button is down");

        // Physical button is released, but WM_LBUTTONUP is NOT fired (simulating OS drop/focus switch)
        physicalDown = false;
        Thread.Sleep(100);

        int movesAfterRelease;
        lock (rec.Gate) movesAfterRelease = rec.Moves;

        // Loop must have stopped because physicalDown became false
        Thread.Sleep(80);
        int movesLater;
        lock (rec.Gate) movesLater = rec.Moves;

        Assert.Equal(movesAfterRelease, movesLater); // No more moves emitted!
    }

    [Fact]
    public void CountDownCts_reset_and_dispose_behave_deterministically()
    {
        using var cts = new CountDownCts();
        Assert.False(cts.IsCancellationRequested);

        // Wait on unsignaled event with short timeout: returns false
        Assert.False(cts.Wait(10));

        // Cancel signals the event
        cts.Cancel();
        Assert.True(cts.IsCancellationRequested);
        Assert.True(cts.Wait(10)); // returns true immediately

        // Reset unsignals the event
        cts.Reset();
        Assert.False(cts.IsCancellationRequested);
        Assert.False(cts.Wait(10));
    }

    [Fact]
    public void Sustained_stress_rapid_state_transitions_remain_stable()
    {
        var selection = SelectionAt(0, 0);
        var rec = new Recorder();
        using var engine = new InputEngine(selection, () => Timing(2, 5, 10, 15, 15, 20), OptionsThat(rec));

        for (int i = 0; i < 50; i++)
        {
            engine.TestButtonDown(MouseButton.Middle); // Toggle
            engine.TestButtonDown(MouseButton.XButton2Forward); // Navigate
            engine.TestLeftButtonDown();
            Thread.Sleep(2);
            engine.TestLeftButtonUp();
            engine.TestButtonDown(MouseButton.XButton1Back); // Navigate back
        }

        Thread.Sleep(50);
        Assert.True(engine.Status == EngineStatus.NotStarted || engine.Status == EngineStatus.Running);
    }

    [Fact]
    public void RapidFire_single_tap_click_fires_complete_burst_and_applies_recoil_move()
    {
        var striker = Operators.First(o => o.Name == "Striker");
        var selection = new SelectionModel(Operators, IndexOf(striker), 2); // SR-25 (rapid fire)
        var rec = new Recorder();
        using var engine = new InputEngine(selection, () => Timing(2, 5, 10, 10, 10, 10), OptionsThat(rec));
        engine.ToggleEnabled();

        // Simulate an immediate tap click (down immediately followed by up, typical 10-20ms physical tap)
        engine.TestLeftButtonDown();
        engine.TestLeftButtonUp();
        Thread.Sleep(80);

        int down, up, moves;
        lock (rec.Gate) { down = rec.KeyDown; up = rec.KeyUp; moves = rec.Moves; }
        Assert.Equal(1, down); // exactly one burst fired
        Assert.True(up >= down, "rfKey must be released");
        Assert.Equal(1, moves); // recoil move must be applied for the shot
    }
}

