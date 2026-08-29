using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Runs the <see cref="TimerWheel{TValue}"/> usage examples published in <c>docs/api/collections.md</c>, the
/// README and the type's own XML documentation, and asserts what those pages claim in their comments.
///
/// <para>
/// A published example is documentation a reader will copy, so an incorrect one is a defect rather than a
/// typo. This file exists because the README example was one: it reused a single destination list across two
/// advances without clearing it, so a reader following it would have re-failed every request the previous
/// advance had already failed. Nothing caught that, because the only claim the example makes is about the
/// <i>shape</i> of the loop, and no test ran the shape.
/// </para>
///
/// <para>
/// The examples name a caller's own <c>PendingRequest</c>; the stand-in below carries the one observable the
/// examples use it for — that a timed-out request is failed exactly once.
/// </para>
/// </summary>
public class TimerWheelDocumentationExampleTests
{
    private sealed class PendingRequest(string name)
    {
        public string Name { get; } = name;

        public int Failures { get; private set; }

        public void Fail() => Failures++;
    }

    /// <summary>
    /// The published loop, run twice: clear, advance, consume. Each pass must see only what its own advance
    /// fired — which is what the <c>fired.Clear()</c> in both examples buys, and what their absence cost.
    /// </summary>
    [Fact]
    public void AdvanceExample_ShouldDeliverOnlyTheCurrentBatch_WhenTheListIsClearedBetweenAdvances()
    {
        var timeouts = new TimerWheel<PendingRequest>();
        var fired = new List<PendingRequest?>();

        var early = new PendingRequest("early");
        var late = new PendingRequest("late");
        var cancelled = new PendingRequest("cancelled");

        timeouts.Schedule(delayTicks: 10, early);
        timeouts.Schedule(delayTicks: 5_000_000, late);

        // "The reply beat the clock — O(1), payload released."
        TimerHandle armed = timeouts.Schedule(delayTicks: 30_000, cancelled);
        Assert.True(timeouts.Cancel(armed));

        fired.Clear();
        timeouts.Advance(10, fired);
        foreach (PendingRequest? timedOut in fired)
            timedOut!.Fail();

        Assert.Equal([early], fired);

        // "A jump costs the wheel, not the distance."
        fired.Clear();
        timeouts.Advance(timeouts.CurrentTick + 5_000_000, fired);
        foreach (PendingRequest? timedOut in fired)
            timedOut!.Fail();

        Assert.Equal([late], fired);

        // The point of the clear: each request is failed once, not once per advance that followed it.
        Assert.Equal(1, early.Failures);
        Assert.Equal(1, late.Failures);
        Assert.Equal(0, cancelled.Failures);
    }

    /// <summary>
    /// Pins the contract the examples' <c>fired.Clear()</c> exists for: <c>Advance</c> is documented as
    /// appending and not clearing first, so an uncleared reused list accumulates across advances. This is the
    /// assertion that makes those <c>Clear()</c> calls load-bearing rather than decorative — remove one and
    /// the example is wrong again, but only this test says so.
    /// </summary>
    [Fact]
    public void Advance_ShouldAppendToTheDestination_RatherThanClearingItFirst()
    {
        var timeouts = new TimerWheel<PendingRequest>();
        var fired = new List<PendingRequest?>();

        var first = new PendingRequest("first");
        var second = new PendingRequest("second");

        timeouts.Schedule(delayTicks: 1, first);
        timeouts.Schedule(delayTicks: 2, second);

        Assert.Equal(1, timeouts.Advance(1, fired));
        Assert.Equal([first], fired);

        // No Clear() this time: the second advance appends, so the first batch is still in front of it.
        Assert.Equal(1, timeouts.Advance(2, fired));
        Assert.Equal([first, second], fired);
    }

    /// <summary>
    /// The enumeration example — <c>foreach ((long deadline, PendingRequest? pending) in timeouts)</c> —
    /// which claims both that the element deconstructs into a deadline and a payload, and that the deadline
    /// is absolute, so <c>deadline - CurrentTick</c> is the time left.
    /// </summary>
    [Fact]
    public void PendingExample_ShouldReportTheTimeLeft_AsTheDeadlineMinusTheCurrentTick()
    {
        var timeouts = new TimerWheel<PendingRequest>();
        var request = new PendingRequest("pending");

        timeouts.Schedule(delayTicks: 30_000, request);
        timeouts.Advance(1_000, new List<PendingRequest?>());

        var remaining = new List<long>();
        foreach ((long deadline, PendingRequest? pending) in timeouts)
        {
            Assert.Same(request, pending);
            remaining.Add(deadline - timeouts.CurrentTick);
        }

        // Scheduled at tick 0 for 30,000 ticks out, then the clock moved 1,000 of them.
        Assert.Equal([29_000], remaining);
    }

    /// <summary>
    /// The claim the README's quick start makes about cancellation, which is the axis the type is sold on:
    /// the payload is released, the handle stops resolving, and a second cancel is <c>false</c> rather than
    /// an exception.
    /// </summary>
    [Fact]
    public void CancelExample_ShouldRetireTheHandle_AndReturnFalseOnASecondAttempt()
    {
        var timeouts = new TimerWheel<PendingRequest>();

        TimerHandle armed = timeouts.Schedule(delayTicks: 30_000, new PendingRequest("cancelled"));
        Assert.True(timeouts.TryGetDeadline(armed, out long deadline));
        Assert.Equal(30_000, deadline);

        Assert.True(timeouts.Cancel(armed));
        Assert.False(timeouts.Cancel(armed));
        Assert.False(timeouts.TryGetDeadline(armed, out _));
        Assert.Equal(0, timeouts.Count);
    }
}
