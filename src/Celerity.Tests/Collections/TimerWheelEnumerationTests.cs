using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="TimerWheel{TValue}"/>: the struct enumerator, both interface-typed
/// enumerators, and the invalidation rule a mutable container has to get right.
///
/// <para>
/// The rule with a decision behind it is the advance that fires nothing. Moving the clock can still <i>move
/// timers</i> — a level-1 slot the clock reaches cascades its contents down into level 0 — but that changes
/// neither the set of pending timers nor the slot each one occupies, so the sequence an enumerator is walking
/// is unaffected and invalidating would be gratuitous. That is the family's own rule, the same one
/// <see cref="SpatialGrid{TValue}.Move"/> is held to, and it is pinned here so a later change cannot quietly
/// tighten it.
/// </para>
/// </summary>
public class TimerWheelEnumerationTests
{
    private static TimerWheel<int> Filled(int count)
    {
        var wheel = new TimerWheel<int>(4, 2);
        for (int i = 0; i < count; i++)
            wheel.Schedule(i % 16, i);

        return wheel;
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryPendingTimer_WhenIterated()
    {
        TimerWheel<int> wheel = Filled(20);

        var seen = new List<int>();
        foreach (ScheduledTimer<int> timer in wheel)
        {
            Assert.Equal(timer.Value % 16, timer.Deadline);
            seen.Add(timer.Value);
        }

        Assert.Equal(Enumerable.Range(0, 20), seen.OrderBy(v => v));
    }

    [Fact]
    public void GetEnumerator_ShouldSkipVacatedSlots_WhenTimersWereCancelledOrFired()
    {
        var wheel = new TimerWheel<int>(4, 2);
        TimerHandle first = wheel.Schedule(1, 1);
        wheel.Schedule(5, 2);
        wheel.Schedule(2, 3);

        wheel.Cancel(first);
        wheel.Advance(2, new List<int>());

        Assert.Equal([2], wheel.Select(t => t.Value).ToArray());
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheWheelIsEmpty()
    {
        var wheel = new TimerWheel<int>(4, 2);

        Assert.Empty(wheel);
        Assert.Equal(0, wheel.Count);
    }

    [Fact]
    public void Enumerator_ShouldClearCurrent_WhenItRunsPastTheEnd()
    {
        var wheel = new TimerWheel<int>(4, 2);
        wheel.Schedule(3, 7);

        TimerWheel<int>.Enumerator enumerator = wheel.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(7, enumerator.Current.Value);

        Assert.False(enumerator.MoveNext());
        Assert.Equal(default, enumerator.Current.Value);
        Assert.Equal(0, enumerator.Current.Deadline);

        enumerator.Dispose();
    }

    [Fact]
    public void Enumerator_ShouldRestart_WhenResetIsCalled()
    {
        TimerWheel<int> wheel = Filled(4);

        TimerWheel<int>.Enumerator enumerator = wheel.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());

        enumerator.Reset();

        var seen = new List<int>();
        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Value);

        Assert.Equal([0, 1, 2, 3], [.. seen.Order()]);
    }

    [Fact]
    public void GenericEnumerator_ShouldYieldEveryPendingTimer_WhenIteratedThroughTheInterface()
    {
        TimerWheel<int> wheel = Filled(5);

        IEnumerator<ScheduledTimer<int>> enumerator = ((IEnumerable<ScheduledTimer<int>>)wheel).GetEnumerator();
        var seen = new List<int>();
        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Value);

        Assert.Equal([0, 1, 2, 3, 4], [.. seen.Order()]);
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldEveryPendingTimer_WhenIteratedThroughTheInterface()
    {
        TimerWheel<int> wheel = Filled(5);

        IEnumerator enumerator = ((IEnumerable)wheel).GetEnumerator();
        var seen = new List<int>();
        while (enumerator.MoveNext())
            seen.Add(((ScheduledTimer<int>)enumerator.Current!).Value);

        Assert.Equal([0, 1, 2, 3, 4], [.. seen.Order()]);
    }

    [Fact]
    public void Enumerator_ShouldThrowInvalidOperation_WhenATimerIsScheduledDuringEnumeration()
    {
        TimerWheel<int> wheel = Filled(3);

        TimerWheel<int>.Enumerator enumerator = wheel.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        wheel.Schedule(1, 99);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Enumerator_ShouldThrowInvalidOperation_WhenATimerIsCancelledDuringEnumeration()
    {
        var wheel = new TimerWheel<int>(4, 2);
        wheel.Schedule(1, 1);
        TimerHandle handle = wheel.Schedule(2, 2);

        TimerWheel<int>.Enumerator enumerator = wheel.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(wheel.Cancel(handle));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Enumerator_ShouldThrowInvalidOperation_WhenAnAdvanceFiresSomething()
    {
        var wheel = new TimerWheel<int>(4, 2);
        wheel.Schedule(1, 1);
        wheel.Schedule(9, 2);

        TimerWheel<int>.Enumerator enumerator = wheel.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        wheel.Advance(1, new List<int>());

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void EnumeratorReset_ShouldThrowInvalidOperation_WhenTheWheelWasModified()
    {
        TimerWheel<int> wheel = Filled(3);

        TimerWheel<int>.Enumerator enumerator = wheel.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        wheel.Schedule(1, 99);

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void Enumerator_ShouldStayValid_WhenAnAdvanceOnlyCascadesTimersBetweenLevels()
    {
        // Nine ticks out is a level-1 timer on a 4 x 2 wheel; advancing to tick 4 reaches its level-1 slot and
        // moves it down without firing it. The sequence is unchanged, so the enumerator must survive.
        var wheel = new TimerWheel<int>(4, 2);
        wheel.Schedule(9, 1);
        wheel.Schedule(11, 2);

        TimerWheel<int>.Enumerator enumerator = wheel.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.Equal(0, wheel.Advance(8, new List<int>()));

        enumerator.Reset();

        var seen = new List<int>();
        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Value);

        Assert.Equal([1, 2], [.. seen.Order()]);
    }

    [Fact]
    public void Clear_ShouldInvalidateEnumerators_WhenItActuallyRemovedSomething()
    {
        TimerWheel<int> wheel = Filled(3);

        TimerWheel<int>.Enumerator enumerator = wheel.GetEnumerator();
        wheel.Clear();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Clear_ShouldKeepEnumeratorsValid_WhenTheWheelWasAlreadyEmpty()
    {
        var wheel = new TimerWheel<int>(4, 2);

        TimerWheel<int>.Enumerator enumerator = wheel.GetEnumerator();
        wheel.Clear();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void ScheduledTimer_ShouldDeconstructAndRender_WhatItCarries()
    {
        var wheel = new TimerWheel<string>(4, 2);
        wheel.Schedule(3, "payload");

        ScheduledTimer<string> timer = wheel.Single();
        (long deadline, string? value) = timer;

        Assert.Equal(3, deadline);
        Assert.Equal("payload", value);
        Assert.Equal("@3 = payload", timer.ToString());
        Assert.Equal(new ScheduledTimer<string>(3, "payload").ToString(), timer.ToString());
    }

    [Fact]
    public void TimerHandle_ShouldCompareByBothSlotAndVersion_WhenEqualityIsAsked()
    {
        var wheel = new TimerWheel<int>(4, 2);
        TimerHandle first = wheel.Schedule(1, 1);
        TimerHandle second = wheel.Schedule(2, 2);
        wheel.Cancel(first);
        TimerHandle reused = wheel.Schedule(3, 3);

        TimerHandle copy = first;
        Assert.True(copy == first);
        Assert.True(first != second);
        Assert.True(first != reused);
        Assert.False(first.Equals(reused));
        Assert.True(first.Equals((object)first));
        Assert.False(first.Equals("not a handle"));
        Assert.Equal(first.GetHashCode(), copy.GetHashCode());
        Assert.NotEqual(default, first);
    }

    [Fact]
    public void TimerHandle_ShouldRenderItsSlotAndVersion_WhenConvertedToAString()
    {
        var wheel = new TimerWheel<int>(4, 2);
        TimerHandle handle = wheel.Schedule(1, 1);

        Assert.Equal("#0.1", handle.ToString());
        Assert.Equal("#0.0", default(TimerHandle).ToString());
    }
}
