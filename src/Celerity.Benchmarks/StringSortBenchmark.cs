using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Sorting;

// StringSort vs Array.Sort(keys, StringComparer.Ordinal), the best a caller can do with the BCL for
// an ordinal string sort. The default Array.Sort(string[]) is culture-aware and several times slower
// again, so measuring against it would flatter the result; ordinal against ordinal is the honest
// comparison.
//
// Both arms copy the pristine keys into a working buffer inside the measured method, so neither needs
// an [IterationSetup] and the O(n) copy is charged identically to both. StringSort rents its working
// state above 256 elements; ArrayPool returns the same array on every call after the first, so the
// Allocated column reads near zero, and that is the steady state a caller sees too.
//
// The categories are data shapes, and two of them are published weak spots:
//   Random       — 8–20 random alphanumerics; prefixes diverge almost at once.
//   SharedPrefix — "tenant:42:user:%09d" keys; the win workload, since the first fifteen characters are
//                  what a comparison sort re-reads on every comparison.
//   Duplicates   — 100 distinct random strings repeated; a run of equal strings must be read to its end.
//   Presorted    — SharedPrefix keys already in order. StringSort detects this with one linear scan
//                  and returns, so this row is a large win and says nothing about the sort itself.
//   NearlySorted — the same keys with the last two swapped: the worst case for that scan, which runs to
//                  the end, finds the inversion and then sorts in full, while introsort stays cheap on
//                  almost-ordered input. This is the published weak spot.
//   Pairs        — Random keys carrying an int payload, against the two-array Array.Sort.
//   ArgSort      — Random keys ranked into an index permutation; the BCL has no argsort, so its arm
//                  copies the keys, fills an identity array and runs the two-array Array.Sort.
// ItemCount is the parameter name the gh-pages dashboard's benchmark-name parser requires.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class StringSortBenchmark
{
    private string[] random = null!;
    private string[] sharedPrefix = null!;
    private string[] duplicates = null!;
    private string[] presorted = null!;
    private string[] nearlySorted = null!;
    private int[] payload = null!;
    private string[] keys = null!;
    private int[] values = null!;
    private int[] indices = null!;

    [Params(100, 1_000, 100_000, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        random = new string[ItemCount];
        sharedPrefix = new string[ItemCount];
        duplicates = new string[ItemCount];
        payload = new int[ItemCount];

        string[] distinct = new string[100];
        for (int i = 0; i < distinct.Length; i++)
        {
            distinct[i] = RandomAlphanumeric(rand);
        }

        for (int i = 0; i < ItemCount; i++)
        {
            random[i] = RandomAlphanumeric(rand);
            sharedPrefix[i] = "tenant:42:user:" + rand.Next(ItemCount * 4).ToString("D9");
            duplicates[i] = distinct[rand.Next(distinct.Length)];
            payload[i] = i;
        }

        presorted = (string[])sharedPrefix.Clone();
        Array.Sort(presorted, StringComparer.Ordinal);
        nearlySorted = (string[])presorted.Clone();
        (nearlySorted[^1], nearlySorted[^2]) = (nearlySorted[^2], nearlySorted[^1]);

        keys = new string[ItemCount];
        values = new int[ItemCount];
        indices = new int[ItemCount];
    }

    private static string RandomAlphanumeric(Random rand)
    {
        const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        char[] chars = new char[rand.Next(8, 21)];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[rand.Next(Alphabet.Length)];
        }

        return new string(chars);
    }

    private string[] ArrayOrdinal(string[] source)
    {
        source.CopyTo(keys, 0);
        Array.Sort(keys, StringComparer.Ordinal);
        return keys;
    }

    private string[] StringSortOrdinal(string[] source)
    {
        source.CopyTo(keys, 0);
        StringSort.Sort(keys.AsSpan());
        return keys;
    }

    // ---- Keys only, by data shape ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Random")]
    public string[] Array_Random() => ArrayOrdinal(random);

    [Benchmark]
    [BenchmarkCategory("Random")]
    public string[] StringSort_Random() => StringSortOrdinal(random);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SharedPrefix")]
    public string[] Array_SharedPrefix() => ArrayOrdinal(sharedPrefix);

    [Benchmark]
    [BenchmarkCategory("SharedPrefix")]
    public string[] StringSort_SharedPrefix() => StringSortOrdinal(sharedPrefix);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Duplicates")]
    public string[] Array_Duplicates() => ArrayOrdinal(duplicates);

    [Benchmark]
    [BenchmarkCategory("Duplicates")]
    public string[] StringSort_Duplicates() => StringSortOrdinal(duplicates);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Presorted")]
    public string[] Array_Presorted() => ArrayOrdinal(presorted);

    [Benchmark]
    [BenchmarkCategory("Presorted")]
    public string[] StringSort_Presorted() => StringSortOrdinal(presorted);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NearlySorted")]
    public string[] Array_NearlySorted() => ArrayOrdinal(nearlySorted);

    [Benchmark]
    [BenchmarkCategory("NearlySorted")]
    public string[] StringSort_NearlySorted() => StringSortOrdinal(nearlySorted);

    // ---- Pairs: sort keys carrying a parallel payload ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Pairs")]
    public int[] Array_Pairs()
    {
        random.CopyTo(keys, 0);
        payload.CopyTo(values, 0);
        Array.Sort(keys, values, StringComparer.Ordinal);
        return values;
    }

    [Benchmark]
    [BenchmarkCategory("Pairs")]
    public int[] StringSort_Pairs()
    {
        random.CopyTo(keys, 0);
        payload.CopyTo(values, 0);
        StringSort.Sort(keys.AsSpan(), values.AsSpan());
        return values;
    }

    // ---- ArgSort: produce the sorting permutation without moving the keys ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ArgSort")]
    public int[] Array_ArgSort()
    {
        random.CopyTo(keys, 0);
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        Array.Sort(keys, indices, StringComparer.Ordinal);
        return indices;
    }

    [Benchmark]
    [BenchmarkCategory("ArgSort")]
    public int[] StringSort_ArgSort()
    {
        StringSort.ArgSort(random, indices.AsSpan());
        return indices;
    }
}
