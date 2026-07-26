using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>: the constructors, the
/// add / lookup / remove core, the ordered surface (<c>Min</c>, <c>Max</c>, the two bounds,
/// <c>EnumerateRange</c>), the key and value views, the <see cref="IDictionary{TKey, TValue}"/> /
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> surface, and the structural corners that only fire once
/// nodes split and merge. Enumeration lives in <see cref="BTreeDictionaryEnumerationTests"/>; the randomized
/// reconciliation against <see cref="SortedDictionary{TKey, TValue}"/> lives in
/// <see cref="BTreeDictionaryDifferentialTests"/>.
/// </summary>
public class BTreeDictionaryTests
{
    // A stateful struct comparer: proves the type parameter is not assumed to be default-constructed, and
    // that a custom order is respected end to end.
    private readonly struct DirectionalComparer : IComparer<int>
    {
        private readonly int _sign;

        public DirectionalComparer(bool ascending) => _sign = ascending ? 1 : -1;

        public int Compare(int x, int y) => _sign * x.CompareTo(y);
    }

    [Fact]
    public void Constructor_ShouldStartEmpty_WhenParameterless()
    {
        var dict = new BTreeDictionary<int, string>();

        Assert.Equal(0, dict.Count);
        Assert.False(dict.ContainsKey(0));
        Assert.Empty(dict);
    }

    [Fact]
    public void Constructor_ShouldSeedFromSource_WhenGivenEnumerable()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(3, "three"),
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(2, "two"),
        };

        var dict = new BTreeDictionary<int, string>(source);

        Assert.Equal(3, dict.Count);
        Assert.Equal(new[] { 1, 2, 3 }, dict.Select(e => e.Key));
        Assert.Equal("two", dict[2]);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new BTreeDictionary<int, string>(null!));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSourceHasDuplicateKey()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(1, "one"),
            new KeyValuePair<int, string>(1, "uno"),
        };

        Assert.Throws<ArgumentException>(() => new BTreeDictionary<int, string>(source));
    }

    [Fact]
    public void Constructor_ShouldUseSuppliedComparer_WhenComparerIsStateful()
    {
        // Descending order comes from the comparer *instance*, not from default(TComparer) — the ascending
        // default would produce the opposite sequence.
        var dict = new BTreeDictionary<int, string, DirectionalComparer>(new DirectionalComparer(ascending: false));

        for (int i = 0; i < 100; i++)
            dict.Add(i, i.ToString());

        Assert.Equal(Enumerable.Range(0, 100).Reverse(), dict.Select(e => e.Key));
        Assert.Equal(99, dict.Min.Key);
        Assert.Equal(0, dict.Max.Key);
    }

    [Fact]
    public void Add_ShouldStoreEntry_WhenKeyIsAbsent()
    {
        var dict = new BTreeDictionary<int, string>();

        dict.Add(7, "seven");

        Assert.Equal(1, dict.Count);
        Assert.True(dict.TryGetValue(7, out string? value));
        Assert.Equal("seven", value);
    }

    [Fact]
    public void Add_ShouldThrow_WhenKeyAlreadyPresent()
    {
        var dict = new BTreeDictionary<int, string>();
        dict.Add(7, "seven");

        var ex = Assert.Throws<ArgumentException>(() => dict.Add(7, "again"));
        Assert.Equal("key", ex.ParamName);
        Assert.Equal(1, dict.Count);
        Assert.Equal("seven", dict[7]);
    }

    [Fact]
    public void TryAdd_ShouldReturnFalseAndKeepValue_WhenKeyAlreadyPresent()
    {
        var dict = new BTreeDictionary<int, string>();
        Assert.True(dict.TryAdd(7, "seven"));

        Assert.False(dict.TryAdd(7, "again"));
        Assert.Equal(1, dict.Count);
        Assert.Equal("seven", dict[7]);
    }

    [Fact]
    public void TryAdd_ShouldNotDisturbTheTree_WhenKeyIsPresentInAFullLeaf()
    {
        // A node holds 31 keys before it splits. Re-adding an existing key while the leaf is exactly full
        // must neither split it nor lose an entry — the reason insertion splits bottom-up rather than
        // preemptively on the way down.
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 31; i++)
            dict.Add(i, i);

        for (int i = 0; i < 31; i++)
            Assert.False(dict.TryAdd(i, -1));

        Assert.Equal(31, dict.Count);
        Assert.Equal(Enumerable.Range(0, 31), dict.Select(e => e.Key));
        Assert.Equal(Enumerable.Range(0, 31), dict.Select(e => e.Value));
    }

    [Fact]
    public void Indexer_ShouldInsertThenOverwrite()
    {
        var dict = new BTreeDictionary<int, string>();

        dict[1] = "one";
        Assert.Equal(1, dict.Count);
        Assert.Equal("one", dict[1]);

        dict[1] = "uno";
        Assert.Equal(1, dict.Count);
        Assert.Equal("uno", dict[1]);
    }

    [Fact]
    public void Indexer_ShouldThrow_WhenKeyIsAbsent()
    {
        var dict = new BTreeDictionary<int, string>();
        dict.Add(1, "one");

        Assert.Throws<KeyNotFoundException>(() => _ = dict[2]);
    }

    [Fact]
    public void TryGetValue_ShouldReturnFalseAndDefault_WhenKeyIsAbsent()
    {
        var dict = new BTreeDictionary<int, int>();
        dict.Add(1, 100);

        Assert.False(dict.TryGetValue(2, out int value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void ContainsValue_ShouldFindValue_RegardlessOfKeyOrder()
    {
        var dict = new BTreeDictionary<int, string>();
        for (int i = 0; i < 100; i++)
            dict.Add(i, $"v{i}");

        Assert.True(dict.ContainsValue("v0"));
        Assert.True(dict.ContainsValue("v99"));
        Assert.False(dict.ContainsValue("v100"));
    }

    [Fact]
    public void ContainsValue_ShouldFindNull_WhenAnEntryHoldsNull()
    {
        var dict = new BTreeDictionary<int, string>();
        dict.Add(1, null);

        Assert.True(dict.ContainsValue(null));
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenKeyIsAbsent()
    {
        var dict = new BTreeDictionary<int, int>();
        dict.Add(1, 100);

        Assert.False(dict.Remove(2));
        Assert.Equal(1, dict.Count);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenDictionaryIsEmpty()
    {
        var dict = new BTreeDictionary<int, int>();

        Assert.False(dict.Remove(1, out int value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void Remove_ShouldYieldRemovedValue_WhenKeyIsPresent()
    {
        var dict = new BTreeDictionary<int, string>();
        dict.Add(1, "one");

        Assert.True(dict.Remove(1, out string? value));
        Assert.Equal("one", value);
        Assert.Equal(0, dict.Count);
        Assert.False(dict.ContainsKey(1));
    }

    [Fact]
    public void Remove_ShouldEmptyTheTree_WhenTheLastEntryGoes()
    {
        var dict = new BTreeDictionary<int, int>();
        dict.Add(5, 50);

        Assert.True(dict.Remove(5));

        Assert.Equal(0, dict.Count);
        Assert.Empty(dict);
        Assert.False(dict.TryGetMin(out _));

        // The tree must still accept inserts after it drops its last node.
        dict.Add(6, 60);
        Assert.Equal(60, dict[6]);
    }

    [Fact]
    public void Clear_ShouldEmptyTheDictionary_AndAllowReuse()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 500; i++)
            dict.Add(i, i);

        dict.Clear();

        Assert.Equal(0, dict.Count);
        Assert.Empty(dict);
        Assert.False(dict.ContainsKey(250));

        dict.Add(1, 1);
        Assert.Equal(1, dict.Count);
    }

    [Fact]
    public void Clear_ShouldBeANoOp_WhenAlreadyEmpty()
    {
        var dict = new BTreeDictionary<int, int>();

        dict.Clear();

        Assert.Equal(0, dict.Count);
    }

    // ---- structural corners: splits, merges, and the shapes in between -------

    [Theory]
    [InlineData(1)]
    [InlineData(31)]     // exactly one full node
    [InlineData(32)]     // the first root split
    [InlineData(512)]    // two levels
    [InlineData(4000)]   // three levels
    public void Add_ShouldKeepEveryEntry_WhenNodesSplit(int count)
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < count; i++)
            dict.Add(i, i * 2);

        Assert.Equal(count, dict.Count);
        Assert.Equal(Enumerable.Range(0, count), dict.Select(e => e.Key));
        for (int i = 0; i < count; i++)
            Assert.Equal(i * 2, dict[i]);
    }

    [Fact]
    public void Add_ShouldSortKeys_WhenInsertedInDescendingOrder()
    {
        // Descending inserts always split the leftmost leaf, the mirror image of the ascending case.
        var dict = new BTreeDictionary<int, int>();
        for (int i = 999; i >= 0; i--)
            dict.Add(i, i);

        Assert.Equal(Enumerable.Range(0, 1000), dict.Select(e => e.Key));
    }

    [Fact]
    public void Add_ShouldSortKeys_WhenInsertedInShuffledOrder()
    {
        var rand = new Random(20260726);
        int[] keys = Enumerable.Range(0, 2000).OrderBy(_ => rand.Next()).ToArray();

        var dict = new BTreeDictionary<int, int>();
        foreach (int key in keys)
            dict.Add(key, key);

        Assert.Equal(Enumerable.Range(0, 2000), dict.Select(e => e.Key));
    }

    [Theory]
    [InlineData("ascending")]
    [InlineData("descending")]
    [InlineData("alternating")]
    public void Remove_ShouldRebalance_WhenNodesUnderflow(string order)
    {
        // Deleting the whole tree drives every rebalancing path: borrow-from-left, borrow-from-right, merge
        // with either sibling, the internal-node predecessor/successor swap, and the root losing a level.
        const int Count = 1500;
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < Count; i++)
            dict.Add(i, i);

        int[] removal = order switch
        {
            "ascending" => Enumerable.Range(0, Count).ToArray(),
            "descending" => Enumerable.Range(0, Count).Reverse().ToArray(),
            _ => Enumerable.Range(0, Count).Where(i => i % 2 == 0)
                    .Concat(Enumerable.Range(0, Count).Where(i => i % 2 == 1).Reverse())
                    .ToArray(),
        };

        var expected = new SortedSet<int>(Enumerable.Range(0, Count));
        foreach (int key in removal)
        {
            Assert.True(dict.Remove(key, out int value));
            Assert.Equal(key, value);
            expected.Remove(key);

            Assert.Equal(expected.Count, dict.Count);
            Assert.Equal(expected, dict.Select(e => e.Key));
        }

        Assert.Equal(0, dict.Count);
    }

    [Fact]
    public void Remove_ShouldKeepTheRestIntact_WhenDeletingFromInternalNodes()
    {
        // Every 32nd key is the one most likely to have been promoted into an internal node, which is the
        // predecessor/successor-swap path rather than the plain leaf delete.
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 1024; i++)
            dict.Add(i, i);

        var expected = new SortedSet<int>(Enumerable.Range(0, 1024));
        for (int i = 0; i < 1024; i += 32)
        {
            Assert.True(dict.Remove(i));
            expected.Remove(i);
        }

        Assert.Equal(expected.Count, dict.Count);
        Assert.Equal(expected, dict.Select(e => e.Key));
    }

    [Fact]
    public void AddAndRemove_ShouldStayConsistent_WhenInterleaved()
    {
        var rand = new Random(4242);
        var dict = new BTreeDictionary<int, int>();
        var oracle = new SortedDictionary<int, int>();

        for (int step = 0; step < 20_000; step++)
        {
            int key = rand.Next(0, 900);
            if (rand.Next(0, 2) == 0)
            {
                Assert.Equal(oracle.TryAdd(key, step), dict.TryAdd(key, step));
            }
            else
            {
                Assert.Equal(oracle.Remove(key), dict.Remove(key));
            }
        }

        Assert.Equal(oracle.Count, dict.Count);
        Assert.Equal(oracle.Keys, dict.Select(e => e.Key));
        Assert.Equal(oracle.Values, dict.Select(e => e.Value));
    }

    // ---- the default / null key ---------------------------------------------

    [Fact]
    public void Add_ShouldTreatDefaultKeyAsOrdinary_WhenKeyIsAValueType()
    {
        // There is no out-of-band default-key slot here: default(int) is 0, an ordinary key that sorts
        // between the negatives and the positives rather than first.
        var dict = new BTreeDictionary<int, string>();
        dict.Add(5, "five");
        dict.Add(0, "zero");
        dict.Add(-5, "minus five");

        Assert.Equal(new[] { -5, 0, 5 }, dict.Select(e => e.Key));
        Assert.Equal("zero", dict[0]);
        Assert.True(dict.Remove(0));
        Assert.False(dict.ContainsKey(0));
    }

    [Fact]
    public void Add_ShouldAcceptNullKey_AndSortItBeforeEveryNonNullKey()
    {
        // Comparer<string>.Default orders null before every non-null key, so — unlike SortedDictionary —
        // a null key is a legal, well-ordered key here.
        var dict = new BTreeDictionary<string, int>();
        dict.Add("b", 2);
        dict.Add(null!, 0);
        dict.Add("a", 1);

        Assert.Equal(new string?[] { null, "a", "b" }, dict.Select(e => e.Key));
        Assert.Equal(0, dict[null!]);
        Assert.True(dict.ContainsKey(null!));
        Assert.True(dict.Remove(null!));
        Assert.Equal(2, dict.Count);
    }

    // ---- the ordered surface -------------------------------------------------

    [Fact]
    public void MinAndMax_ShouldThrow_WhenDictionaryIsEmpty()
    {
        var dict = new BTreeDictionary<int, int>();

        Assert.Throws<InvalidOperationException>(() => _ = dict.Min);
        Assert.Throws<InvalidOperationException>(() => _ = dict.Max);
        Assert.False(dict.TryGetMin(out KeyValuePair<int, int> min));
        Assert.Equal(default, min);
        Assert.False(dict.TryGetMax(out KeyValuePair<int, int> max));
        Assert.Equal(default, max);
    }

    [Fact]
    public void MinAndMax_ShouldReturnTheSameEntry_WhenDictionaryHasOneEntry()
    {
        var dict = new BTreeDictionary<int, int>();
        dict.Add(9, 90);

        Assert.Equal(new KeyValuePair<int, int>(9, 90), dict.Min);
        Assert.Equal(new KeyValuePair<int, int>(9, 90), dict.Max);
    }

    [Fact]
    public void MinAndMax_ShouldTrackTheEnds_AcrossSplitsAndRemovals()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 1000; i++)
            dict.Add(i, i * 3);

        Assert.Equal(new KeyValuePair<int, int>(0, 0), dict.Min);
        Assert.Equal(new KeyValuePair<int, int>(999, 2997), dict.Max);

        dict.Remove(0);
        dict.Remove(999);

        Assert.Equal(1, dict.Min.Key);
        Assert.Equal(998, dict.Max.Key);
    }

    [Fact]
    public void TryGetLowerBound_ShouldReturnTheKeyItself_WhenPresent()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 200; i += 2)
            dict.Add(i, i);

        Assert.True(dict.TryGetLowerBound(50, out KeyValuePair<int, int> entry));
        Assert.Equal(50, entry.Key);
    }

    [Fact]
    public void TryGetLowerBound_ShouldReturnTheNextKey_WhenAbsent()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 200; i += 2)
            dict.Add(i, i * 10);

        Assert.True(dict.TryGetLowerBound(51, out KeyValuePair<int, int> entry));
        Assert.Equal(52, entry.Key);
        Assert.Equal(520, entry.Value);
    }

    [Fact]
    public void TryGetLowerBound_ShouldFail_WhenEveryKeyIsSmaller()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 100; i++)
            dict.Add(i, i);

        Assert.False(dict.TryGetLowerBound(100, out KeyValuePair<int, int> entry));
        Assert.Equal(default, entry);
    }

    [Fact]
    public void TryGetUpperBound_ShouldSkipAnExactMatch()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 200; i += 2)
            dict.Add(i, i);

        Assert.True(dict.TryGetUpperBound(50, out KeyValuePair<int, int> entry));
        Assert.Equal(52, entry.Key);

        Assert.True(dict.TryGetUpperBound(51, out entry));
        Assert.Equal(52, entry.Key);
    }

    [Fact]
    public void TryGetUpperBound_ShouldFail_WhenNoKeyIsLarger()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 100; i++)
            dict.Add(i, i);

        Assert.False(dict.TryGetUpperBound(99, out _));
        Assert.False(dict.TryGetUpperBound(1000, out _));
    }

    [Fact]
    public void Bounds_ShouldFail_WhenDictionaryIsEmpty()
    {
        var dict = new BTreeDictionary<int, int>();

        Assert.False(dict.TryGetLowerBound(0, out _));
        Assert.False(dict.TryGetUpperBound(0, out _));
    }

    [Fact]
    public void Bounds_ShouldMatchTheOracle_AtEveryPositionOfALargeTree()
    {
        // Sweeps both bounds across present keys, absent keys, and both ends, against a linear oracle — the
        // shape that catches an off-by-one in the descent's candidate tracking.
        var dict = new BTreeDictionary<int, int>();
        int[] keys = Enumerable.Range(0, 600).Select(i => i * 3).ToArray();
        foreach (int key in keys)
            dict.Add(key, key);

        for (int probe = -2; probe <= keys[^1] + 2; probe++)
        {
            bool expectedLower = keys.Any(k => k >= probe);
            Assert.Equal(expectedLower, dict.TryGetLowerBound(probe, out KeyValuePair<int, int> lower));
            if (expectedLower)
                Assert.Equal(keys.First(k => k >= probe), lower.Key);

            bool expectedUpper = keys.Any(k => k > probe);
            Assert.Equal(expectedUpper, dict.TryGetUpperBound(probe, out KeyValuePair<int, int> upper));
            if (expectedUpper)
                Assert.Equal(keys.First(k => k > probe), upper.Key);
        }
    }

    [Fact]
    public void EnumerateRange_ShouldYieldTheHalfOpenRange()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 1000; i++)
            dict.Add(i, i);

        Assert.Equal(Enumerable.Range(100, 50), dict.EnumerateRange(100, 150).Select(e => e.Key));
    }

    [Fact]
    public void EnumerateRange_ShouldYieldNothing_WhenTheRangeIsEmptyOrOutside()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 100; i++)
            dict.Add(i, i);

        Assert.Empty(dict.EnumerateRange(50, 50));
        Assert.Empty(dict.EnumerateRange(1000, 2000));
        Assert.Empty(dict.EnumerateRange(-50, -10));
    }

    [Fact]
    public void EnumerateRange_ShouldThrow_WhenBoundsAreInverted()
    {
        var dict = new BTreeDictionary<int, int>();

        var ex = Assert.Throws<ArgumentException>(() => dict.EnumerateRange(10, 5));
        Assert.Equal("toExclusive", ex.ParamName);
    }

    [Fact]
    public void EnumerateRange_ShouldMatchTheOracle_ForManyRandomWindows()
    {
        var rand = new Random(90210);
        var dict = new BTreeDictionary<int, int>();
        int[] keys = Enumerable.Range(0, 800).Select(i => i * 2).ToArray();
        foreach (int key in keys)
            dict.Add(key, key);

        for (int trial = 0; trial < 200; trial++)
        {
            int from = rand.Next(-10, 1620);
            int to = from + rand.Next(0, 200);

            int[] expected = keys.Where(k => k >= from && k < to).ToArray();
            Assert.Equal(expected, dict.EnumerateRange(from, to).Select(e => e.Key));
        }
    }

    // ---- the key and value views --------------------------------------------

    [Fact]
    public void Keys_ShouldEnumerateInOrder_AndReportCount()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 99; i >= 0; i--)
            dict.Add(i, i);

        Assert.Equal(100, dict.Keys.Count);
        Assert.Equal(Enumerable.Range(0, 100), dict.Keys);
        Assert.True(dict.Keys.Contains(50));
        Assert.False(dict.Keys.Contains(100));
        Assert.True(dict.Keys.IsReadOnly);
    }

    [Fact]
    public void Values_ShouldEnumerateInKeyOrder_AndReportCount()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 99; i >= 0; i--)
            dict.Add(i, i * 10);

        Assert.Equal(100, dict.Values.Count);
        Assert.Equal(Enumerable.Range(0, 100).Select(i => i * 10), dict.Values);
        Assert.True(dict.Values.Contains(500));
        Assert.False(dict.Values.Contains(7));
        Assert.True(dict.Values.IsReadOnly);
    }

    [Fact]
    public void KeysAndValues_ShouldCopyInOrder()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 40; i++)
            dict.Add(i, i * 2);

        var keys = new int[42];
        dict.Keys.CopyTo(keys, 2);
        Assert.Equal(Enumerable.Range(0, 40), keys.Skip(2));

        var values = new int[40];
        dict.Values.CopyTo(values, 0);
        Assert.Equal(Enumerable.Range(0, 40).Select(i => i * 2), values);
    }

    [Fact]
    public void KeysAndValues_ShouldThrow_WhenCopyTargetIsInvalid()
    {
        var dict = new BTreeDictionary<int, int>();
        dict.Add(1, 1);

        Assert.Throws<ArgumentNullException>(() => dict.Keys.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => dict.Keys.CopyTo(new int[4], -1));
        Assert.Throws<ArgumentException>(() => dict.Keys.CopyTo(new int[1], 1));
        Assert.Throws<ArgumentNullException>(() => dict.Values.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => dict.Values.CopyTo(new int[4], -1));
        Assert.Throws<ArgumentException>(() => dict.Values.CopyTo(new int[1], 1));
    }

    [Fact]
    public void KeyAndValueViews_ShouldThrow_WhenMutated()
    {
        var dict = new BTreeDictionary<int, int>();
        dict.Add(1, 1);

        ICollection<int> keys = dict.Keys;
        ICollection<int> values = dict.Values;

        Assert.Throws<NotSupportedException>(() => keys.Add(2));
        Assert.Throws<NotSupportedException>(() => keys.Clear());
        Assert.Throws<NotSupportedException>(() => keys.Remove(1));
        Assert.Throws<NotSupportedException>(() => values.Add(2));
        Assert.Throws<NotSupportedException>(() => values.Clear());
        Assert.Throws<NotSupportedException>(() => values.Remove(1));
    }

    // ---- the BCL interface surface ------------------------------------------

    [Fact]
    public void CopyTo_ShouldWriteEntriesInKeyOrder()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 49; i >= 0; i--)
            dict.Add(i, i);

        var target = new KeyValuePair<int, int>[52];
        dict.CopyTo(target, 2);

        Assert.Equal(Enumerable.Range(0, 50), target.Skip(2).Select(e => e.Key));
    }

    [Fact]
    public void CopyTo_ShouldThrow_WhenTargetIsInvalid()
    {
        var dict = new BTreeDictionary<int, int>();
        dict.Add(1, 1);

        Assert.Throws<ArgumentNullException>(() => dict.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => dict.CopyTo(new KeyValuePair<int, int>[4], -1));
        Assert.Throws<ArgumentException>(() => dict.CopyTo(new KeyValuePair<int, int>[1], 1));
    }

    [Fact]
    public void IDictionary_ShouldExposeTheSameEntries()
    {
        IDictionary<int, int> dict = new BTreeDictionary<int, int>();

        dict.Add(2, 20);
        dict.Add(new KeyValuePair<int, int>(1, 10));

        Assert.False(dict.IsReadOnly);
        Assert.Equal(new[] { 1, 2 }, dict.Keys);
        Assert.Equal(new[] { 10, 20 }, dict.Values);
        Assert.True(dict.Contains(new KeyValuePair<int, int>(1, 10)));
        Assert.False(dict.Contains(new KeyValuePair<int, int>(1, 99)));
        Assert.True(dict.ContainsKey(2));
        Assert.True(dict.TryGetValue(2, out int value));
        Assert.Equal(20, value);

        // The interface-typed indexer is declared TValue? and forwards to the primary one.
        Assert.Equal(10, dict[1]);
        dict[1] = 11;
        dict[3] = 30;
        Assert.Equal(11, dict[1]);
        Assert.Equal(30, dict[3]);
        Assert.True(dict.Remove(3));
        Assert.Throws<KeyNotFoundException>(() => _ = dict[3]);
    }

    [Fact]
    public void IDictionaryRemove_ShouldOnlyRemoveMatchingPairs()
    {
        ICollection<KeyValuePair<int, int>> dict = new BTreeDictionary<int, int>();
        dict.Add(new KeyValuePair<int, int>(1, 10));

        Assert.False(dict.Remove(new KeyValuePair<int, int>(1, 99)));
        Assert.False(dict.Remove(new KeyValuePair<int, int>(2, 10)));
        Assert.Equal(1, dict.Count);

        Assert.True(dict.Remove(new KeyValuePair<int, int>(1, 10)));
        Assert.Equal(0, dict.Count);
    }

    [Fact]
    public void IReadOnlyDictionary_ShouldExposeTheSameEntries()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 40; i++)
            dict.Add(i, i * 2);

        IReadOnlyDictionary<int, int> view = dict;

        Assert.Equal(40, view.Count);
        Assert.Equal(Enumerable.Range(0, 40), view.Keys);
        Assert.Equal(Enumerable.Range(0, 40).Select(i => i * 2), view.Values);
        Assert.Equal(20, view[10]);
        Assert.True(view.ContainsKey(39));
        Assert.True(view.TryGetValue(39, out int value));
        Assert.Equal(78, value);
    }

    [Fact]
    public void Comparer_ShouldReportTheConfiguredComparer()
    {
        var dict = new BTreeDictionary<int, int>();

        Assert.Equal(default(DefaultComparer<int>), dict.Comparer);
        Assert.True(dict.Comparer.Compare(1, 2) < 0);
    }
}
