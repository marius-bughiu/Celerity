using System;

namespace Celerity.Primitives;

/// <summary>
/// A stateful generator of <strong>strictly monotonic</strong> RFC&#160;9562 version&#160;7 GUIDs: even when
/// many are produced within the same millisecond, each is greater than the last, so the canonical string form
/// is a total time order suitable for database primary keys and append-only logs.
/// </summary>
/// <typeparam name="TRng">The concrete struct PRNG supplying the random tail.</typeparam>
/// <remarks>
/// <para>
/// The stateless <see cref="FastGuid.CreateVersion7{TRng}"/> orders by timestamp across milliseconds but, within
/// one millisecond, only by random bits — so a rapid burst is sortable but not strictly increasing. This
/// generator closes that gap with RFC&#160;9562's monotonic-counter method: it keeps the last timestamp and a
/// 12-bit counter in the <c>rand_a</c> field, advancing the counter when the clock has not moved so each GUID
/// in a same-millisecond run is strictly greater than the previous one. If the counter is exhausted inside a
/// single millisecond (more than ~4096 IDs) it borrows from the next millisecond, preserving monotonicity at
/// the cost of letting the embedded timestamp run a hair ahead of the wall clock. The 62-bit <c>rand_b</c> tail
/// stays random on every draw, so independent generators (other threads / processes / nodes) do not collide.
/// </para>
/// <para>
/// <strong>Not cryptographically secure</strong> and <strong>not thread-safe.</strong> Like the
/// <see cref="IRandomSource"/> generators it wraps, this is a mutable <see langword="struct"/>: call
/// <see cref="Next()"/> on a variable or field (the state advances in place), use one generator per thread,
/// and do not copy it (a copy forks both the PRNG stream and the monotonic counter). For unguessable
/// identifiers use <see cref="Guid.NewGuid()"/> instead.
/// </para>
/// </remarks>
public struct GuidV7Generator<TRng> where TRng : struct, IRandomSource
{
    private TRng _rng;
    private long _lastUnixMs;
    private uint _counter; // 12-bit monotonic counter occupying the rand_a field.

    /// <summary>
    /// Creates a generator seeded by <paramref name="rng"/>. The generator copies the PRNG state (forking it
    /// from the caller's), so subsequent draws advance the copy, not the original.
    /// </summary>
    /// <param name="rng">The PRNG supplying the random <c>rand_b</c> tail and the counter seed.</param>
    public GuidV7Generator(TRng rng)
    {
        _rng = rng;
        _lastUnixMs = long.MinValue;
        _counter = 0;
    }

    /// <summary>
    /// Produces the next monotonic version&#160;7 GUID stamped with the current wall-clock time
    /// (<c>DateTimeOffset.UtcNow</c>).
    /// </summary>
    /// <returns>A version&#160;7 GUID strictly greater than the previous one returned by this generator.</returns>
    public Guid Next() => Next(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    /// <summary>
    /// Produces the next monotonic version&#160;7 GUID for an explicit Unix-millisecond timestamp — the
    /// testable / custom-clock entry point. The result is guaranteed greater than the previous one even if
    /// <paramref name="unixTimeMilliseconds"/> equals or precedes the timestamp of that previous call.
    /// </summary>
    /// <param name="unixTimeMilliseconds">The Unix timestamp in milliseconds; only the low 48 bits are stored.</param>
    /// <returns>A version&#160;7 GUID strictly greater than the previous one returned by this generator.</returns>
    public Guid Next(long unixTimeMilliseconds)
    {
        long ms;
        if (unixTimeMilliseconds > _lastUnixMs)
        {
            ms = unixTimeMilliseconds;
            _lastUnixMs = ms;
            // Seed the 12-bit counter from 11 random bits, leaving ~2048 increments of guaranteed-monotonic
            // headroom within the same millisecond before the counter can overflow.
            _counter = _rng.NextUInt32() & 0x07FFu;
        }
        else
        {
            // Same millisecond, or the wall clock moved backwards: advance the counter so the sequence stays
            // strictly increasing despite the unchanged (or stale) timestamp.
            _counter++;
            if (_counter > 0x0FFFu)
            {
                // Counter exhausted within one millisecond: borrow from the next millisecond. Monotonicity
                // still holds — the embedded timestamp simply runs slightly ahead of the clock.
                _lastUnixMs++;
                _counter = 0;
            }

            ms = _lastUnixMs;
        }

        ulong randB = _rng.NextUInt64();
        return FastGuid.AssembleVersion7(ms, _counter, randB);
    }
}
