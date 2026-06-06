using System.Collections;
using System.Collections.Generic;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Issue #29 — code coverage improvements.
//
// These tests deliberately target the corners the behavioural suites leave
// uncovered: the non-generic IEnumerable / IEnumerator surface that only the
// BCL itself usually pokes at (foreach over an IEnumerable reference,
// object IEnumerator.Current, IEnumerator.Reset), plus the throw / early-return
// branches that the happy-path tests never reach (indexer miss on the
// out-of-band default/zero/null key, Clear() on an already-empty collection,
// MultiMap removals of keys and values that were never there).
//
// They are intentionally exhaustive across every collection type so that adding
// a new dictionary/set re-uses the same helpers and the same guarantees.
public class EdgeCaseCoverageTests
{
    // Drives a struct view's boxed enumerator through the non-generic
    // IEnumerable.GetEnumerator() / object IEnumerator.Current / Reset() path and
    // asserts a second pass after Reset() yields the same sequence. Returns the
    // first-pass items so callers can assert on contents.
    private static List<object?> DrainTwiceViaNonGeneric(IEnumerable view)
    {
        IEnumerator e = view.GetEnumerator();

        var first = new List<object?>();
        while (e.MoveNext())
            first.Add(e.Current);

        e.Reset();

        var second = new List<object?>();
        while (e.MoveNext())
            second.Add(e.Current);

        Assert.Equal(first, second);
        return first;
    }

    // ---- CelerityDictionary -------------------------------------------------

    [Fact]
    public void CelerityDictionary_Indexer_ShouldThrow_ForAbsentDefaultKey()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();

        // default(int) == 0 takes the out-of-band IsDefaultKey path; with no
        // default key stored the getter must throw rather than return default(T).
        Assert.Throws<KeyNotFoundException>(() => _ = map[0]);
    }

    [Fact]
    public void CelerityDictionary_Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();

        map.Clear(); // _count == 0 early-return branch

        Assert.Empty(map);
    }

    [Fact]
    public void CelerityDictionary_KeysAndValues_ShouldRoundTrip_ViaNonGenericEnumerator()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;

        var keys = DrainTwiceViaNonGeneric(map.Keys);
        var values = DrainTwiceViaNonGeneric(map.Values);

        Assert.Equal(new object?[] { 1, 2, 3, 4, 5 }, Sorted(keys));
        Assert.Equal(new object?[] { 10, 20, 30, 40, 50 }, Sorted(values));
    }

    // ---- IntDictionary ------------------------------------------------------

    [Fact]
    public void IntDictionary_Indexer_ShouldThrow_ForAbsentZeroKey()
    {
        var map = new IntDictionary<int>();

        Assert.Throws<KeyNotFoundException>(() => _ = map[0]);
    }

    [Fact]
    public void IntDictionary_Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var map = new IntDictionary<int>();

        map.Clear();

        Assert.Empty(map.Keys);
    }

    [Fact]
    public void IntDictionary_KeysAndValues_ShouldRoundTrip_ViaNonGenericEnumerator()
    {
        var map = new IntDictionary<int>();
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;

        var keys = DrainTwiceViaNonGeneric(map.Keys);
        var values = DrainTwiceViaNonGeneric(map.Values);

        Assert.Equal(new object?[] { 1, 2, 3, 4, 5 }, Sorted(keys));
        Assert.Equal(new object?[] { 10, 20, 30, 40, 50 }, Sorted(values));
    }

    // ---- LongDictionary -----------------------------------------------------

    [Fact]
    public void LongDictionary_Indexer_ShouldThrow_ForAbsentZeroKey()
    {
        var map = new LongDictionary<int>();

        Assert.Throws<KeyNotFoundException>(() => _ = map[0L]);
    }

    [Fact]
    public void LongDictionary_Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var map = new LongDictionary<int>();

        map.Clear();

        Assert.Empty(map.Keys);
    }

    [Fact]
    public void LongDictionary_KeysAndValues_ShouldRoundTrip_ViaNonGenericEnumerator()
    {
        var map = new LongDictionary<int>();
        for (long i = 1; i <= 5; i++)
            map[i] = (int)(i * 10);

        var keys = DrainTwiceViaNonGeneric(map.Keys);
        var values = DrainTwiceViaNonGeneric(map.Values);

        Assert.Equal(new object?[] { 1L, 2L, 3L, 4L, 5L }, Sorted(keys));
        Assert.Equal(new object?[] { 10, 20, 30, 40, 50 }, Sorted(values));
    }

    // ---- FrozenCelerityDictionary ------------------------------------------

    [Fact]
    public void FrozenCelerityDictionary_Indexer_ShouldThrow_ForAbsentNullKey()
    {
        var frozen = new FrozenCelerityDictionary<int>(new[]
        {
            new KeyValuePair<string, int>("a", 1),
        });

        Assert.Throws<KeyNotFoundException>(() => _ = frozen[null!]);
    }

    [Fact]
    public void FrozenCelerityDictionary_KeysAndValues_ShouldRoundTrip_ViaNonGenericEnumerator()
    {
        var frozen = new FrozenCelerityDictionary<int>(new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("c", 3),
        });

        var keys = DrainTwiceViaNonGeneric(frozen.Keys);
        var values = DrainTwiceViaNonGeneric(frozen.Values);

        Assert.Equal(new object?[] { "a", "b", "c" }, Sorted(keys));
        Assert.Equal(new object?[] { 1, 2, 3 }, Sorted(values));
    }

    [Fact]
    public void FrozenCelerityDictionary_Enumerator_ShouldRoundTrip_AfterReset()
    {
        var frozen = new FrozenCelerityDictionary<int>(new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
        });

        // The pair-level enumerator implements IEnumerator<KeyValuePair<,>>; drive
        // it through the boxed IEnumerable surface to reach Reset() and the
        // non-generic Current.
        var pairs = DrainTwiceViaNonGeneric((IEnumerable)frozen);

        Assert.Equal(2, pairs.Count);
    }

    // ---- CelerityMultiMap ---------------------------------------------------

    [Fact]
    public void CelerityMultiMap_Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();

        map.Clear();

        Assert.Empty(map.Keys);
        Assert.Equal(0, map.ValueCount);
    }

    [Fact]
    public void CelerityMultiMap_Remove_ShouldReturnFalse_ForAbsentDefaultKey()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();

        // default key (0) group never created.
        Assert.False(map.Remove(0, 123));
        Assert.False(map.RemoveAll(0));
    }

    [Fact]
    public void CelerityMultiMap_Remove_ShouldReturnFalse_ForAbsentValueUnderDefaultKey()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(0, 1);
        map.Add(0, 2);

        Assert.False(map.Remove(0, 999)); // value not in the default-key group
        Assert.True(map.Remove(0, 1));
        Assert.True(map.Remove(0, 2));    // empties the group -> drops the key
        Assert.False(map.ContainsKey(0));
    }

    [Fact]
    public void CelerityMultiMap_Remove_ShouldReturnFalse_ForAbsentValueUnderPresentKey()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(7, 1);

        Assert.False(map.Remove(7, 999)); // key present, value absent
        Assert.True(map.ContainsKey(7));
    }

    [Fact]
    public void CelerityMultiMap_KeysAndValueGroups_ShouldRoundTrip_ViaNonGenericEnumerator()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(1, 10);
        map.Add(1, 11);
        map.Add(2, 20);

        var keys = DrainTwiceViaNonGeneric(map.Keys);
        Assert.Equal(new object?[] { 1, 2 }, Sorted(keys));

        // ValueGroup is an IReadOnlyList<TValue?> struct view; drive its boxed
        // enumerator (non-generic GetEnumerator/Current/Reset).
        var group = map[1];
        var groupItems = DrainTwiceViaNonGeneric(group);
        Assert.Equal(new object?[] { 10, 11 }, Sorted(groupItems));
    }

    [Fact]
    public void CelerityMultiMap_PairEnumerator_ShouldRoundTrip_AfterReset()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(0, 100); // default key, exercises the BeforeDefaultKey state
        map.Add(1, 10);
        map.Add(2, 20);

        // Grouping yields IGrouping<,>; drive the map's own boxed enumerator to
        // reach object IEnumerator.Current and the versioned Reset().
        IEnumerator e = ((IEnumerable)map).GetEnumerator();
        var firstKeys = new List<int>();
        while (e.MoveNext())
            firstKeys.Add(((IGrouping<int, int>)e.Current!).Key);

        e.Reset();

        var secondKeys = new List<int>();
        while (e.MoveNext())
            secondKeys.Add(((IGrouping<int, int>)e.Current!).Key);

        firstKeys.Sort();
        secondKeys.Sort();
        Assert.Equal(firstKeys, secondKeys);
        Assert.Equal(new[] { 0, 1, 2 }, firstKeys);
    }

    [Fact]
    public void CelerityMultiMap_Grouping_ShouldRoundTrip_ViaNonGenericEnumerator()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(5, 50);
        map.Add(5, 51);

        foreach (var grouping in map)
        {
            // Grouping.IEnumerable.GetEnumerator() (non-generic) + value drain.
            var items = DrainTwiceViaNonGeneric((IEnumerable)grouping);
            Assert.Equal(new object?[] { 50, 51 }, Sorted(items));
        }
    }

    /// <summary>A test-only hasher returning the key itself, for predictable slots.</summary>
    private struct IdentityIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => key;
    }

    [Fact]
    public void CelerityMultiMap_ValueGroup_ShouldEnumerate_ViaPublicStructEnumerator()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(1, 10);
        map.Add(1, 11);

        // foreach over the ValueGroup struct binds to its public GetEnumerator(),
        // distinct from the boxed IEnumerable path exercised elsewhere.
        int sum = 0;
        foreach (int? v in map[1])
            sum += v ?? 0;

        Assert.Equal(21, sum);
    }

    [Fact]
    public void CelerityMultiMap_EnumeratorReset_ShouldThrow_WhenMapMutated()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(1, 10);

        var e = map.GetEnumerator();
        e.MoveNext();
        map.Add(2, 20); // bumps the version

        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }

    [Fact]
    public void CelerityMultiMap_BackwardShift_ShouldKeepHomedKey_OnWrapAroundCluster()
    {
        // With an identity hasher and capacity 8, keys 1, 2, 9 form one probe
        // cluster: 1->slot1, 2->slot2, 9(home 1)->slot3. Removing key 1 forces the
        // backward shift to skip key 2 (already at its home slot — the bypassesGap
        // branch) while relocating key 9 into the freed slot.
        var map = new CelerityMultiMap<int, int, IdentityIntHasher>(8);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(9, 90);

        Assert.True(map.RemoveAll(1));

        Assert.False(map.ContainsKey(1));
        Assert.Equal(new[] { 20 }, ToIntArray(map[2]));
        Assert.Equal(new[] { 90 }, ToIntArray(map[9]));
        Assert.Equal(2, map.Count);
    }

    private static int[] ToIntArray(CelerityMultiMap<int, int, IdentityIntHasher>.ValueGroup group)
    {
        var list = new List<int>();
        foreach (int? v in group)
            list.Add(v ?? 0);
        return list.ToArray();
    }

    // Sorts a heterogeneous object list so set-style collections (whose
    // enumeration order is hash-bucket dependent) compare deterministically.
    private static object?[] Sorted(List<object?> items)
    {
        var copy = items.ToArray();
        Array.Sort(copy, (a, b) => Comparer<object>.Default.Compare(a!, b!));
        return copy;
    }
}
