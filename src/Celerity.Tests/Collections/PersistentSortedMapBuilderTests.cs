using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// <see cref="PersistentSortedMap{TKey, TValue, TComparer}.Builder"/>: the transient accumulator and, above
/// all, the isolation it owes the maps it hands out.
///
/// <para>
/// <b>Isolation is the thing on trial, not the last answer.</b> The builder edits nodes <i>in place</i> —
/// shifting entries inside a leaf, splitting into a truncated original, merging a pair of children into one
/// slot of an owned parent — and it may do that only to nodes no published map can still see. A node carries
/// the ownership token of the builder that made it, <c>ToImmutable</c> takes a fresh token, and a node whose
/// token no longer matches is copied instead of written. Get any of that wrong and the builder's current
/// answer is still right; the damage lands on a map handed out <i>earlier</i>, or on the map the builder was
/// seeded from, which is why every test here keeps snapshots and re-checks them at the end.
/// </para>
///
/// <para>
/// The runs are repeated for <c>int</c> and <c>string</c> keys and values, because an in-place removal clears
/// the slots it vacates only when they hold references, and those are two different code paths.
/// </para>
/// </summary>
public class PersistentSortedMapBuilderTests
{
    // Three levels deep: a two-level tree tops out at 1,024 entries.
    private const int LargeCount = 5_000;

    private static PersistentSortedMap<int, string, DefaultComparer<int>> EmptyMap =>
        PersistentSortedMap<int, string, DefaultComparer<int>>.Empty;

    private static PersistentSortedMap<int, string, DefaultComparer<int>>.Builder NewBuilder() => new();

    // ── The basics ───────────────────────────────────────────────────────────────

    [Fact]
    public void NewBuilder_ShouldBeEmpty()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>>.Builder builder = NewBuilder();

        Assert.Equal(0, builder.Count);
        Assert.False(builder.ContainsKey(1));
        Assert.False(builder.TryGetValue(1, out string? value));
        Assert.Null(value);
        Assert.False(builder.Remove(1));
        Assert.Same(EmptyMap, builder.ToImmutable());
    }

    [Fact]
    public void ToBuilder_ShouldBeSeededWithTheMapsEntries()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(LargeCount);

        PersistentSortedMap<int, string, DefaultComparer<int>>.Builder builder = map.ToBuilder();

        Assert.Equal(LargeCount, builder.Count);
        Assert.Equal("4321", builder[4_321]);
        Assert.True(builder.ContainsKey(0));
        Assert.False(builder.ContainsKey(LargeCount));
    }

    [Fact]
    public void Add_ShouldInsert_AndThrowOnADuplicate()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>>.Builder builder = NewBuilder();

        builder.Add(1, "one");

        Assert.Equal(1, builder.Count);
        Assert.Equal("one", builder[1]);

        var ex = Assert.Throws<ArgumentException>(() => builder.Add(1, "uno"));
        Assert.Equal("key", ex.ParamName);
    }

    [Fact]
    public void Add_ShouldLeaveTheBuilderUntouched_WhenItRejectsADuplicate()
    {
        // Add takes the insert-only path, so a caller who catches the duplicate-key exception still holds the
        // builder exactly as it was — in an owned leaf and in one still shared with the seed map alike.
        PersistentSortedMap<int, string, DefaultComparer<int>> seed = BuildRange(LargeCount);
        PersistentSortedMap<int, string, DefaultComparer<int>>.Builder builder = seed.ToBuilder();

        Assert.Throws<ArgumentException>(() => builder.Add(2_000, "changed"));
        builder.Add(LargeCount, "new");
        Assert.Throws<ArgumentException>(() => builder.Add(LargeCount, "newer"));

        Assert.Equal("2000", builder[2_000]);
        Assert.Equal("new", builder[LargeCount]);
        Assert.Equal(LargeCount + 1, builder.Count);
        Assert.Equal(LargeCount + 1, builder.ToImmutable().Count);
    }

    [Fact]
    public void Indexer_ShouldInsertAndOverwrite()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>>.Builder builder = NewBuilder();

        builder[1] = "one";
        builder[1] = "uno";
        builder[2] = "two";

        Assert.Equal(2, builder.Count);
        Assert.Equal("uno", builder[1]);
        Assert.Equal("two", builder[2]);
    }

    [Fact]
    public void Indexer_ShouldThrow_WhenTheKeyIsAbsent()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>>.Builder builder = NewBuilder();
        builder[1] = "one";

        Assert.Throws<KeyNotFoundException>(() => builder[2]);
    }

    [Fact]
    public void OverwritingWithAnEqualValue_ShouldNotChangeTheCount()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>>.Builder builder = BuildRange(100).ToBuilder();

        builder[50] = "50";
        builder[50] = "fifty";
        builder[50] = "fifty";

        Assert.Equal(100, builder.Count);
        Assert.Equal("fifty", builder[50]);
    }

    [Fact]
    public void Remove_ShouldReportWhetherTheKeyWasThere()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>>.Builder builder = NewBuilder();
        builder[1] = "one";
        builder[2] = "two";

        Assert.True(builder.Remove(1));
        Assert.False(builder.Remove(1));
        Assert.False(builder.Remove(3));
        Assert.Equal(1, builder.Count);
        Assert.False(builder.ContainsKey(1));
    }

    // ── Isolation ────────────────────────────────────────────────────────────────

    [Fact]
    public void ToBuilder_ShouldNotWriteThroughIntoTheSourceMap()
    {
        // The first write to each node the builder reaches finds it shared with the seed: an overwrite, an
        // insert, a split, a removal and a rebalance must each copy rather than edit it.
        PersistentSortedMap<int, string, DefaultComparer<int>> seed = BuildRange(LargeCount);
        PersistentSortedMap<int, string, DefaultComparer<int>>.Builder builder = seed.ToBuilder();

        builder[10] = "changed";
        builder[-1] = "minus one";
        for (int key = LargeCount; key < LargeCount + 100; key++)
            builder[key] = key.ToString();

        foreach (int key in Shuffled(LargeCount, seed: 23).Take(3_000))
            builder.Remove(key);

        AssertRange(seed, LargeCount);
        Assert.Equal(builder.Count, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(builder.ToImmutable()));
    }

    [Fact]
    public void ToBuilder_ShouldNotWriteThroughIntoTheSourceMap_WhenItsFirstWriteIsARebalance()
    {
        // A removal straight off the seed, from a leaf already at MinFill, rebalances under branches the
        // builder does not own yet — the unowned merge and the unowned redistribute of the parent.
        PersistentSortedMap<int, string, DefaultComparer<int>> seed = BuildRange(LargeCount);

        for (int key = 0; key < LargeCount; key += 37)
        {
            PersistentSortedMap<int, string, DefaultComparer<int>>.Builder builder = seed.ToBuilder();
            Assert.True(builder.Remove(key));

            PersistentSortedMap<int, string, DefaultComparer<int>> trimmed = builder.ToImmutable();
            Assert.Equal(LargeCount - 1, trimmed.Count);
            Assert.False(trimmed.ContainsKey(key));
        }

        AssertRange(seed, LargeCount);
    }

    [Fact]
    public void ToImmutable_ShouldFreezeTheSnapshot_AgainstEveryLaterKindOfWrite()
    {
        // Built through the builder, so every node of the snapshot is one the builder made — and owned until
        // ToImmutable. Each later write must find it read-only: overwrite, insert, split, remove, borrow, merge.
        PersistentSortedMap<int, string, DefaultComparer<int>>.Builder builder = NewBuilder();
        for (int key = 0; key < LargeCount; key++)
            builder.Add(key, key.ToString());

        PersistentSortedMap<int, string, DefaultComparer<int>> snapshot = builder.ToImmutable();

        builder[0] = "changed";
        builder[2_500] = "changed";
        for (int key = LargeCount; key < LargeCount + 200; key++)
            builder.Add(key, key.ToString());

        foreach (int key in Shuffled(LargeCount, seed: 29))
            builder.Remove(key);

        Assert.Equal(200, builder.Count);
        AssertRange(snapshot, LargeCount);
    }

    [Fact]
    public void EverySnapshotAlongARun_ShouldStaySeparate()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>>.Builder builder = NewBuilder();
        var snapshots = new List<(PersistentSortedMap<int, string, DefaultComparer<int>> Map, SortedDictionary<int, string> Expected)>();
        var expected = new SortedDictionary<int, string>();
        var rng = new Random(31);

        for (int step = 0; step < 12_000; step++)
        {
            int key = rng.Next(3_000);
            if (rng.Next(100) < 40)
            {
                Assert.Equal(expected.Remove(key), builder.Remove(key));
            }
            else
            {
                builder[key] = "v" + step;
                expected[key] = "v" + step;
            }

            if (step % 503 == 0)
                snapshots.Add((builder.ToImmutable(), new SortedDictionary<int, string>(expected)));
        }

        snapshots.Add((builder.ToImmutable(), new SortedDictionary<int, string>(expected)));

        foreach ((PersistentSortedMap<int, string, DefaultComparer<int>> map, SortedDictionary<int, string> oracle) in snapshots)
        {
            Assert.Equal(oracle.Count, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(map));
            Assert.Equal(oracle.Select(e => (e.Key, (string?)e.Value)), map.Select(e => (e.Key, e.Value)));
        }
    }

    [Fact]
    public void ABuilderThatDrainsToNothing_ShouldProduceTheEmptySingleton()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>>.Builder builder = BuildRange(LargeCount).ToBuilder();

        foreach (int key in Shuffled(LargeCount, seed: 37))
            Assert.True(builder.Remove(key));

        Assert.Equal(0, builder.Count);
        Assert.Same(EmptyMap, builder.ToImmutable());

        // The builder stays usable from the shared empty root, and the singleton stays empty.
        builder[1] = "one";
        Assert.Equal(1, builder.ToImmutable().Count);
        Assert.True(EmptyMap.IsEmpty);
    }

    // ── Large runs ───────────────────────────────────────────────────────────────

    [Fact]
    public void LargeRuns_WithValueTypeKeysAndValues_ShouldStayValid()
    {
        // int keys and values: the in-place truncate and remove leave vacated slots uncleared, since there are
        // no references to release.
        PersistentSortedMap<int, int, DefaultComparer<int>>.Builder builder = new();
        var expected = new SortedDictionary<int, int>();

        RunRandom(
            builder,
            expected,
            key => key,
            key => key * 3,
            seed: 41,
            (b, e) => Assert.Equal(e.Count, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(b.ToImmutable())));

        PersistentSortedMap<int, int, DefaultComparer<int>> map = builder.ToImmutable();
        Assert.Equal(expected.Select(e => (e.Key, e.Value)), map.Select(e => (e.Key, e.Value)));
    }

    [Fact]
    public void LargeRuns_WithReferenceTypeKeysAndValues_ShouldStayValid()
    {
        // string keys and values: the in-place paths clear the slots they vacate so a builder does not keep
        // removed keys and values reachable.
        PersistentSortedMap<string, string, DefaultComparer<string>>.Builder builder = new();
        var expected = new SortedDictionary<string, string>(Comparer<string>.Default);

        RunRandom(
            builder,
            expected,
            key => key.ToString("D5"),
            key => "v" + key,
            seed: 43,
            (b, e) => Assert.Equal(e.Count, PersistentSortedMapShape.AssertValid<string, DefaultComparer<string>>(b.ToImmutable())));

        PersistentSortedMap<string, string, DefaultComparer<string>> map = builder.ToImmutable();
        Assert.Equal(expected.Select(e => (e.Key, (string?)e.Value)), map.Select(e => (e.Key, e.Value)));
    }

    // Fills to several thousand keys in random order, drains most of them in random order, refills and
    // drains again, checking the tree's shape at intervals. Interval ToImmutable calls hand the next writes
    // a mix of owned and unowned nodes, so both the in-place and the copying arm of each node edit run.
    private static void RunRandom<TKey, TValue>(
        PersistentSortedMap<TKey, TValue, DefaultComparer<TKey>>.Builder builder,
        SortedDictionary<TKey, TValue> expected,
        Func<int, TKey> keyOf,
        Func<int, TValue> valueOf,
        int seed,
        Action<PersistentSortedMap<TKey, TValue, DefaultComparer<TKey>>.Builder, SortedDictionary<TKey, TValue>> check)
        where TKey : notnull
    {
        var rng = new Random(seed);
        for (int round = 0; round < 2; round++)
        {
            foreach (int i in Shuffled(LargeCount, seed + round))
            {
                builder[keyOf(i)] = valueOf(i);
                expected[keyOf(i)] = valueOf(i);
                if (expected.Count % 1_009 == 0)
                    check(builder, expected);
            }

            Assert.Equal(expected.Count, builder.Count);
            check(builder, expected);

            foreach (int i in Shuffled(LargeCount, seed + round + 100).Take(LargeCount - 50))
            {
                Assert.Equal(expected.Remove(keyOf(i)), builder.Remove(keyOf(i)));
                if (rng.Next(500) == 0)
                    check(builder, expected);
            }

            Assert.Equal(expected.Count, builder.Count);
            check(builder, expected);
        }
    }

    private static void AssertRange(PersistentSortedMap<int, string, DefaultComparer<int>> map, int count)
    {
        Assert.Equal(count, map.Count);
        Assert.Equal(count, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(map));

        int expected = 0;
        foreach (KeyValuePair<int, string?> entry in map)
        {
            Assert.Equal(expected, entry.Key);
            Assert.Equal(expected.ToString(), entry.Value);
            expected++;
        }
    }

    private static PersistentSortedMap<int, string, DefaultComparer<int>> BuildRange(int count)
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap;
        for (int key = 0; key < count; key++)
            map = map.Add(key, key.ToString());

        return map;
    }

    private static int[] Shuffled(int count, int seed)
    {
        int[] keys = Enumerable.Range(0, count).ToArray();
        new Random(seed).Shuffle(keys);
        return keys;
    }
}
