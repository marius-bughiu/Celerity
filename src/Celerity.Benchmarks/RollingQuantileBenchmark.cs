using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Statistics;

// RollingQuantile<T> vs the rolling median a caller writes instead. The BCL has no windowed quantile, so the
// baseline is the hand-roll a careful developer reaches for: the window kept sorted in an int[], a
// Queue<int> remembering arrival order, and per sample two Array.BinarySearch calls (the expiring value's
// index, the new value's insertion point) and ONE span copy that shifts only the values between the two
// positions. That is O(log w) to find and O(|i − j|) to move, and the move is a memmove, so it is fast for
// a small window. The more obvious List<int>.RemoveAt + Insert moves the tail twice, about three times as
// much memory per sample, and re-sorting the window per read is far worse again; either would flatter the
// type for no reason, so neither is the baseline.
//
// One op:
//
//   Slide — the steady state: the window (ItemCount values) is already full, and each invocation pushes
//           Steps more samples through it, reading the median after every one. Both sides are filled once
//           in GlobalSetup and keep sliding across invocations; a full window stays full, so every
//           invocation does the same work. The samples are uniform over a range wide enough that ties are
//           rare, which is the shape a latency stream has.
//
// ItemCount is the window size, which is the axis the comparison turns on: the sorted list's memmove grows
// with it and the heaps' depth grows with its logarithm.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RollingQuantileBenchmark
{
    private const int Steps = 1_000;
    private const double Median = 0.5d;

    private int[] samples = null!;
    private int cursor;

    private RollingQuantile<int> rolling = null!;
    private int[] sorted = null!;
    private Queue<int> arrival = null!;
    private int sortedCursor;

    [Params(1_000, 10_000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        samples = new int[ItemCount + (Steps * 64)];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = random.Next(1_000_000_000);
        }

        rolling = new RollingQuantile<int>(ItemCount, Median);
        arrival = new Queue<int>(ItemCount);
        sorted = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            int value = samples[i];
            rolling.Add(value);
            arrival.Enqueue(value);
            sorted[i] = value;
        }

        Array.Sort(sorted);
        cursor = ItemCount;
        sortedCursor = ItemCount;
    }

    [BenchmarkCategory("Slide")]
    [Benchmark(Baseline = true)]
    public long Array_Slide()
    {
        int rank = (int)(Median * (ItemCount - 1));
        long total = 0;
        for (int i = 0; i < Steps; i++)
        {
            int value = samples[sortedCursor];
            sortedCursor = sortedCursor + 1 == samples.Length ? ItemCount : sortedCursor + 1;

            int old = Array.BinarySearch(sorted, arrival.Dequeue());
            int at = Array.BinarySearch(sorted, value);
            at = at < 0 ? ~at : at;
            if (at > old)
            {
                // The new value lands above the old one: the values between slide down one place.
                sorted.AsSpan(old + 1, at - old - 1).CopyTo(sorted.AsSpan(old));
                sorted[at - 1] = value;
            }
            else
            {
                // At or below: the values between slide up one place, over the old value.
                sorted.AsSpan(at, old - at).CopyTo(sorted.AsSpan(at + 1));
                sorted[at] = value;
            }

            arrival.Enqueue(value);

            total += sorted[rank];
        }

        return total;
    }

    [BenchmarkCategory("Slide")]
    [Benchmark]
    public long RollingQuantile_Slide()
    {
        long total = 0;
        for (int i = 0; i < Steps; i++)
        {
            int value = samples[cursor];
            cursor = cursor + 1 == samples.Length ? ItemCount : cursor + 1;

            rolling.Add(value);
            total += rolling.Value;
        }

        return total;
    }
}
