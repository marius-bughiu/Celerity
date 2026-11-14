using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

/// <summary>
/// <see cref="StringInternTable"/> against the BCL's own span-keyed interning, over the workload the type exists
/// to serve: a token stream held as spans of one input buffer, with far fewer distinct tokens than occurrences.
/// </summary>
/// <remarks>
/// <para>
/// The BCL arms are <c>HashSet&lt;string&gt;</c> probed through
/// <c>GetAlternateLookup&lt;ReadOnlySpan&lt;char&gt;&gt;()</c>, which every runtime Celerity targets has. That is the
/// BCL's best answer, not a workaround: it probes with the span, <c>TryGetValue</c> hands back the stored instance,
/// and a <see cref="string"/> is materialized only for a token the set has not seen, exactly as
/// <see cref="StringInternTable"/> does. Both sides therefore allocate once per <em>distinct token</em>, so the
/// difference in the <b>Allocated</b> column is what each table's own storage costs, and what is left to compare
/// is the probe.
/// </para>
/// <para>
/// The <c>Dedupe</c> category is the end-to-end comparison: both arms walk the stream from an empty table and end
/// up holding one canonical instance per distinct token. <c>Lookup</c> isolates the probe on an already-warm table.
/// </para>
/// <para>
/// The <c>*CityHash64</c> arms run the same table over <see cref="StringCityHash64Hasher"/> instead of the default
/// <see cref="StringFnV1AFullHasher"/>, which folds every UTF-16 code unit a byte at a time and so accounts for most
/// of the probe's cost on these 16-character tokens; CityHash64 reads them eight bytes at a time. Those arms carry
/// op names of their own so each lands in a dashboard bucket by itself, since two Celerity arms sharing an op would
/// overwrite each other in the index the cards are built from, while the shared category keeps BenchmarkDotNet's
/// ratio against the same <c>HashSet</c> baseline.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class StringInternTableBenchmark
{
    // Distinct tokens in the stream. Deliberately far smaller than ItemCount: the point of interning is
    // that a long stream draws from a small vocabulary (column values, log levels, header names, …).
    private const int DistinctTokens = 64;

    // One contiguous buffer holding the whole token stream, exactly as a parser would have it. The
    // benchmarks slice it — they never hold the tokens as strings.
    private char[] buffer = null!;
    private (int Offset, int Length)[] tokens = null!;

    // A warm table / set for the Lookup category, built once in [GlobalSetup], and the set's span-keyed view,
    // resolved once as a caller holding it would.
    private StringInternTable internedFull = null!;
    private StringInternTable<StringCityHash64Hasher> internedCityFull = null!;
    private HashSet<string> hashSetFull = null!;
    private HashSet<string>.AlternateLookup<ReadOnlySpan<char>> hashSetSpanLookup;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var vocabulary = new string[DistinctTokens];
        for (int i = 0; i < DistinctTokens; i++)
            vocabulary[i] = $"token_{i:D4}_value";

        tokens = new (int, int)[ItemCount];
        var text = new System.Text.StringBuilder(ItemCount * 17);
        for (int i = 0; i < ItemCount; i++)
        {
            string token = vocabulary[i % DistinctTokens];
            tokens[i] = (text.Length, token.Length);
            text.Append(token).Append(',');   // the separator a real parser would slice around
        }

        buffer = text.ToString().ToCharArray();

        internedFull = new StringInternTable(DistinctTokens);
        internedCityFull = new StringInternTable<StringCityHash64Hasher>(DistinctTokens);
        hashSetFull = new HashSet<string>(DistinctTokens, StringComparer.Ordinal);
        foreach (string token in vocabulary)
        {
            internedFull.GetOrAdd(token);
            internedCityFull.GetOrAdd(token);
            hashSetFull.Add(token);
        }

        hashSetSpanLookup = hashSetFull.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    // ---- Dedupe: walk the stream and end up holding one instance per distinct token -------------------

    // The intern pattern on the BCL: TryGetValue returns the canonical instance on a hit, and a miss materializes
    // the token once and stores it. A hit, the common case, is one probe; GetOrAdd is one probe either way.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Dedupe")]
    public int HashSet_Dedupe()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var lookup = set.GetAlternateLookup<ReadOnlySpan<char>>();
        for (int i = 0; i < tokens.Length; i++)
        {
            (int offset, int length) = tokens[i];
            ReadOnlySpan<char> token = buffer.AsSpan(offset, length);
            if (!lookup.TryGetValue(token, out _))
                set.Add(new string(token));
        }
        return set.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Dedupe")]
    public int StringInternTable_Dedupe()
    {
        var table = new StringInternTable();
        for (int i = 0; i < tokens.Length; i++)
        {
            (int offset, int length) = tokens[i];
            table.GetOrAdd(buffer.AsSpan(offset, length));
        }
        return table.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Dedupe")]
    public int StringInternTable_DedupeCityHash64()
    {
        var table = new StringInternTable<StringCityHash64Hasher>();
        for (int i = 0; i < tokens.Length; i++)
        {
            (int offset, int length) = tokens[i];
            table.GetOrAdd(buffer.AsSpan(offset, length));
        }
        return table.Count;
    }

    // ---- Lookup: probe an already-warm table once per occurrence --------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int HashSet_Lookup()
    {
        int hits = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            (int offset, int length) = tokens[i];
            if (hashSetSpanLookup.Contains(buffer.AsSpan(offset, length)))
                hits++;
        }
        return hits;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int StringInternTable_Lookup()
    {
        int hits = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            (int offset, int length) = tokens[i];
            if (internedFull.Contains(buffer.AsSpan(offset, length)))
                hits++;
        }
        return hits;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int StringInternTable_LookupCityHash64()
    {
        int hits = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            (int offset, int length) = tokens[i];
            if (internedCityFull.Contains(buffer.AsSpan(offset, length)))
                hits++;
        }
        return hits;
    }
}
