using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="TimerWheel{TValue}"/> against the definition of what a
/// timer wheel is <i>for</i>: a list of deadlines, from which an advance fires exactly the entries that are
/// due and have not been cancelled. CsCheck generates the wheel's geometry and the operation sequence, so a
/// disagreement shrinks to a minimal reproduction with the seed printed.
///
/// <para>
/// The failure mode on trial is the one an example suite written from the same intuition as the cascade
/// arithmetic will step around: a timer placed on a level whose slot the advance never visits, or visited one
/// revolution early, does not throw — it silently fires late, or never. That only shows up against a
/// population of deadlines spread across every level, driven by a clock that sometimes steps a tick and
/// sometimes jumps past a whole wheel.
/// </para>
///
/// <para>
/// The geometry is drawn small on purpose. A four-slot wheel wraps every four ticks and a three-level stack of
/// them has a horizon of 64, so cascades, full-revolution jumps and wrap-around slot reuse are the common case
/// rather than something a long run might eventually reach. The default 256 &#215; 4 wheel would need billions
/// of ticks to exercise the same paths.
/// </para>
/// </summary>
public class TimerWheelDifferentialTests
{
    private static readonly Gen<(int Slots, int Levels, uint Seed)> GenScenario =
        Gen.Select(Gen.Int[1, 3].Select(exponent => 1 << exponent), Gen.Int[1, 3], Gen.UInt);

    [Fact]
    public void AnOperationSequence_ShouldFireExactlyTheTimersThatAreDueAndUncancelled()
    {
        GenScenario.Sample(spec =>
        {
            var wheel = new TimerWheel<int>(spec.Slots, spec.Levels);
            long horizon = wheel.Horizon;
            var rand = new Random((int)spec.Seed);

            // The oracle: every timer ever scheduled, by the id the payload carries.
            var deadlines = new Dictionary<int, long>();
            var handles = new Dictionary<int, TimerHandle>();
            var pending = new HashSet<int>();
            var fired = new List<int>();
            int next = 0;

            for (int step = 0; step < 400; step++)
            {
                switch (rand.Next(10))
                {
                    case 0:
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                    {
                        long delay = rand.Next((int)horizon);
                        int id = next++;
                        handles[id] = wheel.Schedule(delay, id);
                        deadlines[id] = wheel.CurrentTick + delay;
                        pending.Add(id);
                        break;
                    }

                    case 5:
                    case 6:
                    {
                        if (pending.Count == 0)
                            break;

                        int victim = pending.ElementAt(rand.Next(pending.Count));
                        Assert.True(wheel.Cancel(handles[victim]));
                        pending.Remove(victim);
                        break;
                    }

                    case 7:
                    {
                        // A stale handle must never cancel whatever reused its slot.
                        if (fired.Count == 0)
                            break;

                        Assert.False(wheel.Cancel(handles[fired[rand.Next(fired.Count)]]));
                        break;
                    }

                    case 8:
                    {
                        wheel.Clear();
                        pending.Clear();
                        break;
                    }

                    default:
                    {
                        // Sometimes a tick, sometimes a jump past a whole revolution of some level.
                        long jump = rand.Next(3) switch
                        {
                            0 => 0,
                            1 => rand.Next(1, spec.Slots + 1),
                            _ => rand.Next(1, (int)horizon + 1),
                        };

                        long target = wheel.CurrentTick + jump;
                        var expired = new List<int>();
                        int count = wheel.Advance(target, expired!);

                        List<int> expected = [.. pending.Where(id => deadlines[id] <= target).Order()];
                        Assert.Equal(expected.Count, count);
                        Assert.Equal(expected, [.. expired.Order()]);

                        foreach (int id in expected)
                        {
                            pending.Remove(id);
                            fired.Add(id);
                        }

                        break;
                    }
                }

                Assert.Equal(pending.Count, wheel.Count);
            }

            // Everything still pending is reachable through its handle at exactly the deadline it was given,
            // however many cascades it took to get there.
            foreach (int id in pending)
            {
                Assert.True(wheel.TryGetDeadline(handles[id], out long deadline));
                Assert.Equal(deadlines[id], deadline);
            }

            Assert.Equal(
                [.. pending.Select(id => deadlines[id]).Order()],
                [.. wheel.Select(timer => timer.Deadline).Order()]);
        });
    }

    [Fact]
    public void EveryDelayWithinTheHorizon_ShouldFireOnItsOwnTick_WhenTheClockStepsOneTickAtATime()
    {
        // The cascade's exactness, isolated from cancellation: one timer per reachable delay, then a
        // tick-at-a-time walk that must find each of them on precisely its own tick and no other.
        GenScenario.Sample(spec =>
        {
            var wheel = new TimerWheel<long>(spec.Slots, spec.Levels);
            long horizon = wheel.Horizon;

            // Started off zero so the low wheel is mid-revolution and the digits are not all aligned.
            var warmUp = new List<long>();
            wheel.Advance(spec.Seed % horizon, warmUp!);
            long start = wheel.CurrentTick;

            for (long delay = 0; delay < horizon; delay++)
                wheel.Schedule(delay, start + delay);

            var expired = new List<long>();
            for (long tick = start; tick < start + horizon; tick++)
            {
                expired.Clear();
                wheel.Advance(tick, expired!);
                Assert.Equal([tick], expired);
            }

            Assert.Equal(0, wheel.Count);
        });
    }
}
