namespace Celerity.Collections;

/// <summary>
/// A single entry in a <see cref="TopKSketch{T, THasher}"/>'s top-k result: a monitored
/// element together with its estimated occurrence count and the maximum amount that count
/// could overestimate the element's true frequency.
/// </summary>
/// <remarks>
/// The Space-Saving guarantee bounds the true frequency of <see cref="Element"/> to the
/// closed interval <c>[<see cref="Count"/> − <see cref="Error"/>, <see cref="Count"/>]</c>:
/// the sketch never underestimates (<see cref="Count"/> ≥ the true frequency), and it
/// overestimates by at most <see cref="Error"/> — the count of the entry that was evicted
/// from this monitor's slot. A newly monitored element that has never shared its slot has
/// <see cref="Error"/> <c>0</c>, so its <see cref="Count"/> is exact.
/// </remarks>
/// <typeparam name="T">The element type.</typeparam>
public readonly struct TopKEntry<T>
{
    /// <summary>
    /// Initializes a new <see cref="TopKEntry{T}"/> with the given element, count, and error.
    /// </summary>
    /// <param name="element">The monitored element.</param>
    /// <param name="count">The estimated occurrence count (an upper bound on the true frequency).</param>
    /// <param name="error">The maximum amount <paramref name="count"/> overestimates the truth.</param>
    public TopKEntry(T element, long count, long error)
    {
        Element = element;
        Count = count;
        Error = error;
    }

    /// <summary>
    /// Gets the monitored element.
    /// </summary>
    public T Element { get; }

    /// <summary>
    /// Gets the estimated number of occurrences of <see cref="Element"/> — an upper bound on
    /// its true frequency (the sketch never underestimates).
    /// </summary>
    public long Count { get; }

    /// <summary>
    /// Gets the maximum amount by which <see cref="Count"/> may exceed the true frequency of
    /// <see cref="Element"/>. The true frequency lies in <c>[Count − Error, Count]</c>; an
    /// <see cref="Error"/> of <c>0</c> means <see cref="Count"/> is exact.
    /// </summary>
    public long Error { get; }

    /// <summary>
    /// Returns a string of the form <c>Element (count, err error)</c>.
    /// </summary>
    /// <returns>A human-readable representation of the entry.</returns>
    public override string ToString() => $"{Element} ({Count}, err {Error})";
}
