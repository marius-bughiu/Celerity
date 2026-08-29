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
    public void Cancel_ShouldReturnTheSlotToTheFreeList_WhenTheValueTypeIsAReferenceType()
    {
        TimerWheel<string> wheel = SmallWheel();
        TimerHandle handle = wheel.Schedule(3, "released");

        Assert.True(wheel.Cancel(handle));

        // What is observable is that the slot comes back and the next timer to take it reports its own value.
        // That the vacated slot's payload reference is also cleared is not observable from the public surface
        // at all — it is the reference-typed arm of Vacate, and this is the test that runs it.
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
    public void Schedule_ShouldThrowArgumentOutOfRange_WhenTheDeadlineWouldPassLongMaxValue()
    {
        // The clock runs to long.MaxValue and the horizon is refused against it, rather than the clock being
        // capped below long.MaxValue — which would have accepted timers no advance could ever reach.
        TimerWheel<string> wheel = SmallWheel();
        Drain(wheel, long.MaxValue - 4);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => wheel.Schedule(5, "past the end of time"));
        Assert.Equal("delayTicks", ex.ParamName);

        // And the last delay that does fit is both accepted and reachable.
        wheel.Schedule(4, "the last instant");
        Assert.Equal(["the last instant"], Drain(wheel, long.MaxValue));
        Assert.Equal(long.MaxValue, wheel.CurrentTick);
    }

    [Fact]
    public void Schedule_ShouldStayReachable_ForEveryDelayTheWheelAccepts()
    {
        // The property the ceiling exists for, checked at the point it used to fail: at the end of the clock,
        // whatever Schedule takes, some Advance must be able to fire.
        var wheel = new TimerWheel<long>(4, 2);
        var expired = new List<long>();
        wheel.Advance(long.MaxValue - wheel.Horizon, expired);

        for (long delay = 0; delay < wheel.Horizon; delay++)
            wheel.Schedule(delay, delay);

        Assert.Equal(wheel.Horizon, wheel.Count);
        Assert.Equal((int)wheel.Horizon, wheel.Advance(long.MaxValue, expired));
        Assert.Equal(0, wheel.Count);
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

    [Fact]
    public void Advance_ShouldThrowArgumentException_WhenTheDestinationIsReadOnly()
    {
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(1, "a");

        // Rejected before the clock moves, rather than part-way through the walk when Add first refuses.
        var ex = Assert.Throws<ArgumentException>(() => wheel.Advance(1, new string?[4]));
        Assert.Equal("expired", ex.ParamName);
        Assert.Equal(0, wheel.CurrentTick);
        Assert.Equal(1, wheel.Count);
    }

    [Fact]
    public void Advance_ShouldLeaveAnUndeliveredTimerPending_WhenTheDestinationThrows()
    {
        // The corruption this guards against: delivery is the last step, so a destination that gives up
        // part-way must not leave the timers it refused stranded in a slot the wheel has already walked past.
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(1, "first");
        wheel.Schedule(1, "second");
        wheel.Schedule(9, "kept");

        var destination = new ThrowingCollection(acceptBeforeThrowing: 1);
        Assert.Throws<InvalidOperationException>(() => wheel.Advance(1, destination));

        // One was accepted and is gone; the other is still pending, still counted, still enumerable.
        Assert.Single(destination.Accepted);
        Assert.Equal(2, wheel.Count);
        Assert.Equal(1, wheel.CurrentTick);
        Assert.Contains(wheel, timer => timer.Deadline == 1);

        // And the next advance delivers it, even though its tick has passed. Where in that batch it lands is
        // not promised and this does not check it: nothing else becomes due here, and a timer that did would
        // be prepended ahead of it.
        List<string?> recovered = Drain(wheel, 5);
        Assert.Single(recovered);
        List<string?> together = [.. destination.Accepted.Concat(recovered).Order()];
        Assert.Equal(["first", "second"], together);
        Assert.Equal(1, wheel.Count);
    }

    [Fact]
    public void Advance_ShouldNotInvalidateEnumerators_WhenTheDestinationRefusesItsFirstPayload()
    {
        // Nothing left the pending set, so nothing an enumerator is walking changed. The version steps on what
        // was actually delivered rather than on what the advance was about to deliver.
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(1, "a");
        wheel.Schedule(1, "b");

        var enumerator = wheel.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.Throws<InvalidOperationException>(
            () => wheel.Advance(1, new ThrowingCollection(acceptBeforeThrowing: 0)));

        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());

        // One accepted payload is a real change, and does invalidate.
        var second = wheel.GetEnumerator();
        Assert.Throws<InvalidOperationException>(
            () => wheel.Advance(1, new ThrowingCollection(acceptBeforeThrowing: 1)));
        Assert.Throws<InvalidOperationException>(() => second.MoveNext());
    }

    [Fact]
    public void Advance_ShouldInvalidateEnumerators_BeforeCallingTheDestinationASecondTime()
    {
        // A destination may *read* the wheel from its Add — only mutation is refused — so an enumerator taken
        // before the advance must already be invalid by the time the second payload is offered, rather than
        // walking a wheel the first delivery has vacated a slot from.
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(1, "a");
        wheel.Schedule(1, "b");

        var enumerator = wheel.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        var seen = new List<bool>();
        var destination = new EnumeratingCollection(() =>
        {
            try
            {
                enumerator.MoveNext();
                seen.Add(false);
            }
            catch (InvalidOperationException)
            {
                seen.Add(true);
            }
        });

        Assert.Equal(2, wheel.Advance(1, destination));

        // The first Add ran before anything was removed; the second saw the invalidation.
        Assert.Equal([false, true], seen);
    }

    [Fact]
    public void Advance_ShouldStillCascadeCorrectly_WhenAnEarlierAdvanceWasRefused()
    {
        // The refused advance still moved the clock and still re-placed everything it cascaded, so the timers
        // it did not fire must remain on their own schedule rather than inheriting the failure.
        TimerWheel<string> wheel = SmallWheel();
        wheel.Schedule(2, "due");
        wheel.Schedule(11, "later");

        Assert.Throws<InvalidOperationException>(
            () => wheel.Advance(2, new ThrowingCollection(acceptBeforeThrowing: 0)));

        Assert.Equal(["due"], Drain(wheel, 2));
        Assert.Empty(Drain(wheel, 10));
        Assert.Equal(["later"], Drain(wheel, 11));
    }

    private sealed class EnumeratingCollection(Action onAdd) : ICollection<string?>
    {
        public int Count => 0;

        public bool IsReadOnly => false;

        public void Add(string? item) => onAdd();

        public void Clear()
        {
        }

        public bool Contains(string? item) => false;

        public void CopyTo(string?[] array, int arrayIndex)
        {
        }

        public bool Remove(string? item) => false;

        public IEnumerator<string?> GetEnumerator() => Enumerable.Empty<string?>().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ThrowingCollection(int acceptBeforeThrowing) : ICollection<string?>
    {
        public List<string?> Accepted { get; } = [];

        public int Count => Accepted.Count;

        public bool IsReadOnly => false;

        public void Add(string? item)
        {
            if (Accepted.Count >= acceptBeforeThrowing)
                throw new InvalidOperationException("This destination has had enough.");

            Accepted.Add(item);
        }

        public void Clear() => Accepted.Clear();

        public bool Contains(string? item) => Accepted.Contains(item);

        public void CopyTo(string?[] array, int arrayIndex) => Accepted.CopyTo(array, arrayIndex);

        public bool Remove(string? item) => Accepted.Remove(item);

        public IEnumerator<string?> GetEnumerator() => Accepted.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void Advance_ShouldThrowInvalidOperation_WhenTheDestinationMutatesTheWheel()
    {
        // The destination is the one place this type runs code it does not own, so a callback that reaches
        // back into the wheel would be mutating the buckets and the free list under the loop walking both.
        TimerWheel<string> wheel = SmallWheel();
        TimerHandle handle = wheel.Schedule(1, "a");
        wheel.Schedule(1, "b");

        var destination = new ReentrantCollection(wheel, handle);
        Assert.Throws<InvalidOperationException>(() => wheel.Advance(1, destination));

        // Nothing was delivered, because the refusal came before the first payload was accepted — so both
        // timers are still pending, and the guard is cleared on the way out rather than bricking the wheel.
        Assert.Equal(2, wheel.Count);
        List<string?> both = [.. Drain(wheel, 2).Order()];
        Assert.Equal(["a", "b"], both);
    }

    [Theory]
    [InlineData("Schedule")]
    [InlineData("ScheduleAt")]
    [InlineData("Cancel")]
    [InlineData("Clear")]
    [InlineData("Advance")]
    public void EveryMutatingMember_ShouldThrowInvalidOperation_WhenCalledFromTheDestination(string member)
    {
        TimerWheel<string> wheel = SmallWheel();
        TimerHandle handle = wheel.Schedule(1, "a");

        var destination = new ReentrantCollection(wheel, handle) { Member = member };
        var ex = Assert.Throws<InvalidOperationException>(() => wheel.Advance(1, destination));
        Assert.Contains("delivering expired timers", ex.Message);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenTheSlotArrayWouldOverflowAnInt()
    {
        // 2^30 slots across two levels passes the power-of-two and 2^62-horizon checks, and their product
        // overflows an int — which would reach the allocator as a negative length with an unrelated message.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new TimerWheel<string>(1 << 30, 2));
        Assert.Equal("slotsPerWheel", ex.ParamName);
    }

    private sealed class ReentrantCollection(TimerWheel<string> wheel, TimerHandle handle) : ICollection<string?>
    {
        public string Member { get; init; } = "Cancel";

        public int Count => 0;

        public bool IsReadOnly => false;

        public void Add(string? item)
        {
            switch (Member)
            {
                case "Schedule":
                    wheel.Schedule(1, "reentrant");
                    break;
                case "ScheduleAt":
                    wheel.ScheduleAt(wheel.CurrentTick, "reentrant");
                    break;
                case "Clear":
                    wheel.Clear();
                    break;
                case "Advance":
                    wheel.Advance(wheel.CurrentTick, new List<string?>());
                    break;
                default:
                    wheel.Cancel(handle);
                    break;
            }
        }

        public void Clear()
        {
        }

        public bool Contains(string? item) => false;

        public void CopyTo(string?[] array, int arrayIndex)
        {
        }

        public bool Remove(string? item) => false;

        public IEnumerator<string?> GetEnumerator() => Enumerable.Empty<string?>().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
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
