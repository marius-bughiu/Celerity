using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// RankSelectBitVector vs the loop a caller writes by hand. There is no BCL counterpart at all — BitArray has
// neither rank nor select, BitOperations.PopCount is per-word, and .NET 8/9/10 ship no succinct data-structure
// support — so the honest baseline is the naive popcount walk over the same packed ulong[], marked Baseline in
// every category and named Array_* so the dashboard classifies it as the reference arm.
//
// The RankEarly / RankMid / RankLate categories sweep the query position, because the baseline's cost is
// O(index / 64) while the indexed answer is two loads and one masked popcount regardless: the gap should be
// nearly absent at the front of the vector and widest at the back. Select is the O(log n) side. Build is the
// price of the index — the row that keeps the tradeoff honest, since it is pure overhead a caller only recovers
// by querying many times. The index also costs 25% of the vector in space (IndexSizeInBytes), which a timing
// benchmark cannot show; the docs carry that number.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RankSelectBitVectorBenchmark
{
    private const int QueryCount = 1000;

    private ulong[] words = null!;
    private RankSelectBitVector vector = null!;
    private int[] earlyPositions = null!;
    private int[] midPositions = null!;
    private int[] latePositions = null!;
    private int[] ranks = null!;

    // Named ItemCount (not BitCount) so the gh-pages dashboard's benchmark-name parser picks up the parameter
    // the same way it does for every other collection. The sweep is bit length, matching BitSetBenchmark.
    [Params(1024, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        // A half-dense vector: sparse enough that Select has to walk, dense enough that every superblock is
        // populated, so neither side of the comparison gets an unrepresentative shortcut.
        words = new ulong[(ItemCount + 63) / 64];
        for (int i = 0; i < words.Length; i++)
            words[i] = ((ulong)(uint)rand.Next() << 32) | (uint)rand.Next();

        int tail = ItemCount & 63;
        if (tail != 0)
            words[words.Length - 1] &= (1UL << tail) - 1;

        vector = new RankSelectBitVector(ItemCount, words);

        earlyPositions = RandomPositions(rand, 0, Math.Max(1, ItemCount / 100));
        midPositions = RandomPositions(rand, ItemCount / 2, Math.Max(1, ItemCount / 100));
        latePositions = RandomPositions(rand, Math.Max(0, ItemCount - Math.Max(1, ItemCount / 100)), Math.Max(1, ItemCount / 100));

        ranks = new int[QueryCount];
        for (int i = 0; i < QueryCount; i++)
            ranks[i] = rand.Next(vector.Count);
    }

    private static int[] RandomPositions(Random rand, int origin, int span)
    {
        int[] positions = new int[QueryCount];
        for (int i = 0; i < positions.Length; i++)
            positions[i] = origin + rand.Next(span);
        return positions;
    }

    // The loop a caller writes without a rank index: popcount every whole word below the position, then mask the
    // partial one. O(index / 64), which is exactly the cost the index exists to erase.
    private static int NaiveRank(ulong[] words, int index)
    {
        int count = 0;
        int fullWords = index >> 6;
        for (int w = 0; w < fullWords; w++)
            count += BitOperations.PopCount(words[w]);

        int rem = index & 63;
        if (rem != 0)
            count += BitOperations.PopCount(words[fullWords] & ((1UL << rem) - 1));

        return count;
    }

    // The matching hand-rolled select: accumulate whole-word popcounts until the target word is reached, then
    // clear low set bits one at a time to land on the right one.
    private static int NaiveSelect(ulong[] words, int rank)
    {
        for (int w = 0; w < words.Length; w++)
        {
            int inWord = BitOperations.PopCount(words[w]);
            if (inWord <= rank)
            {
                rank -= inWord;
                continue;
            }

            ulong remaining = words[w];
            while (rank-- > 0)
                remaining &= remaining - 1;

            return (w << 6) + BitOperations.TrailingZeroCount(remaining);
        }

        return -1;
    }

    // ---- RankEarly: positions in the first 1% of the vector, where the naive loop has almost nothing to do ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RankEarly")]
    public int Array_RankEarly() => SumNaiveRanks(earlyPositions);

    [Benchmark]
    [BenchmarkCategory("RankEarly")]
    public int RankSelectBitVector_RankEarly() => SumRanks(earlyPositions);

    // ---- RankMid: positions around the midpoint — the naive loop walks half the vector ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RankMid")]
    public int Array_RankMid() => SumNaiveRanks(midPositions);

    [Benchmark]
    [BenchmarkCategory("RankMid")]
    public int RankSelectBitVector_RankMid() => SumRanks(midPositions);

    // ---- RankLate: positions in the last 1% — the naive loop's worst case, the index's unchanged case ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RankLate")]
    public int Array_RankLate() => SumNaiveRanks(latePositions);

    [Benchmark]
    [BenchmarkCategory("RankLate")]
    public int RankSelectBitVector_RankLate() => SumRanks(latePositions);

    // ---- Select: locate the k-th set bit for random k ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Select")]
    public int Array_Select()
    {
        int sink = 0;
        for (int i = 0; i < ranks.Length; i++)
            sink += NaiveSelect(words, ranks[i]);
        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("Select")]
    public int RankSelectBitVector_Select()
    {
        int sink = 0;
        for (int i = 0; i < ranks.Length; i++)
            sink += vector.Select(ranks[i]);
        return sink;
    }

    // ---- Build: the index construction the queries above amortize, against holding the raw words alone ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public ulong[] Array_Build() => (ulong[])words.Clone();

    [Benchmark]
    [BenchmarkCategory("Build")]
    public RankSelectBitVector RankSelectBitVector_Build() => new(ItemCount, words);

    private int SumNaiveRanks(int[] positions)
    {
        int sink = 0;
        for (int i = 0; i < positions.Length; i++)
            sink += NaiveRank(words, positions[i]);
        return sink;
    }

    private int SumRanks(int[] positions)
    {
        int sink = 0;
        for (int i = 0; i < positions.Length; i++)
            sink += vector.Rank(positions[i]);
        return sink;
    }
}
