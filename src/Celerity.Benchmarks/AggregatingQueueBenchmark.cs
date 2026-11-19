using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// AggregatingQueue<int, TMonoid> vs the rolling aggregate a caller writes instead. A fold with no inverse cannot
// be kept as a running total (there is no "un-max" or "un-or"), so the BCL answer is a Queue<int> holding the
// window and a loop that re-folds it after every step: O(w) per read. That loop is the charted baseline. It
// iterates the queue with its struct enumerator, which is as cheap as a Queue<T> walk gets.
//
// Two charted ops, both in the steady state: the window (ItemCount values) is already full, and each invocation
// pushes Steps more samples through it, reading the aggregate after every one. Both sides are filled once in
// GlobalSetup and keep sliding across invocations, so every invocation does the same work. The samples rotate
// through a buffer of ItemCount + 64 * Steps values, so the branch predictor never sees the same short stream twice.
//
//   Slide   — a sliding-window maximum (MaxMonoid).
//   SlideOr — a sliding-window bitwise OR (BitwiseOrMonoid) over flag words with one to three bits set: the fold
//             the monotonic-deque trick does not cover.
//
// Three uncharted arms answer "what would a careful developer use instead", and each has an op name
// of its own so the dashboard (which keys one BCL and one Celerity value per card) does not overwrite the charted
// Celerity series with them:
//
//   MonotonicDeque_CrossSlideMax — the hand-written monotonic deque, specific to min / max. The type's docs say it
//                                  is the tighter structure for that fold; this arm is the measurement behind it.
//   SegmentTree_CrossSlideMax    — a SegmentTree over a ring of ItemCount slots, the in-library way to the same
//                                  answer for a fixed-length window: O(log w) per step.
//   BitCounts_CrossSlideOr       — the hand-roll for a window OR: a count per bit of the window's words that set
//                                  it, plus the mask of bits with a non-zero count. Per-bit counts are an inverse,
//                                  so this is a running total after all; each step costs one update per set bit of
//                                  the word entering and the word leaving. It is the OR analogue of the monotonic
//                                  deque, and on sparse flag words it is the faster of the two.
//
// ItemCount is the window size, the axis the comparison turns on: the re-fold grows linearly with it and the
// segment tree with its logarithm, while the queue does a constant number of combines per step.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class AggregatingQueueBenchmark
{
    private const int Steps = 1_000;

    private int[] samples = null!;
    private int[] flagSamples = null!;

    private Queue<int> maxWindow = null!;
    private AggregatingQueue<int, MaxMonoid<int>> maxQueue = null!;
    private int maxBaselineCursor;
    private int maxCursor;

    private Queue<int> orWindow = null!;
    private AggregatingQueue<int, BitwiseOrMonoid<int>> orQueue = null!;
    private int orBaselineCursor;
    private int orCursor;

    // Monotonic deque: a power-of-two ring of (position, value), values strictly decreasing from head to tail.
    private int[] monoIndex = null!;
    private int[] monoValue = null!;
    private int monoMask;
    private int monoHead;
    private int monoTail;
    private int monoPosition;
    private int monoCursor;

    // Per-bit counts for the OR hand-roll, the window's words in a ring, and the mask of bits with a count.
    private int[] bitCounts = null!;
    private int[] bitWindow = null!;
    private int bitSlot;
    private int bitMask;
    private int bitCursor;

    private SegmentTree<int, MaxMonoid<int>> ring = null!;
    private int ringSlot;
    private int ringCursor;

    [Params(1_000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        samples = new int[ItemCount + (Steps * 64)];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = random.Next(1_000_000_000);

        // Flag words carry one to three set bits each, the shape an error or capability mask has. Sparse words are
        // the per-bit hand-roll's best case: its updates run once per set bit.
        flagSamples = new int[samples.Length];
        for (int i = 0; i < flagSamples.Length; i++)
        {
            int flags = 0;
            for (int b = random.Next(1, 4); b > 0; b--)
                flags |= 1 << random.Next(32);
            flagSamples[i] = flags;
        }

        maxWindow = new Queue<int>(ItemCount);
        maxQueue = new AggregatingQueue<int, MaxMonoid<int>>(ItemCount);
        orWindow = new Queue<int>(ItemCount);
        orQueue = new AggregatingQueue<int, BitwiseOrMonoid<int>>(ItemCount);

        int capacity = 1;
        while (capacity < ItemCount + 1)
            capacity <<= 1;
        monoIndex = new int[capacity];
        monoValue = new int[capacity];
        monoMask = capacity - 1;

        ring = new SegmentTree<int, MaxMonoid<int>>(ItemCount);

        bitCounts = new int[32];
        bitWindow = new int[ItemCount];

        for (int i = 0; i < ItemCount; i++)
        {
            int value = samples[i];
            maxWindow.Enqueue(value);
            maxQueue.Enqueue(value);
            MonotonicPush(value);
            ring[i] = value;

            int flags = flagSamples[i];
            orWindow.Enqueue(flags);
            orQueue.Enqueue(flags);
            bitWindow[i] = flags;
            AddBits(flags);
        }

        maxBaselineCursor = maxCursor = orBaselineCursor = orCursor = monoCursor = ringCursor = bitCursor = ItemCount;
        ringSlot = 0;
    }

    // ---- Slide: sliding-window maximum ------------------------------------------------------------

    [BenchmarkCategory("Slide")]
    [Benchmark(Baseline = true)]
    public long Queue_Slide()
    {
        long total = 0;
        for (int i = 0; i < Steps; i++)
        {
            maxWindow.Dequeue();
            maxWindow.Enqueue(Next(samples, ref maxBaselineCursor));

            int max = int.MinValue;
            foreach (int value in maxWindow)
            {
                if (value > max)
                    max = value;
            }

            total += max;
        }

        return total;
    }

    [BenchmarkCategory("Slide")]
    [Benchmark]
    public long AggregatingQueue_Slide()
    {
        long total = 0;
        for (int i = 0; i < Steps; i++)
        {
            maxQueue.Dequeue();
            maxQueue.Enqueue(Next(samples, ref maxCursor));
            total += maxQueue.Aggregate;
        }

        return total;
    }

    [BenchmarkCategory("Slide")]
    [Benchmark]
    public long MonotonicDeque_CrossSlideMax()
    {
        long total = 0;
        for (int i = 0; i < Steps; i++)
        {
            MonotonicPush(Next(samples, ref monoCursor));
            // Written as a difference so it stays right when the position counter wraps over a long run.
            if (monoPosition - 1 - monoIndex[monoHead & monoMask] >= ItemCount)
                monoHead++;

            total += monoValue[monoHead & monoMask];
        }

        return total;
    }

    [BenchmarkCategory("Slide")]
    [Benchmark]
    public long SegmentTree_CrossSlideMax()
    {
        long total = 0;
        for (int i = 0; i < Steps; i++)
        {
            ring[ringSlot] = Next(samples, ref ringCursor);
            ringSlot = ringSlot + 1 == ItemCount ? 0 : ringSlot + 1;
            total += ring.Aggregate;
        }

        return total;
    }

    // ---- SlideOr: sliding-window bitwise OR -------------------------------------------------------

    [BenchmarkCategory("SlideOr")]
    [Benchmark(Baseline = true)]
    public long Queue_SlideOr()
    {
        long total = 0;
        for (int i = 0; i < Steps; i++)
        {
            orWindow.Dequeue();
            orWindow.Enqueue(Next(flagSamples, ref orBaselineCursor));

            int flags = 0;
            foreach (int value in orWindow)
                flags |= value;

            total += flags;
        }

        return total;
    }

    [BenchmarkCategory("SlideOr")]
    [Benchmark]
    public long AggregatingQueue_SlideOr()
    {
        long total = 0;
        for (int i = 0; i < Steps; i++)
        {
            orQueue.Dequeue();
            orQueue.Enqueue(Next(flagSamples, ref orCursor));
            total += orQueue.Aggregate;
        }

        return total;
    }

    [BenchmarkCategory("SlideOr")]
    [Benchmark]
    public long BitCounts_CrossSlideOr()
    {
        long total = 0;
        for (int i = 0; i < Steps; i++)
        {
            int incoming = Next(flagSamples, ref bitCursor);
            RemoveBits(bitWindow[bitSlot]);
            bitWindow[bitSlot] = incoming;
            bitSlot = bitSlot + 1 == ItemCount ? 0 : bitSlot + 1;
            AddBits(incoming);

            total += bitMask;
        }

        return total;
    }

    // ---- helpers -----------------------------------------------------------------------------------

    private int Next(int[] source, ref int cursor)
    {
        int value = source[cursor];
        cursor = cursor + 1 == source.Length ? ItemCount : cursor + 1;
        return value;
    }

    // Every bit the entering word sets is in the window now, so the mask takes the word whole, with no branch.
    private void AddBits(int value)
    {
        uint bits = (uint)value;
        while (bits != 0)
        {
            bitCounts[System.Numerics.BitOperations.TrailingZeroCount(bits)]++;
            bits &= bits - 1;
        }

        bitMask |= value;
    }

    private void RemoveBits(int value)
    {
        uint bits = (uint)value;
        while (bits != 0)
        {
            int bit = System.Numerics.BitOperations.TrailingZeroCount(bits);
            if (--bitCounts[bit] == 0)
                bitMask &= ~(1 << bit);
            bits &= bits - 1;
        }
    }

    // Pops every tail entry the new value dominates, then appends it. The head is evicted by the caller once it
    // falls out of the window.
    private void MonotonicPush(int value)
    {
        while (monoTail != monoHead && monoValue[(monoTail - 1) & monoMask] <= value)
            monoTail--;

        monoIndex[monoTail & monoMask] = monoPosition;
        monoValue[monoTail & monoMask] = value;
        monoTail++;
        monoPosition++;
    }
}
