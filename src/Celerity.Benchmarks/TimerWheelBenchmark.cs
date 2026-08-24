using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

/// <summary>
/// <see cref="TimerWheel{TValue}"/> against the two things a caller reaches for when they need a population of
/// pending deadlines: the BCL's <see cref="PriorityQueue{TElement, TPriority}"/> with the lazy-deletion
/// workaround its missing <c>Remove</c> forces, and
/// <see cref="IndexedPriorityQueue{TElement, TPriority, THasher}"/>, this library's addressable heap, which
/// cancels properly in <c>O(log n)</c>.
/// </summary>
/// <remarks>
/// <para>
/// The unit that matters is <c>Round</c>, and it is shaped by cancellation because the workload is: schedule
/// <c>ItemCount</c> timeouts at random delays, cancel nine in ten of them — the reply arrived, the lease was
/// renewed — then run the clock out and drain what survived. The other three groups take that round apart, so
/// a change can be attributed rather than guessed at.
/// </para>
/// <para>
/// The BCL arm is a lazy-deletion heap, which is not a strawman but the standard workaround: there is no
/// <c>Remove</c> on <see cref="PriorityQueue{TElement, TPriority}"/> at all on the <c>net8.0</c> floor, and the
/// one .NET 9 added is documented <c>O(n)</c>. So a cancel is a <see cref="HashSet{T}"/> insert — genuinely
/// cheap, and the reason the <c>Cancel</c> group is not the blowout one might expect — paid for at drain time,
/// where every cancelled timer still has to be popped and then discarded.
/// <see cref="IndexedPriorityQueue{TElement, TPriority, THasher}"/> is the strong baseline: it cancels for
/// real, and what it charges is <c>O(log n)</c> on both halves plus the index it keeps to find an element by
/// value.
/// </para>
/// <para>
/// Only the <see cref="PriorityQueue{TElement, TPriority}"/> arms carry the dashboard's op names; the
/// addressable-heap arms are suffixed (<c>RoundAddressable</c>, …) so each dashboard card still resolves to
/// one BCL measurement and one Celerity one, while the report keeps all three in the same category with ratios
/// against the same baseline.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class TimerWheelBenchmark
{
    // The clock the round runs out to. Delays are drawn from [1, Span], so the drain crosses the whole span
    // and every level of the wheel takes part rather than only the lowest.
    private const int Span = 10_000;

    // Nine in ten timeouts are cancelled before they fire, which is what a timeout population does and what
    // the type exists for.
    private const int CancelPercent = 90;

    private int[] delays = null!;
    private bool[] cancelled = null!;

    private TimerWheel<int> wheel = null!;
    private PriorityQueue<int, long> heap = null!;
    private HashSet<int> tombstones = null!;
    private IndexedPriorityQueue<int, long, Int32WangHasher> addressable = null!;
    private TimerHandle[] handles = null!;

    private List<int> expired = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        delays = new int[ItemCount];
        cancelled = new bool[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            delays[i] = rand.Next(1, Span + 1);
            cancelled[i] = rand.Next(100) < CancelPercent;
        }

        handles = new TimerHandle[ItemCount];
        expired = new List<int>(ItemCount);

        wheel = new TimerWheel<int>(capacity: ItemCount);
        heap = new PriorityQueue<int, long>(ItemCount);
        tombstones = new HashSet<int>(ItemCount);
        addressable = new IndexedPriorityQueue<int, long, Int32WangHasher>(ItemCount);
    }

    // ---- Round: schedule, cancel nine in ten, run the clock out --------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Round")]
    public int PriorityQueue_Round()
    {
        var queue = new PriorityQueue<int, long>(ItemCount);
        var dead = new HashSet<int>();

        for (int i = 0; i < ItemCount; i++)
            queue.Enqueue(i, delays[i]);

        for (int i = 0; i < ItemCount; i++)
        {
            if (cancelled[i])
                dead.Add(i);
        }

        int fired = 0;
        while (queue.TryDequeue(out int id, out _))
        {
            if (!dead.Remove(id))
                fired++;
        }

        return fired;
    }

    [Benchmark]
    [BenchmarkCategory("Round")]
    public int IndexedPriorityQueue_RoundAddressable()
    {
        var queue = new IndexedPriorityQueue<int, long, Int32WangHasher>(ItemCount);

        for (int i = 0; i < ItemCount; i++)
            queue.Enqueue(i, delays[i]);

        for (int i = 0; i < ItemCount; i++)
        {
            if (cancelled[i])
                queue.Remove(i);
        }

        int fired = 0;
        while (queue.TryDequeue(out _, out _))
            fired++;

        return fired;
    }

    [Benchmark]
    [BenchmarkCategory("Round")]
    public int TimerWheel_Round()
    {
        var timers = new TimerWheel<int>(capacity: ItemCount);
        var fired = new List<int>(ItemCount);

        // The handle array is allocated inside the round, not hoisted into the setup: it is this type's own
        // per-round bookkeeping, the counterpart of the tombstone set the heap arm allocates, and hoisting it
        // would charge the baseline for something it was not charged for.
        var issued = new TimerHandle[ItemCount];

        for (int i = 0; i < ItemCount; i++)
            issued[i] = timers.Schedule(delays[i], i);

        for (int i = 0; i < ItemCount; i++)
        {
            if (cancelled[i])
                timers.Cancel(issued[i]);
        }

        return timers.Advance(Span, fired);
    }

    // ---- Schedule ------------------------------------------------------------------------------------

    [IterationSetup(Target = nameof(PriorityQueue_Schedule))]
    public void SetupForHeapSchedule() => heap = new PriorityQueue<int, long>(ItemCount);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Schedule")]
    public void PriorityQueue_Schedule()
    {
        for (int i = 0; i < ItemCount; i++)
            heap.Enqueue(i, delays[i]);
    }

    [IterationSetup(Target = nameof(IndexedPriorityQueue_ScheduleAddressable))]
    public void SetupForAddressableSchedule() =>
        addressable = new IndexedPriorityQueue<int, long, Int32WangHasher>(ItemCount);

    [Benchmark]
    [BenchmarkCategory("Schedule")]
    public void IndexedPriorityQueue_ScheduleAddressable()
    {
        for (int i = 0; i < ItemCount; i++)
            addressable.Enqueue(i, delays[i]);
    }

    [IterationSetup(Target = nameof(TimerWheel_Schedule))]
    public void SetupForWheelSchedule() => wheel = new TimerWheel<int>(capacity: ItemCount);

    [Benchmark]
    [BenchmarkCategory("Schedule")]
    public void TimerWheel_Schedule()
    {
        for (int i = 0; i < ItemCount; i++)
            handles[i] = wheel.Schedule(delays[i], i);
    }

    // ---- Cancel --------------------------------------------------------------------------------------

    [IterationSetup(Target = nameof(PriorityQueue_Cancel))]
    public void SetupForHeapCancel() => tombstones = new HashSet<int>(ItemCount);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Cancel")]
    public int PriorityQueue_Cancel()
    {
        // Nothing is removed from the heap: the BCL type cannot, so the cancel is a tombstone and the cost
        // lands on the drain instead. Charging it here would be charging it twice.
        for (int i = 0; i < ItemCount; i++)
            tombstones.Add(i);

        return tombstones.Count;
    }

    [IterationSetup(Target = nameof(IndexedPriorityQueue_CancelAddressable))]
    public void SetupForAddressableCancel()
    {
        addressable = new IndexedPriorityQueue<int, long, Int32WangHasher>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            addressable.Enqueue(i, delays[i]);
    }

    [Benchmark]
    [BenchmarkCategory("Cancel")]
    public int IndexedPriorityQueue_CancelAddressable()
    {
        for (int i = 0; i < ItemCount; i++)
            addressable.Remove(i);

        return addressable.Count;
    }

    [IterationSetup(Target = nameof(TimerWheel_Cancel))]
    public void SetupForWheelCancel()
    {
        wheel = new TimerWheel<int>(capacity: ItemCount);
        for (int i = 0; i < ItemCount; i++)
            handles[i] = wheel.Schedule(delays[i], i);
    }

    [Benchmark]
    [BenchmarkCategory("Cancel")]
    public int TimerWheel_Cancel()
    {
        for (int i = 0; i < ItemCount; i++)
            wheel.Cancel(handles[i]);

        return wheel.Count;
    }

    // ---- Drain: no cancellation at all, which is the wheel's worst case ------------------------------

    [IterationSetup(Target = nameof(PriorityQueue_Drain))]
    public void SetupForHeapDrain()
    {
        heap = new PriorityQueue<int, long>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            heap.Enqueue(i, delays[i]);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Drain")]
    public int PriorityQueue_Drain()
    {
        int fired = 0;
        while (heap.TryDequeue(out _, out _))
            fired++;

        return fired;
    }

    [IterationSetup(Target = nameof(IndexedPriorityQueue_DrainAddressable))]
    public void SetupForAddressableDrain()
    {
        addressable = new IndexedPriorityQueue<int, long, Int32WangHasher>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            addressable.Enqueue(i, delays[i]);
    }

    [Benchmark]
    [BenchmarkCategory("Drain")]
    public int IndexedPriorityQueue_DrainAddressable()
    {
        int fired = 0;
        while (addressable.TryDequeue(out _, out _))
            fired++;

        return fired;
    }

    [IterationSetup(Target = nameof(TimerWheel_Drain))]
    public void SetupForWheelDrain()
    {
        wheel = new TimerWheel<int>(capacity: ItemCount);
        for (int i = 0; i < ItemCount; i++)
            wheel.Schedule(delays[i], i);

        expired.Clear();
    }

    [Benchmark]
    [BenchmarkCategory("Drain")]
    public int TimerWheel_Drain() => wheel.Advance(Span, expired);

    // ---- Tick: the clock driven one tick at a time, which is how an event loop drives it --------------

    [IterationSetup(Target = nameof(PriorityQueue_Tick))]
    public void SetupForHeapTick()
    {
        heap = new PriorityQueue<int, long>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            heap.Enqueue(i, delays[i]);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Tick")]
    public int PriorityQueue_Tick()
    {
        int fired = 0;
        for (long now = 1; now <= Span; now++)
        {
            while (heap.TryPeek(out _, out long deadline) && deadline <= now)
            {
                heap.Dequeue();
                fired++;
            }
        }

        return fired;
    }

    [IterationSetup(Target = nameof(IndexedPriorityQueue_TickAddressable))]
    public void SetupForAddressableTick()
    {
        addressable = new IndexedPriorityQueue<int, long, Int32WangHasher>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            addressable.Enqueue(i, delays[i]);
    }

    [Benchmark]
    [BenchmarkCategory("Tick")]
    public int IndexedPriorityQueue_TickAddressable()
    {
        int fired = 0;
        for (long now = 1; now <= Span; now++)
        {
            while (addressable.TryPeek(out _, out long deadline) && deadline <= now)
            {
                addressable.Dequeue();
                fired++;
            }
        }

        return fired;
    }

    [IterationSetup(Target = nameof(TimerWheel_Tick))]
    public void SetupForWheelTick()
    {
        wheel = new TimerWheel<int>(capacity: ItemCount);
        for (int i = 0; i < ItemCount; i++)
            wheel.Schedule(delays[i], i);

        expired.Clear();
    }

    /// <summary>
    /// The honest counterpart to <see cref="TimerWheel_Drain"/>: ten thousand advances of one tick each rather
    /// than one jump, so the wheel pays a slot read per tick whether or not anything is on it, and the heap
    /// pays a peek per tick. This is the arm that charges the wheel for the sweep its bound is stated in.
    /// </summary>
    [Benchmark]
    [BenchmarkCategory("Tick")]
    public int TimerWheel_Tick()
    {
        int fired = 0;
        for (long now = 1; now <= Span; now++)
            fired += wheel.Advance(now, expired);

        return fired;
    }
}
