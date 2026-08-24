using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Dedicated coverage for <see cref="TimerWheel{TValue}"/>: the geometry it accepts, the handles it issues and
/// retires, and the exactness of what an advance fires. The cascade's correctness across every level and every
/// jump size is pinned separately, and randomly, by <see cref="TimerWheelDifferentialTests"/>; this suite
/// pins the contract each member states.
/// </summary>
public class TimerWheelTests
{
    // A tiny wheel makes cascades and wrap-around reachable in a handful of ticks: 4 slots x 2 levels is a
    // horizon of 16, so level 1 holds anything four or more ticks out.
    private static TimerWheel<string> SmallWheel() => new(slotsPerWheel: 4, levels: 2);

    private static List<string?> Drain(TimerWheel<string> wheel, long tick)
    {
        var expired = new List<string?>();
        int fired = wheel.Advance(tick, expired);
        Assert.Equal(fired, expired.Count);
        return expired;
    }

    // ---- construction --------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldDefaultToA256By4Wheel_WhenNoGeometryIsGiven()
    {
        var wheel = new TimerWheel<string>();

        Assert.Equal(256, wheel.SlotsPerWheel);
        Assert.Equal(4, wheel.Levels);
        Assert.Equal(1L << 32, wheel.Horizon);
        Assert.Equal(long.MaxValue - (1L << 32), wheel.MaxTick);
        Assert.Equal(0, wheel.Count);
        Assert.Equal(0, wheel.CurrentTick);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(100)]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenSlotsPerWheelIsNotAPowerOfTwoAtLeastTwo(int slots)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new TimerWheel<string>(slots));
        Assert.Equal("slotsPerWheel", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenLevelsIsBelowOne()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new TimerWheel<string>(4, 0));
        Assert.Equal("levels", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenTheHorizonWouldPass2Pow62()
    {
        // 256 slots is a shift of 8, so eight levels ask for 2^64 ticks and the horizon would wrap negative
        // rather than merely being unreachable.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new TimerWheel<string>(256, 8));
        Assert.Equal("levels", ex.ParamName);

        // The exact ceiling is admissible: 2^62 fits, and one bit more does not.
        Assert.Equal(1L << 62, new TimerWheel<string>(2, 62).Horizon);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenCapacityIsNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new TimerWheel<string>(4, 2, -1));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldPreallocate_WhenCapacityIsGiven()
    {
        var wheel = new TimerWheel<string>(4, 2, capacity: 8);

        for (int i = 0; i < 8; i++)
            wheel.Schedule(1, $"t{i}");

        Assert.Equal(8, wheel.Count);
        Assert.Equal(8, Drain(wheel, 1).Count);
    }

    [Fact]
    public void Constructor_ShouldGiveAOneLevelWheelASlotWideHorizon_WhenLevelsIsOne()
    {
        var wheel = new TimerWheel<string>(slotsPerWheel: 8, levels: 1);

        Assert.Equal(8, wheel.Horizon);
        wheel.Schedule(7, "last");
        Assert.Throws<ArgumentOutOfRangeException>(() => wheel.Schedule(8, "past the horizon"));
        Assert.Equal(["last"], Drain(wheel, 7));
    }

    // ---- scheduling ----------------------------------------------------------------------------------

    [Fact]
    public void Schedule_ShouldFireOnExactlyItsOwnTick_WhenTheClockStepsPastIt()
    {
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(3, "level zero");
        wheel.Schedule(9, "level one");

        Assert.Empty(Drain(wheel, 2));
        Assert.Equal(["level zero"], Drain(wheel, 3));
        Assert.Empty(Drain(wheel, 8));
        Assert.Equal(["level one"], Drain(wheel, 9));
        Assert.Equal(0, wheel.Count);
    }

    [Fact]
    public void Schedule_ShouldFireOnTheNextAdvance_WhenTheDelayIsZero()
    {
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(0, "already due");

        // No wheel slot can hold a timer due at the current tick — every slot is strictly in the future — so
        // this is the one that would be lost if the due list were not there.
        Assert.Equal(1, wheel.Count);
        Assert.Equal(["already due"], Drain(wheel, 0));
        Assert.Equal(0, wheel.Count);
    }

    [Fact]
    public void Schedule_ShouldKeepDuplicatesDistinct_WhenTwoTimersShareADeadlineOrAPayload()
    {
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(5, "same");
        wheel.Schedule(5, "same");

        Assert.Equal(2, wheel.Count);
        Assert.Equal(["same", "same"], Drain(wheel, 5));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void Schedule_ShouldThrowArgumentOutOfRange_WhenTheDelayIsOutsideTheHorizon(long delay)
    {
        TimerWheel<string> wheel = SmallWheel();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => wheel.Schedule(delay, "x"));
        Assert.Equal("delayTicks", ex.ParamName);
    }

    [Fact]
    public void Schedule_ShouldAcceptTheLastTickInsideTheHorizon()
    {
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(15, "the last reachable tick");

        Assert.Equal(["the last reachable tick"], Drain(wheel, 15));
    }

    [Fact]
    public void ScheduleAt_ShouldTakeAnAbsoluteDeadline_OnTheSameClockAsCurrentTick()
    {
        TimerWheel<string> wheel = SmallWheel();
        Drain(wheel, 10);

        wheel.ScheduleAt(10, "due now");
        wheel.ScheduleAt(14, "four ticks out");

        Assert.Equal(["due now"], Drain(wheel, 10));
        Assert.Equal(["four ticks out"], Drain(wheel, 14));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(26)]
    public void ScheduleAt_ShouldThrowArgumentOutOfRange_WhenTheDeadlineIsBehindTheClockOrPastTheHorizon(long deadline)
    {
        TimerWheel<string> wheel = SmallWheel();
        Drain(wheel, 10);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => wheel.ScheduleAt(deadline, "x"));
        Assert.Equal("deadline", ex.ParamName);
    }

    // ---- cancellation --------------------------------------------------------------------------------

    [Fact]
    public void Cancel_ShouldRemoveThePendingTimer_AndReportTrue()
    {
        TimerWheel<string> wheel = SmallWheel();
        TimerHandle handle = wheel.Schedule(3, "cancelled");
        wheel.Schedule(3, "kept");

        Assert.True(wheel.Cancel(handle));
        Assert.Equal(1, wheel.Count);
        Assert.Equal(["kept"], Drain(wheel, 3));
    }

    [Fact]
    public void Cancel_ShouldUnlinkFromAnyPositionInTheSlotsList()
    {
        // Three timers on one slot exercise the head, middle and tail arms of the unlink, which is where an
        // intrusive doubly-linked list loses its neighbours.
        foreach (int victim in new[] { 0, 1, 2 })
        {
            TimerWheel<string> wheel = SmallWheel();
            TimerHandle[] handles = [.. Enumerable.Range(0, 3).Select(i => wheel.Schedule(3, $"t{i}"))];

            Assert.True(wheel.Cancel(handles[victim]));

            List<string?> survivors = [.. Drain(wheel, 3).Order()];
            List<string?> expected = [.. Enumerable.Range(0, 3).Where(i => i != victim).Select(i => (string?)$"t{i}")];
            Assert.Equal(expected, survivors);
        }
    }

    [Fact]
    public void Cancel_ShouldUnlinkFromTheDueList_WhenTheTimerWasScheduledWithNoDelay()
    {
        TimerWheel<string> wheel = SmallWheel();
        TimerHandle handle = wheel.Schedule(0, "cancelled before it could fire");

        Assert.True(wheel.Cancel(handle));
        Assert.Empty(Drain(wheel, 0));
    }

    [Fact]
    public void Cancel_ShouldReportFalse_WhenTheHandleIsDefault()
    {
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(1, "the first timer ever scheduled");

        Assert.False(wheel.Cancel(default));
        Assert.Equal(1, wheel.Count);
    }

    [Fact]
    public void Cancel_ShouldReportFalse_WhenTheHandleAddressesASlotThatWasNeverAllocated()
    {
        TimerWheel<string> wheel = SmallWheel();
        TimerHandle handle = wheel.Schedule(1, "a");
        wheel.Cancel(handle);

        var other = new TimerWheel<string>(4, 2);
        Assert.False(other.Cancel(handle));
    }

    [Fact]
    public void Cancel_ShouldReportFalse_WhenTheTimerAlreadyFired()
    {
        TimerWheel<string> wheel = SmallWheel();
        TimerHandle handle = wheel.Schedule(1, "fired");
        Drain(wheel, 1);

        Assert.False(wheel.Cancel(handle));
    }

    [Fact]
    public void Cancel_ShouldReportFalseTheSecondTime()
    {
        TimerWheel<string> wheel = SmallWheel();
        TimerHandle handle = wheel.Schedule(1, "a");

        Assert.True(wheel.Cancel(handle));
        Assert.False(wheel.Cancel(handle));
    }

    [Fact]
    public void Cancel_ShouldRejectARetiredHandle_WhenAnotherTimerHasReusedItsSlot()
    {
        TimerWheel<string> wheel = SmallWheel();
        TimerHandle retired = wheel.Schedule(1, "first");
        Assert.True(wheel.Cancel(retired));

        TimerHandle reused = wheel.Schedule(1, "second");
        Assert.Equal("#0.1", retired.ToString());
        Assert.Equal("#0.2", reused.ToString());

        Assert.False(wheel.Cancel(retired));
        Assert.Equal(["second"], Drain(wheel, 1));
    }

    [Fact]
    public void Cancel_ShouldReleaseThePayload_SoTheWheelStopsHoldingIt()
    {
        TimerWheel<string> wheel = SmallWheel();
        TimerHandle handle = wheel.Schedule(3, "released");

        Assert.True(wheel.Cancel(handle));

        // The slot is back on the free list with its payload nulled out, so the next timer to reuse it sees
        // its own value and nothing of the cancelled one.
        Assert.Equal(0, wheel.Count);
        wheel.Schedule(3, "reused");
        Assert.Equal(["reused"], Drain(wheel, 3));
    }

    // ---- reading a pending timer ---------------------------------------------------------------------

    [Fact]
    public void TryGetDeadline_ShouldReportTheAbsoluteDeadline_ThroughEveryCascade()
    {
        TimerWheel<string> wheel = SmallWheel();
        TimerHandle handle = wheel.Schedule(15, "far out");

        for (long tick = 0; tick < 15; tick++)
        {
            Drain(wheel, tick);
            Assert.True(wheel.TryGetDeadline(handle, out long deadline));
            Assert.Equal(15, deadline);
        }

        Assert.Equal(["far out"], Drain(wheel, 15));
        Assert.False(wheel.TryGetDeadline(handle, out long gone));
        Assert.Equal(0, gone);
    }

    // ---- advancing -----------------------------------------------------------------------------------

    [Fact]
    public void Advance_ShouldThrowArgumentNullException_WhenTheDestinationIsNull()
    {
        TimerWheel<string> wheel = SmallWheel();

        var ex = Assert.Throws<ArgumentNullException>(() => wheel.Advance(1, null!));
        Assert.Equal("expired", ex.ParamName);
    }

    [Fact]
    public void Advance_ShouldThrowArgumentOutOfRange_WhenTheClockWouldRunBackwards()
    {
        TimerWheel<string> wheel = SmallWheel();
        Drain(wheel, 5);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => wheel.Advance(4, new List<string?>()));
        Assert.Equal("tick", ex.ParamName);
        Assert.Equal(5, wheel.CurrentTick);
    }

    [Fact]
    public void Advance_ShouldThrowArgumentOutOfRange_WhenTheTickWouldPassMaxTick()
    {
        TimerWheel<string> wheel = SmallWheel();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => wheel.Advance(long.MaxValue, new List<string?>()));
        Assert.Equal("tick", ex.ParamName);

        // The ceiling itself is legal, and a timer scheduled from it still lands inside a long.
        Drain(wheel, wheel.MaxTick);
        wheel.Schedule(wheel.Horizon - 1, "the last schedulable tick");
        Assert.Equal(long.MaxValue - 1, wheel.CurrentTick + wheel.Horizon - 1);
    }

    [Fact]
    public void Advance_ShouldAppendRatherThanReplace_SoOneListCanCollectSeveralAdvances()
    {
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(1, "a");
        wheel.Schedule(2, "b");

        var expired = new List<string?>();
        Assert.Equal(1, wheel.Advance(1, expired));
        Assert.Equal(1, wheel.Advance(2, expired));
        Assert.Equal(["a", "b"], expired);
    }

    [Fact]
    public void Advance_ShouldFireNothingAndNotMoveTheClock_WhenTheTickIsTheOneItAlreadyStandsAt()
    {
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(1, "not yet");

        Assert.Empty(Drain(wheel, 0));
        Assert.Equal(0, wheel.CurrentTick);
        Assert.Equal(1, wheel.Count);
    }

    [Fact]
    public void Advance_ShouldFireEverythingDue_WhenTheClockJumpsPastAWholeRevolutionOfEveryLevel()
    {
        TimerWheel<string> wheel = SmallWheel();
        for (long delay = 0; delay < 16; delay++)
            wheel.Schedule(delay, $"d{delay}");

        // One jump to the horizon: every slot on both levels is reached, and the cap that stops the walk
        // visiting a slot twice is the branch under test.
        List<string?> expired = Drain(wheel, 15);

        Assert.Equal(16, expired.Count);
        List<string?> everyDelay = [.. Enumerable.Range(0, 16).Select(d => (string?)$"d{d}").Order()];
        Assert.Equal(everyDelay, [.. expired.Order()]);
        Assert.Equal(0, wheel.Count);
    }

    [Fact]
    public void Advance_ShouldLeaveATimerPending_WhenAJumpReachesItsSlotBeforeItsDeadline()
    {
        // The cascade's other arm: a level-1 slot can be reached by a jump that stops short of the deadlines
        // inside it, and those must move down a level rather than fire early.
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(6, "still to come");

        Assert.Empty(Drain(wheel, 5));
        Assert.Equal(1, wheel.Count);
        Assert.Equal(6, wheel.Single().Deadline);
        Assert.Equal(["still to come"], Drain(wheel, 6));
    }

    [Fact]
    public void Advance_ShouldReturnTheFiredPayloads_WhenCalledWithoutADestination()
    {
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(2, "a");
        wheel.Schedule(9, "b");

        Assert.Equal(["a"], wheel.Advance(2));
        Assert.Empty(wheel.Advance(3));
        Assert.Equal(["b"], wheel.Advance(9));
    }

    [Fact]
    public void Advance_ShouldStayCorrectAcrossManyRevolutions_WhenTheClockStepsATickAtATime()
    {
        TimerWheel<string> wheel = SmallWheel();
        var rand = new Random(17);
        var deadlines = new Dictionary<string, long>();

        for (long tick = 0; tick <= 200; tick++)
        {
            for (int i = 0; i < 2; i++)
            {
                long delay = rand.Next(0, 16);
                string payload = $"t{tick}.{i}";
                wheel.Schedule(delay, payload);
                deadlines[payload] = wheel.CurrentTick + delay;
            }

            List<string?> expired = Drain(wheel, tick);
            List<string?> expected = [.. deadlines.Where(t => t.Value <= tick).Select(t => (string?)t.Key).Order()];
            Assert.Equal(expected, [.. expired.Order()]);

            foreach (string? payload in expired)
                deadlines.Remove(payload!);
        }
    }

    // ---- clearing ------------------------------------------------------------------------------------

    [Fact]
    public void Clear_ShouldRetireEveryHandle_AndLeaveTheClockWhereItStands()
    {
        TimerWheel<string> wheel = SmallWheel();
        Drain(wheel, 7);
        TimerHandle handle = wheel.Schedule(3, "a");
        wheel.Schedule(0, "due");

        wheel.Clear();

        Assert.Equal(0, wheel.Count);
        Assert.Equal(7, wheel.CurrentTick);
        Assert.False(wheel.Cancel(handle));
        Assert.False(wheel.TryGetDeadline(handle, out _));
        Assert.Empty(Drain(wheel, 10));
    }

    [Fact]
    public void Clear_ShouldReuseTheStorage_WithoutReissuingARetiredHandle()
    {
        TimerWheel<string> wheel = SmallWheel();
        TimerHandle retired = wheel.Schedule(3, "a");
        wheel.Clear();

        TimerHandle reissued = wheel.Schedule(3, "b");
        Assert.Equal("#0.1", retired.ToString());
        Assert.Equal("#0.2", reissued.ToString());
        Assert.NotEqual(retired, reissued);
        Assert.False(wheel.Cancel(retired));
        Assert.True(wheel.Cancel(reissued));
    }

    [Fact]
    public void Clear_ShouldNotStepTheVersionOfAnAlreadyVacatedSlot()
    {
        // Vacate retires a slot's handle already. Stepping it again on every unrelated Clear would walk the
        // version round to a value some long-retired handle still holds, far sooner than that slot's own
        // vacations would.
        TimerWheel<string> wheel = SmallWheel();
        TimerHandle first = wheel.Schedule(3, "a");
        Assert.True(wheel.Cancel(first));

        wheel.Schedule(3, "b");
        wheel.Clear();
        wheel.Clear();

        // Slot 0 was vacated by the Cancel and again by the first Clear; the second Clear finds it already
        // free and leaves it alone, so the version has moved twice rather than four times.
        TimerHandle reissued = wheel.Schedule(3, "c");
        Assert.Equal("#0.1", first.ToString());
        Assert.Equal("#0.3", reissued.ToString());
    }

    [Fact]
    public void Clear_ShouldReleaseThePayloads_WhenTheValueTypeIsAReferenceType()
    {
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(3, "dropped");
        wheel.Schedule(0, "also dropped");

        wheel.Clear();

        Assert.Equal(0, wheel.Count);
        Assert.Empty(wheel);

        // Refilling reuses the very slots that held the cleared payloads, and reports only the new ones.
        wheel.Schedule(3, "fresh");
        Assert.Equal(["fresh"], Drain(wheel, 3));
    }

    // ---- value payloads ------------------------------------------------------------------------------

    [Fact]
    public void AValueTypedPayload_ShouldNeedNoClearingWhenASlotIsVacatedOrTheWheelCleared()
    {
        // The unmanaged arm of the payload-release check: nothing to null out, and the wheel must still be
        // correct through a cancel, a fire and a clear.
        var wheel = new TimerWheel<int>(4, 2);
        TimerHandle cancelled = wheel.Schedule(1, 10);
        wheel.Schedule(1, 20);
        wheel.Schedule(9, 30);

        Assert.True(wheel.Cancel(cancelled));

        var expired = new List<int>();
        Assert.Equal(1, wheel.Advance(1, expired));
        Assert.Equal([20], expired);

        wheel.Clear();
        Assert.Equal(0, wheel.Count);
    }

    // ---- growth --------------------------------------------------------------------------------------

    [Fact]
    public void Schedule_ShouldGrowTheEntryStorage_AndKeepEveryHandleAddressingItsOwnTimer()
    {
        var wheel = new TimerWheel<string>(4, 3);
        var handles = new List<TimerHandle>();

        // Well past the initial four slots, spread over every level so the growth interleaves with cascades.
        for (int i = 0; i < 200; i++)
            handles.Add(wheel.Schedule(i % 60, $"t{i}"));

        for (int i = 0; i < 200; i++)
        {
            Assert.True(wheel.TryGetDeadline(handles[i], out long deadline));
            Assert.Equal(i % 60, deadline);
        }

        Assert.Equal(200, wheel.Count);
        Assert.Equal(200, Drain(wheel, 59).Count);
    }
}
