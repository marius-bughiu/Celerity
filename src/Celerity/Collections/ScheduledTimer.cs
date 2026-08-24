namespace Celerity.Collections;

/// <summary>
/// A pending deadline and the value it carries — the element type of <see cref="TimerWheel{TValue}"/>.
/// </summary>
/// <typeparam name="TValue">The payload carried alongside the deadline.</typeparam>
/// <remarks>
/// <para>
/// The deadline is an absolute tick on the wheel's own clock, comparable with
/// <see cref="TimerWheel{TValue}.CurrentTick"/> — not a remaining delay, which would be stale the moment the
/// wheel advanced. Subtract <see cref="TimerWheel{TValue}.CurrentTick"/> for the time left.
/// </para>
/// <para>
/// What a tick <i>means</i> is the caller's choice and the wheel never asks: it is whatever unit was used to
/// schedule and to advance. Milliseconds, frames, and sequence numbers are all ordinary uses.
/// </para>
/// </remarks>
public readonly struct ScheduledTimer<TValue>
{
    /// <summary>Initializes a new pending timer.</summary>
    /// <param name="deadline">The absolute tick at which the timer is due.</param>
    /// <param name="value">The payload to carry. May be <c>null</c> for a reference type.</param>
    public ScheduledTimer(long deadline, TValue? value)
    {
        Deadline = deadline;
        Value = value;
    }

    /// <summary>Gets the absolute tick at which this timer is due.</summary>
    public long Deadline { get; }

    /// <summary>Gets the payload carried by this timer.</summary>
    public TValue? Value { get; }

    /// <summary>Deconstructs the timer into its deadline and value.</summary>
    /// <param name="deadline">Receives the absolute deadline.</param>
    /// <param name="value">Receives the payload.</param>
    public void Deconstruct(out long deadline, out TValue? value)
    {
        deadline = Deadline;
        value = Value;
    }

    /// <summary>Returns a string of the form <c>@deadline = value</c>.</summary>
    /// <returns>A readable rendering of the timer, for debugging.</returns>
    public override string ToString() => $"@{Deadline} = {Value}";
}
