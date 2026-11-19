using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Sorting;

// StableSort against the stable sorts a caller can write with the BCL. Array.Sort and Span.Sort are
// introsorts and may reorder equal elements, so the honest baselines are the two stable routes:
//
//   Linq_*         — records.OrderBy(r => r.Key).ToArray(): stable, and the strongest stable BCL arm in
//                    probing, but it allocates the key array, an index map and the output on every call.
//                    This is the charted baseline.
//   Array_Tagged*  — the careful in-place hand-roll: copy each record into a (record, position) tuple,
//                    Span.Sort the tuples with a comparer that breaks ties on the position, copy back.
//                    Measured in every category; read off the BenchmarkDotNet report, not charted (the
//                    dashboard keys one BCL arm per card).
//   Array_Unstable* — plain Span.Sort with the same comparer. Not stable, so not a baseline: it is the
//                    price-of-stability reference, measured on Random and Strings, where the docs say an
//                    unstable sort can win.
//
// The categories are input shapes, over (int Key, int Id) records ordered by Key:
//   Random       — keys in [0, n/8): eight copies of each key on average, so ties are common.
//   NearlySorted — ascending keys, each repeated four times, then n/100 swaps of two random positions
//                  (about 2% of keys move): the shape of a list re-sorted after a few edits.
//   SortedRuns   — sorted batches of n/16 random keys, concatenated: results from sixteen shards (at
//                  1,000 elements n/16 rounds down, so there are seventeen batches).
//   Strings      — records ordered by an ordinal compare of a string field with a shared prefix: the
//                  expensive-comparison case. (StringSort is the faster tool for sorting the strings
//                  themselves; this measures sorting objects by a string key.)
//
// Every arm sorts a fresh copy of one of several pre-built inputs, rotating per invocation. A 1,000-
// element input replayed on every call lets the branch predictor learn its comparison outcomes, which
// flatters every arm unevenly; the inputs hold about 500,000 keys before the order repeats. The copy is
// charged to every arm alike. StableSort rents its merge buffer from ArrayPool, so its Allocated column
// reads near zero in the steady state a caller sees. ItemCount is the parameter name the gh-pages
// dashboard's benchmark-name parser requires.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class StableSortBenchmark
{
    private const int KeysBeforeRepeat = 500_000;

    public readonly record struct Rec(int Key, int Id);

    public sealed class NamedRec(string name, int id)
    {
        public string Name { get; } = name;

        public int Id { get; } = id;
    }

    private readonly struct ByKey : IComparer<Rec>
    {
        public int Compare(Rec x, Rec y) => x.Key.CompareTo(y.Key);
    }

    private readonly struct TaggedByKey : IComparer<(Rec Item, int Position)>
    {
        public int Compare((Rec Item, int Position) x, (Rec Item, int Position) y)
        {
            int order = x.Item.Key.CompareTo(y.Item.Key);
            return order != 0 ? order : x.Position.CompareTo(y.Position);
        }
    }

    private readonly struct ByName : IComparer<NamedRec>
    {
        public int Compare(NamedRec? x, NamedRec? y) => string.CompareOrdinal(x!.Name, y!.Name);
    }

    private readonly struct TaggedByName : IComparer<(NamedRec Item, int Position)>
    {
        public int Compare((NamedRec Item, int Position) x, (NamedRec Item, int Position) y)
        {
            int order = string.CompareOrdinal(x.Item.Name, y.Item.Name);
            return order != 0 ? order : x.Position.CompareTo(y.Position);
        }
    }

    private Rec[][] random = null!;
    private Rec[][] nearlySorted = null!;
    private Rec[][] sortedRuns = null!;
    private NamedRec[][] strings = null!;
    private Rec[] work = null!;
    private (Rec Item, int Position)[] tagged = null!;
    private NamedRec[] nameWork = null!;
    private (NamedRec Item, int Position)[] nameTagged = null!;
    private int next;

    [Params(1_000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);
        int inputs = Math.Max(5, KeysBeforeRepeat / ItemCount);
        random = new Rec[inputs][];
        nearlySorted = new Rec[inputs][];
        sortedRuns = new Rec[inputs][];
        strings = new NamedRec[inputs][];
        for (int s = 0; s < inputs; s++)
        {
            random[s] = Records(i => rand.Next(Math.Max(1, ItemCount / 8)));

            var nearly = Records(i => i / 4);
            for (int swaps = 0; swaps < ItemCount / 100; swaps++)
            {
                int a = rand.Next(ItemCount), b = rand.Next(ItemCount);
                (nearly[a], nearly[b]) = (nearly[b], nearly[a]);
            }

            nearlySorted[s] = nearly;

            var runs = Records(i => rand.Next(Math.Max(1, ItemCount / 8)));
            int runLength = ItemCount / 16;
            for (int start = 0; start < ItemCount; start += runLength)
            {
                runs.AsSpan(start, Math.Min(runLength, ItemCount - start)).Sort(default(ByKey));
            }

            sortedRuns[s] = runs;

            strings[s] = new NamedRec[ItemCount];
            for (int i = 0; i < ItemCount; i++)
            {
                strings[s][i] = new NamedRec("customer/" + rand.Next(Math.Max(1, ItemCount / 4)).ToString("D7"), i);
            }
        }

        work = new Rec[ItemCount];
        tagged = new (Rec, int)[ItemCount];
        nameWork = new NamedRec[ItemCount];
        nameTagged = new (NamedRec, int)[ItemCount];
    }

    private Rec[] Records(Func<int, int> key)
    {
        var records = new Rec[ItemCount];
        for (int i = 0; i < records.Length; i++)
        {
            records[i] = new Rec(key(i), i);
        }

        return records;
    }

    private T Next<T>(T[] inputs)
    {
        int i = next;
        next = i + 1 == inputs.Length ? 0 : i + 1;
        return inputs[i];
    }

    private static Rec[] Linq(Rec[] source) => source.OrderBy(r => r.Key).ToArray();

    private Rec[] Stable(Rec[] source)
    {
        source.CopyTo(work, 0);
        StableSort.Sort(work.AsSpan(), default(ByKey));
        return work;
    }

    private Rec[] Tagged(Rec[] source)
    {
        for (int i = 0; i < source.Length; i++)
        {
            tagged[i] = (source[i], i);
        }

        tagged.AsSpan().Sort(default(TaggedByKey));
        for (int i = 0; i < tagged.Length; i++)
        {
            work[i] = tagged[i].Item;
        }

        return work;
    }

    private Rec[] Unstable(Rec[] source)
    {
        source.CopyTo(work, 0);
        work.AsSpan().Sort(default(ByKey));
        return work;
    }

    // ---- Random ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Random")]
    public Rec[] Linq_Random() => Linq(Next(random));

    [Benchmark]
    [BenchmarkCategory("Random")]
    public Rec[] Array_TaggedRandom() => Tagged(Next(random));

    [Benchmark]
    [BenchmarkCategory("Random")]
    public Rec[] Array_UnstableRandom() => Unstable(Next(random));

    [Benchmark]
    [BenchmarkCategory("Random")]
    public Rec[] StableSort_Random() => Stable(Next(random));

    // ---- NearlySorted ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NearlySorted")]
    public Rec[] Linq_NearlySorted() => Linq(Next(nearlySorted));

    [Benchmark]
    [BenchmarkCategory("NearlySorted")]
    public Rec[] Array_TaggedNearlySorted() => Tagged(Next(nearlySorted));

    [Benchmark]
    [BenchmarkCategory("NearlySorted")]
    public Rec[] StableSort_NearlySorted() => Stable(Next(nearlySorted));

    // ---- SortedRuns ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SortedRuns")]
    public Rec[] Linq_SortedRuns() => Linq(Next(sortedRuns));

    [Benchmark]
    [BenchmarkCategory("SortedRuns")]
    public Rec[] Array_TaggedSortedRuns() => Tagged(Next(sortedRuns));

    [Benchmark]
    [BenchmarkCategory("SortedRuns")]
    public Rec[] StableSort_SortedRuns() => Stable(Next(sortedRuns));

    // ---- Strings ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Strings")]
    public NamedRec[] Linq_Strings() => Next(strings).OrderBy(r => r.Name, StringComparer.Ordinal).ToArray();

    [Benchmark]
    [BenchmarkCategory("Strings")]
    public NamedRec[] Array_TaggedStrings()
    {
        var source = Next(strings);
        for (int i = 0; i < source.Length; i++)
        {
            nameTagged[i] = (source[i], i);
        }

        nameTagged.AsSpan().Sort(default(TaggedByName));
        for (int i = 0; i < nameTagged.Length; i++)
        {
            nameWork[i] = nameTagged[i].Item;
        }

        return nameWork;
    }

    [Benchmark]
    [BenchmarkCategory("Strings")]
    public NamedRec[] Array_UnstableStrings()
    {
        Next(strings).CopyTo(nameWork, 0);
        nameWork.AsSpan().Sort(default(ByName));
        return nameWork;
    }

    [Benchmark]
    [BenchmarkCategory("Strings")]
    public NamedRec[] StableSort_Strings()
    {
        Next(strings).CopyTo(nameWork, 0);
        StableSort.Sort(nameWork.AsSpan(), default(ByName));
        return nameWork;
    }
}
