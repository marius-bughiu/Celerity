using Celerity.Sorting;

namespace Celerity.Tests.Sorting;

/// <summary>
/// Reconciles both <see cref="KWayMerge"/> forms against a stable LINQ <c>OrderBy</c> over the
/// concatenated runs, on seeded pseudo-random inputs. Elements carry a tag that the comparer
/// ignores, so the oracle checks stability as well as order: the concatenation lists every run in
/// turn, and a stable sort of it is exactly the stable merge.
/// </summary>
/// <remarks>
/// Each case is a pure function of its seed, so a failure reproduces exactly. Run counts span the
/// single-run copy, the two-way merge, the stack-held tree and the pooled one past 128 runs; run
/// lengths include empty runs and one long run among short ones; the key domain is narrow, so
/// cross-run ties are the common case rather than the exception.
/// </remarks>
public class KWayMergeDifferentialTests
{
    private readonly struct ByKey : IComparer<(int Key, int Tag)>
    {
        public int Compare((int Key, int Tag) x, (int Key, int Tag) y) => x.Key.CompareTo(y.Key);
    }

    public static TheoryData<int, int> Cases
    {
        get
        {
            var data = new TheoryData<int, int>();
            foreach (int runCount in new[] { 1, 2, 3, 4, 7, 16, 33, 128, 129, 400 })
            {
                foreach (int seed in new[] { 1, 2, 3 })
                {
                    data.Add(runCount, seed);
                }
            }

            return data;
        }
    }

    private static (int Key, int Tag)[][] RandomRuns(int runCount, int seed)
    {
        var rand = new Random(seed * 7919 + runCount);
        int keyRange = rand.Next(2, 50);
        var runs = new (int Key, int Tag)[runCount][];
        int tag = 0;
        for (int r = 0; r < runCount; r++)
        {
            // Mostly short runs, about one in six empty, and now and then one far longer than the
            // rest, so the tree sees runs retire at very different times.
            int length = rand.Next(6) == 0 ? 0 : rand.Next(10) == 0 ? rand.Next(100, 300) : rand.Next(1, 12);
            var run = new (int Key, int Tag)[length];
            for (int i = 0; i < length; i++)
            {
                run[i] = (rand.Next(keyRange), tag++);
            }

            // Sort each run stably by key alone, so tags still increase within a run's equal keys.
            runs[r] = run.OrderBy(e => e.Key).ToArray();
        }

        return runs;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Merge_ShouldMatchAStableSortOfTheConcatenation(int runCount, int seed)
    {
        var runs = RandomRuns(runCount, seed);
        var source = runs.SelectMany(r => r).ToArray();
        var destination = new (int Key, int Tag)[source.Length];

        KWayMerge.Merge(source, runs.Select(r => r.Length).ToArray(), destination.AsSpan(), default(ByKey));

        Assert.Equal(source.OrderBy(e => e.Key), destination);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void LazyMerge_ShouldMatchAStableSortOfTheConcatenation(int runCount, int seed)
    {
        var runs = RandomRuns(runCount, seed);

        Assert.Equal(runs.SelectMany(r => r).OrderBy(e => e.Key), KWayMerge.Merge(runs, default(ByKey)));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Merge_ShouldMatchArraySort_WhenKeysAreFullWidthIntegers(int runCount, int seed)
    {
        var rand = new Random(seed);
        int[][] runs = new int[runCount][];
        for (int r = 0; r < runCount; r++)
        {
            runs[r] = new int[rand.Next(0, 40)];
            for (int i = 0; i < runs[r].Length; i++)
            {
                runs[r][i] = rand.Next(int.MinValue, int.MaxValue);
            }

            Array.Sort(runs[r]);
        }

        int[] source = runs.SelectMany(r => r).ToArray();
        int[] expected = (int[])source.Clone();
        Array.Sort(expected);
        int[] destination = new int[source.Length];

        KWayMerge.Merge<int>(source, runs.Select(r => r.Length).ToArray(), destination);

        Assert.Equal(expected, destination);
        Assert.Equal(expected, KWayMerge.Merge(runs));
    }
}
