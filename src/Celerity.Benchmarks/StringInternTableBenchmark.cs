using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

/// <summary>
/// <see cref="StringInternTable"/> against the BCL shapes a parser would otherwise reach for, over the
/// workload the type exists to serve: a token stream held as spans of one input buffer, with far fewer
/// distinct tokens than occurrences.
/// </summary>
/// <remarks>
/// <para>
/// The headline is the <b>Allocated</b> column, not the nanoseconds — which is why
/// <c>[MemoryDiagnoser]</c> reports bytes here. Every BCL arm must call <c>new string(span)</c> before it
/// can even ask whether it already had that token, so it allocates once per <em>occurrence</em>;
/// <see cref="StringInternTable"/> is probed with the span and allocates once per <em>distinct token</em>.
/// At the sweep's 64-token universe that is a 15×–1500× difference in strings created, and the gap widens
/// with the stream length.
/// </para>
/// <para>
/// The <c>Dedupe</c> category is the honest end-to-end comparison: all three arms end up holding one
/// canonical instance per distinct token, so they differ only in how much garbage they made getting there.
/// <c>Lookup</c> isolates the probe on an already-warm table, where the intern table's win is just the
/// deleted allocation and copy.
/// </para>
/// <para>
/// <b>The .NET 9+ caveat.</b> .NET 9 shipped
/// <c>Dictionary&lt;string,V&gt;.GetAlternateLookup&lt;ReadOnlySpan&lt;char&gt;&gt;()</c>, which closes
/// this gap for a plain dictionary on that runtime. It is not benchmarked here because this project
/// targets <c>net8.0</c> only — the floor where the BCL has no answer at all, and the reason the type
/// exists. On .NET 9+ the two approaches are comparable; the library's value there is that the same code
/// works across all three of Celerity's target frameworks.
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

    // A warm table / set for the Lookup category, built once in [GlobalSetup].
    private StringInternTable internedFull = null!;
    private HashSet<string> hashSetFull = null!;

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
        hashSetFull = new HashSet<string>(DistinctTokens, StringComparer.Ordinal);
        foreach (string token in vocabulary)
        {
            internedFull.GetOrAdd(token);
            hashSetFull.Add(token);
        }
    }

    // ---- Dedupe: walk the stream and end up holding one instance per distinct token -------------------

    // The pre-.NET-9 BCL answer: materialize every occurrence, then let the set throw the duplicates away.
    // Correct, and it allocates one string per occurrence.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Dedupe")]
    public int HashSet_Dedupe()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < tokens.Length; i++)
        {
            (int offset, int length) = tokens[i];
            set.Add(new string(buffer, offset, length));
        }
        return set.Count;
    }

    // The same shape with a dictionary, which at least hands the canonical instance back — still one
    // allocation per occurrence, because the key must exist before it can be looked up.
    [Benchmark]
    [BenchmarkCategory("Dedupe")]
    public int Dictionary_Dedupe()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < tokens.Length; i++)
        {
            (int offset, int length) = tokens[i];
            string materialized = new string(buffer, offset, length);
            if (!map.TryGetValue(materialized, out _))
                map[materialized] = materialized;
        }
        return map.Count;
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

    // ---- Lookup: probe an already-warm table once per occurrence --------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int HashSet_Lookup()
    {
        int hits = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            (int offset, int length) = tokens[i];
            if (hashSetFull.Contains(new string(buffer, offset, length)))
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
}
