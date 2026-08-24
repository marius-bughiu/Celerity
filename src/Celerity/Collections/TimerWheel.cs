using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A <b>hierarchical timing wheel</b>: constant-time <see cref="Cancel"/> and amortized constant-time
/// <see cref="Schedule"/> over a population of pending deadlines, and an <see cref="Advance(long, ICollection{TValue})"/> bounded by the
/// wheel's own geometry and the timers it moves rather than by the ticks it crosses — the container for
/// <i>which of these hundred thousand pending things have timed out</i>, which no heap answers cheaply
/// because a heap cannot cancel.
/// </summary>
/// <typeparam name="TValue">The payload carried by each timer.</typeparam>
/// <remarks>
/// <para>
/// <b>This is a data structure, not a scheduler.</b> There is no thread, no clock and no callback: the caller
/// owns time and drives the wheel with <see cref="Advance(long, ICollection{TValue})"/>, which is what keeps
/// the type deterministic, testable, and inside this library's
/// <see href="https://github.com/marius-bughiu/Celerity/blob/main/ROADMAP.md#non-goals">non-goals</see>. A tick
/// is whatever unit the caller schedules and advances in — milliseconds, frames, sequence numbers.
/// </para>
/// <para>
/// <b>The workload is defined by cancellation.</b> Almost every timeout is cancelled rather than fired: the
/// reply arrives, the lease is renewed, the connection closes cleanly. That is the axis on which the obvious
/// structures fail. <see cref="PriorityQueue{TElement, TPriority}"/> has no removal on the <c>net8.0</c> floor
/// at all, and the <c>Remove</c> added in .NET 9 is documented <c>O(n)</c>, so the standard workaround is lazy
/// deletion — push everything, keep a <c>HashSet</c> of cancelled ids, and discard tombstones on pop. The heap
/// then grows with the timers that will never fire, keeps their payloads alive until they are popped, and pays
/// a hash probe per pop. <see cref="IndexedPriorityQueue{TElement, TPriority, THasher}"/> — this library's
/// addressable heap — cancels properly, in <c>O(log n)</c>, and is the strong baseline this type is measured
/// against.
/// </para>
/// <para>
/// <b>Layout.</b> <see cref="Levels"/> wheels of <see cref="SlotsPerWheel"/> slots each, both powers of
/// two, so every index is a shift and a mask. Level <c>L</c> spans <c>slots^(L+1)</c> ticks, and a timer lands
/// in the lowest level that can express its delay, in the slot its deadline's level-<c>L</c> digit names. The
/// slots are heads of intrusive doubly-linked lists threaded through one flat entry array — the same layout
/// <see cref="SpatialGrid{TValue}"/> uses for its cells — so there is no object per timer and no allocation
/// per schedule once the array has grown. A cancel is two pointer writes.
/// </para>
/// <para>
/// <b><see cref="Advance(long, ICollection{TValue})"/> is not the textbook tick-at-a-time loop.</b> The
/// classical wheel steps one tick, then one more, cascading whenever the low wheel wraps, which makes a jump
/// cost <c>O(ticks)</c> — a caller that misses a second of wall clock at millisecond granularity would pay a
/// thousand iterations over empty slots for it. This one computes, per level, exactly the slots the move
/// crosses and walks them from the top level down, firing what is due and staging what is not for
/// re-insertion against the new time. The work is therefore <c>O(levels &#215; slots + fired + cascaded)</c>
/// <i>however far the clock jumped</i>, and <c>O(ticks + fired + cascaded)</c> for the ordinary small step —
/// the cascade term belongs in both, because the single tick that carries the clock across a level boundary
/// reaches that level's whole slot and moves everything on it down, which can be any number of timers and
/// fire none of them. A cascade only ever moves a timer <i>down</i> a level, so each timer is touched at most
/// once per level over its whole life, which is what keeps that term amortized rather than repeated.
/// </para>
/// <para>
/// <b>The horizon is the trade.</b> A wheel buys its constant time by bucketing rather than ordering, and the
/// bucketing is finite: <see cref="Horizon"/> is <c>slots^levels</c> ticks — 2^32 at the defaults, so a
/// millisecond tick reaches about 49 days — and a longer delay is rejected rather than silently misplaced.
/// Widen it by adding a level (each multiplies the horizon by <see cref="SlotsPerWheel"/> and costs another
/// <see cref="SlotsPerWheel"/> slot heads, 1 KiB at the default width) or by choosing a coarser tick. The
/// other half of the trade is that the wheel does <b>not</b> order: timers fired by one <see cref="Advance(long, ICollection{TValue})"/> are delivered in an
/// unspecified order, and only the guarantee that every one of them is due — deadline at or before the tick
/// advanced to — is promised. That holds even when the clock is stepped one tick at a time: a zero-delay
/// schedule, or a timer an earlier advance could not deliver, waits on the already-due list and comes back
/// alongside the next tick's own, so a batch can span deadlines however finely the clock is driven. A caller
/// who needs the earliest deadline first wants a priority queue, and pays <c>O(log n)</c> for it.
/// </para>
/// <para>
/// <b><see cref="Cancel"/> returns a <see cref="bool"/> rather than throwing</b> on a handle that no longer
/// addresses a pending timer, which is the one deliberate divergence from
/// <see cref="SpatialGrid{TValue}.Remove"/>. For a timer, "it already fired" is the normal outcome of the race
/// the caller is running against their own clock, not a programming error.
/// </para>
/// <para>
/// <b>What the bound really is.</b> <see cref="Cancel"/> and <see cref="TryGetDeadline"/> are <c>O(1)</c>
/// outright. <see cref="Schedule"/> is <c>O(1)</c> amortized: the one call in a growth cycle that finds the
/// free list empty and the entry array full resizes and copies the backing arrays, which is <c>O(n)</c> for
/// that call. The level search inside <see cref="Schedule"/> is a loop of at most <see cref="Levels"/>
/// comparisons — four at the default geometry, and 62 at the narrowest legal wheel, which is the ceiling the
/// constructor allows — and in a workload with one characteristic timeout it is the same count every time.
/// </para>
/// <para>
/// <b>The wheel may not be modified from the destination it is delivering into.</b>
/// <see cref="Advance(long, ICollection{TValue})"/> is the one place this type runs code it does not own, and
/// a destination whose <c>Add</c> calls back into the wheel would mutate the buckets and the free list under
/// the loop that is walking both. Such a call throws <see cref="InvalidOperationException"/> rather than
/// corrupting the structure quietly. Schedule and cancel after the advance returns — which is what a
/// <c>foreach</c> over the destination does anyway.
/// </para>
/// <para>
/// <b>Capacity only grows.</b> There is no <c>TrimExcess</c>: a handle <i>is</i> a position in the entry
/// array, so compacting it would invalidate every handle a caller is holding, which is the one thing this type
/// promises not to do. <see cref="Clear"/> retires all outstanding handles and returns the storage to the free
/// list for reuse, but keeps it, and leaves <see cref="CurrentTick"/> where it was — clearing empties the
/// container, it does not rewind the clock.
/// </para>
/// <para>
/// Enumeration yields every pending timer in an unspecified order — slot order, which is neither deadline nor
/// insertion order — and is invalidated by <see cref="Schedule"/>, a successful <see cref="Cancel"/>, an
/// <see cref="Advance(long, ICollection{TValue})"/> that fired something, and a <see cref="Clear"/> that
/// removed something. An <see cref="Advance(long, ICollection{TValue})"/> that fires nothing deliberately does
/// <i>not</i> invalidate it, even when it cascaded timers between levels: cascading changes neither the set of
/// pending timers nor the slot each occupies, so the sequence an enumerator is walking is unaffected.
/// </para>
/// </remarks>
public sealed class TimerWheel<TValue> : IReadOnlyCollection<ScheduledTimer<TValue>>
{
    private const int DefaultSlotsPerWheel = 256;
    private const int DefaultLevels = 4;

    // A deadline plus the two links that thread the timer through its slot's intrusive doubly-linked list, in
    // one 24-byte record. The payload is deliberately not among them: a slot walk reads deadlines and links,
    // and touches TValue only for the timers it actually fires.
    private struct Entry
    {
        public long Deadline;

        // The absolute index into _buckets of the list this timer is on, or -1 when the slot is free. That
        // doubles as the liveness flag, so no separate occupancy bit is needed.
        public int Bucket;

        // Next timer on the same list, or the next free slot when Bucket is -1.
        public int Next;

        public int Prev;

        // Stepped every time the slot is vacated, which is what retires the handles that pointed at it. A
        // pending slot's version is always at least 1, so the default handle can never resolve.
        public uint Version;
    }

    private readonly int _slots;
    private readonly int _shift;
    private readonly int _mask;
    private readonly int _levels;
    private readonly long _horizon;

    // levels * slots wheel slots, plus one trailing list for timers that are already due — those scheduled
    // with a zero delay, which no wheel slot can hold because every wheel slot is strictly in the future.
    // Giving it a bucket index rather than its own field is what lets Link and Unlink stay uniform.
    private readonly int[] _buckets;
    private readonly int _dueBucket;

    private Entry[] _entries;
    private TValue?[] _values;

    // The high-water mark of allocated slots. Slots below it are either pending or on the free list; slots
    // above it have never been used, which is what lets a fresh one start at version 1.
    private int _slotCount;
    private int _freeHead;
    private int _count;
    private int _version;
    private long _now;

    // Set only while Advance is handing payloads to the caller's collection, which is the one place this type
    // runs code it does not own. A destination that calls back into the wheel from its Add would mutate the
    // buckets and the free list under a loop that is walking both.
    private bool _delivering;

    /// <summary>Creates an empty wheel whose clock starts at tick zero.</summary>
    /// <param name="slotsPerWheel">
    /// Slots in each level's wheel. Must be a power of two, at least two. Wider wheels cross fewer levels for
    /// the same horizon and cascade less, at one <see cref="int"/> per slot per level.
    /// </param>
    /// <param name="levels">
    /// How many wheels are stacked. Each multiplies <see cref="Horizon"/> by <paramref name="slotsPerWheel"/>
    /// and adds at most one cascade to a timer's life.
    /// </param>
    /// <param name="capacity">How many timers to make room for up front. Storage grows as needed.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="slotsPerWheel"/> is below two or not a power of two, <paramref name="levels"/> is below
    /// one, the two together would put <see cref="Horizon"/> beyond <c>2^62</c> or call for more slots than
    /// one array can hold — two independent limits, since a wide wheel reaches the second first — or
    /// <paramref name="capacity"/> is negative.
    /// </exception>
    /// <remarks>
    /// The defaults — 256 slots across 4 levels — give a 2^32-tick horizon, about 49 days at a millisecond
    /// tick, in one flat array of 1,025 slot heads: 4 KiB, whatever the wheel goes on to hold.
    /// </remarks>
    public TimerWheel(int slotsPerWheel = DefaultSlotsPerWheel, int levels = DefaultLevels, int capacity = 0)
    {
        if (slotsPerWheel < 2 || (slotsPerWheel & (slotsPerWheel - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slotsPerWheel), slotsPerWheel,
                "The number of slots per wheel must be a power of two and at least two.");
        }

        if (levels < 1)
            throw new ArgumentOutOfRangeException(nameof(levels), levels, "A wheel must have at least one level.");

        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must not be negative.");

        _slots = slotsPerWheel;
        _shift = BitOperations.TrailingZeroCount((uint)slotsPerWheel);
        _mask = slotsPerWheel - 1;
        _levels = levels;

        // Counted in the exponent rather than by multiplying, so an oversized request is rejected instead of
        // overflowing into a plausible-looking horizon.
        long totalShift = (long)_shift * levels;
        if (totalShift > 62)
        {
            throw new ArgumentOutOfRangeException(nameof(levels), levels,
                "The slot count and level count together call for a horizon beyond 2^62 ticks. Use fewer levels or a narrower wheel.");
        }

        _horizon = 1L << (int)totalShift;

        // Counted in long: the two checks above admit 2^30 slots across two levels, whose product overflows
        // an int and would reach the allocator as a negative length with an unrelated message.
        long buckets = (long)levels * slotsPerWheel;
        if (buckets >= Array.MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(slotsPerWheel), slotsPerWheel,
                "The slot count and level count together call for more slots than an array can hold. Use fewer levels or a narrower wheel.");
        }

        _dueBucket = (int)buckets;
        _buckets = new int[_dueBucket + 1];
        _buckets.AsSpan().Fill(-1);

        _entries = capacity == 0 ? Array.Empty<Entry>() : new Entry[capacity];
        _values = capacity == 0 ? Array.Empty<TValue?>() : new TValue?[capacity];
        _freeHead = -1;
    }

    /// <summary>Gets the number of timers that are scheduled and have neither fired nor been cancelled.</summary>
    public int Count => _count;

    /// <summary>Gets the tick the wheel's clock currently stands at. Starts at zero and never moves backwards.</summary>
    public long CurrentTick => _now;

    /// <summary>Gets the number of slots in each level's wheel, as passed to the constructor.</summary>
    public int SlotsPerWheel => _slots;

    /// <summary>Gets the number of stacked wheels, as passed to the constructor.</summary>
    public int Levels => _levels;

    /// <summary>
    /// Gets the exclusive upper bound on a schedulable delay — <c>slotsPerWheel^levels</c> ticks, the span the
    /// stacked wheels can address.
    /// </summary>
    public long Horizon => _horizon;

    // ---- scheduling ---------------------------------------------------------------------------------

    /// <summary>Schedules a timer to fire <paramref name="delayTicks"/> ticks from now.</summary>
    /// <param name="delayTicks">
    /// How far ahead of <see cref="CurrentTick"/> the timer is due. Zero means due now, which fires on the next
    /// <see cref="Advance(long, ICollection{TValue})"/> whatever tick it names.
    /// </param>
    /// <param name="value">The payload to carry. May be <c>null</c> for a reference type.</param>
    /// <returns>A handle that stays valid until the timer fires, is cancelled, or the wheel is cleared.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="delayTicks"/> is negative, at least <see cref="Horizon"/>, or would put the deadline
    /// past <see cref="long.MaxValue"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Called from the destination an <see cref="Advance(long, ICollection{TValue})"/> is delivering into.
    /// </exception>
    /// <remarks>
    /// Duplicate deadlines and duplicate payloads are kept distinct: two timers due at the same tick are two
    /// entries with two handles, and both fire. The type never compares <typeparamref name="TValue"/>, so it
    /// needs no equality contract on it.
    /// </remarks>
    public TimerHandle Schedule(long delayTicks, TValue? value)
    {
        ThrowIfDelivering();

        if ((ulong)delayTicks >= (ulong)_horizon)
        {
            throw new ArgumentOutOfRangeException(nameof(delayTicks), delayTicks,
                $"The delay must be at least zero and below the wheel's horizon of {_horizon} ticks.");
        }

        // The clock runs to long.MaxValue and no further, so the only delay this has to refuse is the one that
        // would land past it. Refusing it here rather than capping the clock is what keeps the promise that
        // every timer this accepts can be reached by some advance.
        if (delayTicks > long.MaxValue - _now)
        {
            throw new ArgumentOutOfRangeException(nameof(delayTicks), delayTicks,
                $"The deadline would pass long.MaxValue: the clock stands at {_now}.");
        }

        return ScheduleCore(_now + delayTicks, value);
    }

    /// <summary>Schedules a timer to fire at the absolute tick <paramref name="deadline"/>.</summary>
    /// <param name="deadline">
    /// The tick at which the timer is due, on the same clock as <see cref="CurrentTick"/>. A deadline equal to
    /// <see cref="CurrentTick"/> is due now; one in the past is rejected rather than silently fired.
    /// </param>
    /// <param name="value">The payload to carry. May be <c>null</c> for a reference type.</param>
    /// <returns>A handle that stays valid until the timer fires, is cancelled, or the wheel is cleared.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="deadline"/> precedes <see cref="CurrentTick"/>, or is <see cref="Horizon"/> ticks or
    /// more beyond it.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Called from the destination an <see cref="Advance(long, ICollection{TValue})"/> is delivering into.
    /// </exception>
    public TimerHandle ScheduleAt(long deadline, TValue? value)
    {
        ThrowIfDelivering();

        if (deadline < _now || deadline - _now >= _horizon)
        {
            throw new ArgumentOutOfRangeException(nameof(deadline), deadline,
                $"The deadline must be at or after the current tick ({_now}) and within the wheel's horizon of {_horizon} ticks.");
        }

        return ScheduleCore(deadline, value);
    }

    /// <summary>Cancels the timer addressed by <paramref name="handle"/> in constant time.</summary>
    /// <param name="handle">The handle returned when the timer was scheduled.</param>
    /// <returns>
    /// <c>true</c> when a pending timer was cancelled; <c>false</c> when the handle does not address one —
    /// because it already fired, was already cancelled, was retired by <see cref="Clear"/>, or is
    /// <c>default</c>.
    /// </returns>
    /// <remarks>
    /// A <c>false</c> return is the expected outcome of losing the race against the wheel's own clock, not an
    /// error, which is why this does not throw the way <see cref="SpatialGrid{TValue}.Remove"/> does. The
    /// payload is released immediately, so cancelling stops the wheel holding a reference to it.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Called from the destination an <see cref="Advance(long, ICollection{TValue})"/> is delivering into.
    /// </exception>
    public bool Cancel(TimerHandle handle)
    {
        ThrowIfDelivering();

        if (!TryResolve(handle, out int slot))
            return false;

        Unlink(slot);
        Vacate(slot);

        _count--;
        _version++;
        return true;
    }

    /// <summary>Reads back the deadline of the timer addressed by <paramref name="handle"/>.</summary>
    /// <param name="handle">The handle to resolve.</param>
    /// <param name="deadline">Receives the absolute deadline, or zero when the handle is not pending.</param>
    /// <returns><c>true</c> when the handle addresses a pending timer; otherwise <c>false</c>.</returns>
    /// <remarks>This is also the way to ask whether a timer is still pending, without cancelling it.</remarks>
    public bool TryGetDeadline(TimerHandle handle, out long deadline)
    {
        if (!TryResolve(handle, out int slot))
        {
            deadline = 0;
            return false;
        }

        deadline = _entries[slot].Deadline;
        return true;
    }

    // ---- time ---------------------------------------------------------------------------------------

    /// <summary>
    /// Moves the clock to <paramref name="tick"/> and appends the payload of every timer due at or before it
    /// to <paramref name="expired"/>.
    /// </summary>
    /// <param name="tick">
    /// The tick to advance to. Must be at or after <see cref="CurrentTick"/> — time does not run backwards.
    /// There is no upper limit short of <see cref="long.MaxValue"/>: it is <see cref="Schedule"/> that refuses
    /// a delay which would land past it, so that every timer the wheel accepts can be reached.
    /// </param>
    /// <param name="expired">
    /// Receives the payloads of the fired timers, appended in an unspecified order. Not cleared first, so one
    /// reused <see cref="List{T}"/> can collect several advances; reusing one is what makes a steady-state
    /// advance allocation-free.
    /// </param>
    /// <returns>How many timers fired.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expired"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="expired"/> is read-only.</exception>
    /// <exception cref="InvalidOperationException">
    /// Called from the destination an <see cref="Advance(long, ICollection{TValue})"/> is already delivering
    /// into.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="tick"/> precedes <see cref="CurrentTick"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Advancing to the tick the clock already stands at is legal and is not a no-op: it fires the timers
    /// scheduled with a zero delay since the last advance. Every fired timer's handle is retired and its
    /// payload released by the wheel.
    /// </para>
    /// <para>
    /// The cost is <c>O(levels &#215; slots + fired + cascaded)</c> — bounded by the wheel's geometry and the
    /// timers it moves, <i>not</i> by how far the clock jumped, and not by the timers it fires alone: the walk
    /// inspects the slots the move crossed whether or not anything is on them, which for the ordinary
    /// one-tick step is a single slot and for a jump past every level is <see cref="Levels"/> &#215;
    /// <see cref="SlotsPerWheel"/> of them.
    /// </para>
    /// <para>
    /// <b>A destination whose <c>Add</c> throws does not damage the wheel</b>, which is why handing the
    /// payloads over is the <i>last</i> thing this does rather than something interleaved with the slot walk.
    /// A timer that could not be delivered is still pending, still counted, still addressable by its handle,
    /// and delivered by the next advance — though not necessarily before the timers that advance makes due,
    /// since a batch has no promised order. The clock has still moved, because that part succeeded. A
    /// read-only destination is rejected outright, before the clock moves at all.
    /// </para>
    /// </remarks>
    public int Advance(long tick, ICollection<TValue?> expired)
    {
        ArgumentNullException.ThrowIfNull(expired);

        if (expired.IsReadOnly)
        {
            throw new ArgumentException(
                "The destination is read-only and cannot receive the fired timers.", nameof(expired));
        }

        return AdvanceCore(tick, expired);
    }

    /// <summary>
    /// Moves the clock to <paramref name="tick"/> and returns the payload of every timer due at or before it.
    /// </summary>
    /// <param name="tick">
    /// The tick to advance to. Must be at or after <see cref="CurrentTick"/>.
    /// </param>
    /// <returns>The payloads of the fired timers, in an unspecified order. Empty when nothing was due.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="tick"/> precedes <see cref="CurrentTick"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Called from the destination an <see cref="Advance(long, ICollection{TValue})"/> is already delivering
    /// into.
    /// </exception>
    /// <remarks>
    /// The convenience tier: it allocates a list per call. Pass a reused one to
    /// <see cref="Advance(long, ICollection{TValue})"/> on a hot loop.
    /// </remarks>
    public List<TValue?> Advance(long tick)
    {
        var expired = new List<TValue?>();
        AdvanceCore(tick, expired);
        return expired;
    }

    /// <summary>Cancels every pending timer, retiring every outstanding handle.</summary>
    /// <remarks>
    /// The storage is kept and returned to the free list, so refilling the wheel allocates nothing.
    /// <see cref="CurrentTick"/> is left where it stands: this empties the container, it does not rewind the
    /// clock. Clearing an already-empty wheel changes nothing and does not invalidate enumerators.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Called from the destination an <see cref="Advance(long, ICollection{TValue})"/> is delivering into.
    /// </exception>
    public void Clear()
    {
        ThrowIfDelivering();

        if (_count == 0)
            return;

        _buckets.AsSpan().Fill(-1);

        // Every slot goes back on the free list with its version stepped, which is what retires the handles.
        // Rebuilding the list rather than resetting _slotCount is deliberate: a slot reissued from scratch
        // would start at version 1 again and could collide with a handle the caller is still holding.
        for (int i = 0; i < _slotCount; i++)
        {
            // Only a *pending* slot needs its version stepped. Vacate already retired the free ones, and
            // stepping them again would churn a vacated slot's version on every unrelated Clear — walking it
            // back round to a value some long-retired handle still holds after far fewer operations than that
            // slot's own vacations would take.
            if (_entries[i].Bucket >= 0)
            {
                _entries[i].Bucket = -1;
                _entries[i].Version = NextVersion(_entries[i].Version);
            }

            _entries[i].Next = i - 1;
        }

        if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
            Array.Clear(_values, 0, _slotCount);

        _freeHead = _slotCount - 1;
        _count = 0;
        _version++;
    }

    /// <summary>Returns an enumerator over the pending timers, in an unspecified order.</summary>
    /// <returns>A struct enumerator that allocates nothing.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<ScheduledTimer<TValue>> IEnumerable<ScheduledTimer<TValue>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internals ----------------------------------------------------------------------------------

    private void ThrowIfDelivering()
    {
        if (_delivering)
            ThrowReentrant();
    }

    // Split out and never inlined so the guard at each call site is one predictable test rather than a
    // throw's worth of code in the caller's hot path.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowReentrant() =>
        throw new InvalidOperationException(
            "The wheel cannot be modified while it is delivering expired timers. Schedule and cancel after the advance returns.");

    private TimerHandle ScheduleCore(long deadline, TValue? value)
    {
        int slot = AllocateSlot();
        _entries[slot].Deadline = deadline;
        _values[slot] = value;
        Place(slot, deadline);

        _count++;
        _version++;
        return new TimerHandle(slot, _entries[slot].Version);
    }

    private int AdvanceCore(long tick, ICollection<TValue?> expired)
    {
        if (tick < _now)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), tick,
                $"The wheel's clock does not run backwards; it already stands at {_now}.");
        }

        ThrowIfDelivering();

        long previous = _now;

        // The new time is published before the walk, so a timer that has to be re-placed is placed against the
        // clock it will actually be measured by.
        _now = tick;

        int pending = -1;

        // Top level down: a cascade only ever moves a timer to a lower level, so walking downwards means a
        // timer never has to be reconsidered by a level already passed.
        for (int level = _levels - 1; level >= 0; level--)
        {
            int levelShift = level * _shift;
            long crossed = (tick >> levelShift) - (previous >> levelShift);
            if (crossed <= 0)
                continue;

            // More than a full revolution of this wheel means every slot on it has been reached. It cannot be
            // more than that, because a level-L slot only ever holds deadlines within one revolution of it.
            int visited = (int)Math.Min(crossed, _slots);
            long firstDigit = (tick >> levelShift) - visited + 1;
            int origin = level * _slots;

            for (int i = 0; i < visited; i++)
            {
                ref int head = ref _buckets[origin + (int)((firstDigit + i) & _mask)];
                int node = head;
                head = -1;

                while (node >= 0)
                {
                    int next = _entries[node].Next;
                    if (_entries[node].Deadline <= tick)
                    {
                        // Moved to the already-due list rather than handed over here. Delivery is the last
                        // step of the advance and touches one timer at a time, so a destination whose Add
                        // throws leaves every undelivered timer exactly where this put it — pending, counted,
                        // and delivered by the next advance — instead of stranded in a detached slot. Where in
                        // that next batch it lands is not promised: this prepends, so a newly due timer can go
                        // ahead of one an earlier advance could not deliver. A batch has no order to preserve.
                        Link(node, _dueBucket);
                    }
                    else
                    {
                        // Staged rather than re-placed on the spot: a lower level's slots have not been walked
                        // yet, and dropping a timer into one would have it reconsidered — and re-staged — for
                        // as long as the walk lasts.
                        _entries[node].Next = pending;
                        pending = node;
                    }

                    node = next;
                }
            }
        }

        while (pending >= 0)
        {
            int next = _entries[pending].Next;
            Place(pending, _entries[pending].Deadline);
            pending = next;
        }

        // The only caller code this method runs. Everything the wheel had to change is already consistent, and
        // each timer leaves the due list only once its payload has been accepted.
        int fired = 0;
        int due = _buckets[_dueBucket];

        _delivering = true;
        try
        {
            while (due >= 0)
            {
                expired.Add(_values[due]);

                int next = _entries[due].Next;
                _buckets[_dueBucket] = next;
                if (next >= 0)
                    _entries[next].Prev = -1;

                Vacate(due);
                _count--;

                // Stepped on the first timer actually removed, and before the loop can call back into the
                // caller again: a destination is allowed to *read* the wheel from its Add, and one holding an
                // enumerator taken before the advance must not walk a wheel this has already vacated from.
                if (fired == 0)
                    _version++;

                fired++;
                due = next;
            }
        }
        finally
        {
            _delivering = false;
        }

        return fired;
    }

    // Puts a slot on the list that will reach it: the due list when the deadline has already arrived,
    // otherwise the lowest level whose span covers the remaining delay, in the slot that delay's digit names.
    private void Place(int slot, long deadline)
    {
        long delta = deadline - _now;
        if (delta <= 0)
        {
            Link(slot, _dueBucket);
            return;
        }

        int level = 0;
        long span = _slots;
        while (delta >= span)
        {
            level++;
            span <<= _shift;
        }

        Link(slot, (level * _slots) + (int)((deadline >> (level * _shift)) & _mask));
    }

    private void Link(int slot, int bucket)
    {
        int head = _buckets[bucket];
        _entries[slot].Bucket = bucket;
        _entries[slot].Prev = -1;
        _entries[slot].Next = head;

        if (head >= 0)
            _entries[head].Prev = slot;

        _buckets[bucket] = slot;
    }

    private void Unlink(int slot)
    {
        int previous = _entries[slot].Prev;
        int next = _entries[slot].Next;

        if (previous >= 0)
            _entries[previous].Next = next;
        else
            _buckets[_entries[slot].Bucket] = next;

        if (next >= 0)
            _entries[next].Prev = previous;
    }

    private int AllocateSlot()
    {
        if (_freeHead >= 0)
        {
            int reused = _freeHead;
            _freeHead = _entries[reused].Next;
            return reused;
        }

        if (_slotCount == _entries.Length)
            Grow();

        int slot = _slotCount++;

        // A slot that has never been used starts at version 1, so that the default handle — index 0,
        // version 0 — cannot resolve to the first timer ever scheduled.
        _entries[slot].Version = 1;
        return slot;
    }

    private void Grow()
    {
        int current = _entries.Length;
        int capacity = ClampGrowth(current == 0 ? 4 : current * 2, current);

        Array.Resize(ref _entries, capacity);
        Array.Resize(ref _values, capacity);
    }

    // The growth ceiling, split out because neither arm is reachable from the public API. Grow() has exactly
    // one caller — AllocateSlot(), and only when the free list is empty and every slot is pending — so the
    // clamp needs 2^30 simultaneously pending timers. Each is a 24-byte record, which puts _entries past the
    // 2 GiB single-object array limit long before the count gets there, at any available memory.
    [ExcludeFromCodeCoverage(Justification = "Unreachable: needs 2^30 pending timers of 24 bytes each, which " +
        "exceeds the 2 GiB single-object array limit regardless of available memory.")]
    private static int ClampGrowth(int capacity, int current)
    {
        if ((uint)capacity > (uint)Array.MaxLength)
            capacity = Array.MaxLength;
        if (capacity <= current)
            throw new InvalidOperationException("The timer wheel has reached its maximum capacity.");

        return capacity;
    }

    // Steps a vacated slot's version, cycling through [1, uint.MaxValue] and never through 0. A plain
    // increment would eventually wrap to 0 — 2^32 vacations of one slot, which a tight schedule/fire loop
    // reaches in minutes rather than geological time — and a slot sitting at version 0 would be addressable by
    // the `default` handle.
    private static uint NextVersion(uint version) => (version % uint.MaxValue) + 1;

    private void Vacate(int slot)
    {
        _entries[slot].Bucket = -1;
        _entries[slot].Version = NextVersion(_entries[slot].Version);
        _entries[slot].Next = _freeHead;
        _freeHead = slot;

        if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
            _values[slot] = default;
    }

    private bool TryResolve(TimerHandle handle, out int slot)
    {
        slot = handle.Index;
        return (uint)slot < (uint)_slotCount
            && _entries[slot].Bucket >= 0
            && _entries[slot].Version == handle.Version;
    }

    /// <summary>Walks the pending timers of a <see cref="TimerWheel{TValue}"/> in an unspecified order.</summary>
    public struct Enumerator : IEnumerator<ScheduledTimer<TValue>>
    {
        private readonly TimerWheel<TValue> _wheel;
        private readonly int _version;
        private int _slot;
        private ScheduledTimer<TValue> _current;

        internal Enumerator(TimerWheel<TValue> wheel)
        {
            _wheel = wheel;
            _version = wheel._version;
            _slot = 0;
            _current = default;
        }

        /// <summary>Gets the timer at the current position of the enumerator.</summary>
        public readonly ScheduledTimer<TValue> Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next pending timer, skipping vacated slots.</summary>
        /// <returns><c>true</c> if there is a next timer; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The wheel was modified after the enumerator was created.</exception>
        public bool MoveNext()
        {
            if (_version != _wheel._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            while (_slot < _wheel._slotCount)
            {
                int slot = _slot++;
                if (_wheel._entries[slot].Bucket < 0)
                    continue;

                _current = new ScheduledTimer<TValue>(_wheel._entries[slot].Deadline, _wheel._values[slot]);
                return true;
            }

            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to before the first timer.</summary>
        /// <exception cref="InvalidOperationException">The wheel was modified after the enumerator was created.</exception>
        public void Reset()
        {
            if (_version != _wheel._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _slot = 0;
            _current = default;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
