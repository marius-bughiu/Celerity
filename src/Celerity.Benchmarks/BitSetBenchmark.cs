using System.Collections;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// BitSet vs System.Collections.BitArray. BitSet packs bits into 64-bit words and runs
// the bulk boolean operators over a Vector<ulong> when the hardware accelerates it,
// where BitArray walks 32-bit words; and it exposes a population count (Count) via a
// hardware popcount per word, which BitArray has no equivalent for — the baseline has
// to loop bit-by-bit. The PopCount, And, Or, and Xor categories show those two wins.
// Construction is identical for both (a per-bit pass over the same bool[]), so each
// And / Or / Xor category isolates the combine cost on top of that shared build.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BitSetBenchmark
{
    private bool[] valuesA = null!;
    private BitArray bitArrayA = null!;
    private BitArray bitArrayB = null!;
    private BitSet bitSetA = null!;
    private BitSet bitSetB = null!;

    // Named ItemCount (not BitCount) so the gh-pages dashboard's benchmark-name parser
    // picks up the parameter the same way it does for every other collection.
    [Params(1024, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        valuesA = new bool[ItemCount];
        var valuesB = new bool[ItemCount];

        var rand = new Random(42);
        for (int i = 0; i < ItemCount; i++)
        {
            valuesA[i] = rand.Next(2) == 0;
            valuesB[i] = rand.Next(2) == 0;
        }

        bitArrayA = new BitArray(valuesA);
        bitArrayB = new BitArray(valuesB);
        bitSetA = new BitSet(valuesA);
        bitSetB = new BitSet(valuesB);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PopCount")]
    public int BitArray_PopCount()
    {
        int count = 0;
        BitArray bits = bitArrayA;
        for (int i = 0; i < bits.Count; i++)
        {
            if (bits[i])
                count++;
        }
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("PopCount")]
    public int BitSet_PopCount() => bitSetA.Count;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("And")]
    public BitArray BitArray_And() => new BitArray(valuesA).And(bitArrayB);

    [Benchmark]
    [BenchmarkCategory("And")]
    public BitSet BitSet_And() => new BitSet(valuesA).And(bitSetB);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Or")]
    public BitArray BitArray_Or() => new BitArray(valuesA).Or(bitArrayB);

    [Benchmark]
    [BenchmarkCategory("Or")]
    public BitSet BitSet_Or() => new BitSet(valuesA).Or(bitSetB);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Xor")]
    public BitArray BitArray_Xor() => new BitArray(valuesA).Xor(bitArrayB);

    [Benchmark]
    [BenchmarkCategory("Xor")]
    public BitSet BitSet_Xor() => new BitSet(valuesA).Xor(bitSetB);
}
