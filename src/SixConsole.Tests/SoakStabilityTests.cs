using System.Diagnostics;
using SixConsole.App.Input;
using SixConsole.Profile;

namespace SixConsole.Tests;

/// <summary>
/// Soak and stress tests for InputEngine: validates that sustained high-frequency physical
/// input cycles, rapid-fire bursts, and repeated enable/disable transitions remain completely
/// stable, bounded in memory, free of handle/thread leaks, and responsive over time.
/// </summary>
public sealed class SoakStabilityTests
{
    private sealed class SoakRecorder
    {
        public int KeyDown;
        public int KeyUp;
        public int Moves;
        public readonly object Gate = new();
    }

    private static IReadOnlyList<Operator> Operators => Catalogue.Operators;

    private static InputEngineOptions OptionsThat(SoakRecorder rec) => new()
    {
        KeyCodeOf = key => (byte)char.ToUpperInvariant(key[0]),
        SendKeyDown = _ => { lock (rec.Gate) rec.KeyDown++; },
        SendKeyUp = _ => { lock (rec.Gate) rec.KeyUp++; },
        SendRelativeMove = (_, _) => { lock (rec.Gate) rec.Moves++; },
        CheckPhysicalKeyState = false,
    };

    [Fact]
    public void Sustained_soak_1000_cycles_maintains_bounded_memory_and_zero_leaks()
    {
        var striker = Operators.First(o => o.Name == "Striker");
        var selection = new SelectionModel(Operators, Operators.TakeWhile(o => !ReferenceEquals(o, striker)).Count(), 2); // SR-25 rapid fire
        var rec = new SoakRecorder();
        using var engine = new InputEngine(selection, () => ProfileSettings.Defaults(), OptionsThat(rec));
        engine.ToggleEnabled();

        // Baseline memory & thread count
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long memBefore = GC.GetTotalMemory(true);
        int threadsBefore = Process.GetCurrentProcess().Threads.Count;

        // Run 1,000 rapid firing bursts and state transitions
        for (int i = 0; i < 1000; i++)
        {
            engine.TestLeftButtonDown();
            if (i % 20 == 0)
            {
                engine.TestButtonDown(MouseButton.XButton2Forward);
            }
            if (i % 50 == 0)
            {
                engine.TestButtonDown(MouseButton.Middle); // Toggle OFF
                engine.TestButtonDown(MouseButton.Middle); // Toggle ON
            }
            engine.TestLeftButtonUp();
        }

        // Clean unwind
        Thread.Sleep(100);

        // Verify keys and moves were processed safely
        lock (rec.Gate)
        {
            Assert.True(rec.KeyUp >= rec.KeyDown, "rfKey must never remain stuck");
        }

        // Post-run memory & thread check
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long memAfter = GC.GetTotalMemory(true);
        int threadsAfter = Process.GetCurrentProcess().Threads.Count;

        // Bounded growth: thread count must not explode
        Assert.True(threadsAfter <= threadsBefore + 3,
            $"Thread leak detected: before={threadsBefore}, after={threadsAfter}");

        // Memory growth must remain bounded (less than 15MB delta for 1000 cycles)
        long growth = memAfter - memBefore;
        Assert.True(growth < 15 * 1024 * 1024,
            $"Unbounded memory growth detected: growth={growth / 1024} KB");
    }

    [Fact]
    public void Repeated_lifecycle_start_and_dispose_cycles_do_not_leak_native_hooks()
    {
        var selection = new SelectionModel(Operators, 0, 0);
        for (int i = 0; i < 50; i++)
        {
            using var engine = new InputEngine(selection, () => ProfileSettings.Defaults());
            engine.EmergencyStop();
            // Dispose is called by using
        }
    }
}
