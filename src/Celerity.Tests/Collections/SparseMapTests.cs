using System.Reflection;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Issue #473: SparseMap<TValue>, the dictionary half of the Briggs–Torczon sparse tier —
// a dense key array and a parallel dense value array, plus a sparse index array over a
// fixed universe.
//
// These tests mirror the dedicated SparseSetTests / EnumMapTests behavioural coverage,
// adapted for a bounded-universe *dictionary*: out-of-range keys throw on the write
// surface and read as absent on the read surface, the swap-remove must not orphan the
// value it moves, and Clear must leave the map reusable even though it never clears the
// key or sparse arrays (the sparse↔dense round-trip has to reject the stale entries left
// behind). The split Clear contract — O(1) for a TValue holding no references, and a
// dense-prefix clear otherwise — is pinned on both sides.
public class SparseMapTests
{
    [Fact]
    public void Add_ShouldStoreEntry()
    {
        var map = new SparseMap<string>(100);
        map.Add(10, "ten");

        Assert.True(map.ContainsKey(10));
        Assert.Equal("ten", map[10]);
        Assert.Single(map);
    }

    [Fact]
    public void Add_ShouldThrow_OnDuplicateKey()
    {
        var map = new SparseMap<string>(100);
        map.Add(5, "five");

        var ex = Assert.Throws<ArgumentException>(() => map.Add(5, "other"));
        Assert.Contains("5", ex.Message);
        Assert.Equal("key", ex.ParamName);
        Assert.Single(map);
        Assert.Equal("five", map[5]);
    }

    [Fact]
    public void TryAdd_ShouldReturnTrueThenFalse()
    {
        var map = new SparseMap<string>(100);

        Assert.True(map.TryAdd(3, "first"));
        Assert.False(map.TryAdd(3, "second"));
        Assert.Equal("first", map[3]);
        Assert.Single(map);
    }

    [Fact]
    public void ZeroKey_ShouldBeHandled_AsAnOrdinaryKey()
    {
        var map = new SparseMap<string>(16);
        map.Add(0, "zero");
        map.Add(1, "one");

        Assert.True(map.ContainsKey(0));
        Assert.Equal("zero", map[0]);
        Assert.Equal(2, map.Count);

        Assert.True(map.Remove(0, out string? removed));
        Assert.Equal("zero", removed);
        Assert.False(map.ContainsKey(0));
        Assert.True(map.ContainsKey(1));
    }

    [Fact]
    public void Add_ShouldStoreTopOfUniverse()
    {
        var map = new SparseMap<int>(8);
        map.Add(7, 70);

        Assert.True(map.ContainsKey(7));
        Assert.Equal(70, map[7]);
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Add(8, 80));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Add_ShouldThrow_WhenKeyOutOfRange(int key)
    {
        var map = new SparseMap<int>(100);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => map.Add(key, 1));
        Assert.Equal("key", ex.ParamName);
        Assert.Empty(map);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void TryAdd_ShouldThrow_WhenKeyOutOfRange(int key)
    {
        var map = new SparseMap<int>(100);

        Assert.Throws<ArgumentOutOfRangeException>(() => map.TryAdd(key, 1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void IndexerSet_ShouldThrow_WhenKeyOutOfRange(int key)
    {
        var map = new SparseMap<int>(100);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => map[key] = 1);
        Assert.Equal("key", ex.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void ContainsKey_ShouldReturnFalse_WhenKeyOutOfRange(int key)
    {
        var map = new SparseMap<int>(100);
        map.Add(1, 1);

        Assert.False(map.ContainsKey(key));
        Assert.False(map.TryGetValue(key, out _));
        Assert.False(map.Remove(key));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void IndexerGet_ShouldThrowKeyNotFound_WhenKeyOutOfRange(int key)
    {
        var map = new SparseMap<int>(100);

        Assert.Throws<KeyNotFoundException>(() => map[key]);
    }

    [Fact]
    public void IndexerGet_ShouldThrowKeyNotFound_ForAbsentInRangeKey()
    {
        var map = new SparseMap<int>(100);
        map.Add(1, 1);

        Assert.Throws<KeyNotFoundException>(() => map[2]);
    }

    [Fact]
    public void IndexerSet_ShouldInsertThenOverwrite_WithoutChangingCount()
    {
        var map = new SparseMap<string>(100);

        map[4] = "first";
        Assert.Single(map);
        Assert.Equal("first", map[4]);

        map[4] = "second";
        Assert.Single(map);
        Assert.Equal("second", map[4]);
    }

    [Fact]
    public void IndexerOverwrite_ShouldNotInvalidateAnActiveEnumerator()
    {
        // A pure value overwrite is not a structural change, so it must not bump the version —
        // matching Dictionary<,>. A new key, by contrast, does invalidate.
        var map = new SparseMap<string>(32) { [1] = "a", [2] = "b" };

        SparseMap<string>.Enumerator enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        map[1] = "overwritten";
        Assert.True(enumerator.MoveNext());

        SparseMap<string>.Enumerator second = map.GetEnumerator();
        Assert.True(second.MoveNext());
        map[3] = "c";
        Assert.Throws<InvalidOperationException>(() => second.MoveNext());
    }

    [Fact]
    public void TryGetValue_ShouldReportHitAndMiss()
    {
        var map = new SparseMap<string>(100) { [9] = "nine" };

        Assert.True(map.TryGetValue(9, out string? hit));
        Assert.Equal("nine", hit);

        Assert.False(map.TryGetValue(8, out string? miss));
        Assert.Null(miss);
    }

    [Fact]
    public void ContainsValue_ShouldFindStoredValues_AndRejectOthers()
    {
        var map = new SparseMap<string>(100);
        Assert.False(map.ContainsValue("anything"));

        map[1] = "alpha";
        map[2] = "beta";

        Assert.True(map.ContainsValue("alpha"));
        Assert.True(map.ContainsValue("beta"));
        Assert.False(map.ContainsValue("gamma"));
    }

    [Fact]
    public void ContainsValue_ShouldMatchNull_AndIgnoreRemovedSlots()
    {
        var map = new SparseMap<string?>(100);
        map[1] = null;
        Assert.True(map.ContainsValue(null));

        map[2] = "kept";
        Assert.True(map.Remove(1));

        // The removed entry's value must not linger in the scanned prefix.
        Assert.False(map.ContainsValue(null));
        Assert.True(map.ContainsValue("kept"));
    }

    [Fact]
    public void Remove_ShouldDeleteEntryAndMakeItUnreachable()
    {
        var map = new SparseMap<string>(100) { [10] = "ten" };

        Assert.True(map.Remove(10));
        Assert.False(map.ContainsKey(10));
        Assert.False(map.TryGetValue(10, out _));
        Assert.Empty(map);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        var map = new SparseMap<int>(100) { [1] = 1 };

        Assert.False(map.Remove(2, out int removed));
        Assert.Equal(0, removed);
        Assert.Single(map);
    }

    [Fact]
    public void Remove_ShouldNotOrphanOtherEntries_WhenRemovingFromTheMiddle()
    {
        // The swap-remove moves the last dense entry into the vacated slot; its *value* has to
        // move with it, and its sparse index has to be repointed.
        var map = new SparseMap<string>(64);
        for (int i = 0; i < 20; i++)
            map[i] = $"v{i}";

        Assert.True(map.Remove(5, out string? removed));
        Assert.Equal("v5", removed);

        Assert.Equal(19, map.Count);
        for (int i = 0; i < 20; i++)
        {
            if (i == 5)
            {
                Assert.False(map.ContainsKey(i));
                continue;
            }

            Assert.True(map.TryGetValue(i, out string? value));
            Assert.Equal($"v{i}", value);
        }
    }

    [Fact]
    public void Remove_ThenReinsert_ManyEntries_ShouldNotLoseAnything()
    {
        var map = new SparseMap<int>(200);
        for (int i = 0; i < 100; i++)
            map[i] = i * 3;

        for (int i = 0; i < 100; i += 2)
            Assert.True(map.Remove(i));

        for (int i = 0; i < 100; i += 2)
            map[i] = i * 5;

        Assert.Equal(100, map.Count);
        for (int i = 0; i < 100; i++)
            Assert.Equal(i % 2 == 0 ? i * 5 : i * 3, map[i]);
    }

    [Fact]
    public void DenseArrays_ShouldGrow_WhenManyEntriesAdded()
    {
        var map = new SparseMap<int>(1000);
        for (int i = 0; i < 500; i++)
            map[i] = i;

        Assert.Equal(500, map.Count);
        for (int i = 0; i < 500; i++)
            Assert.Equal(i, map[i]);
    }

    [Fact]
    public void FullUniverse_ShouldStoreEveryKey()
    {
        var map = new SparseMap<int>(64);
        for (int i = 0; i < 64; i++)
            map.Add(i, i);

        Assert.Equal(64, map.Count);
        for (int i = 0; i < 64; i++)
            Assert.Equal(i, map[i]);
    }

    [Fact]
    public void Clear_ShouldRemoveEveryEntry_AndLeaveTheMapReusable()
    {
        var map = new SparseMap<string>(100);
        for (int i = 0; i < 30; i++)
            map[i] = $"v{i}";

        map.Clear();

        Assert.Empty(map);
        for (int i = 0; i < 30; i++)
        {
            Assert.False(map.ContainsKey(i));
            Assert.False(map.TryGetValue(i, out _));
        }

        map[7] = "again";
        Assert.Single(map);
        Assert.Equal("again", map[7]);
        Assert.False(map.ContainsKey(6));
    }

    [Fact]
    public void Clear_ThenReaddSameKey_ShouldNotSeeTheOldValue()
    {
        // The stale sparse entry for key 3 still points at dense slot 0 after the clear; the
        // round-trip has to reject it until the key is genuinely re-added.
        var map = new SparseMap<string>(16) { [3] = "old" };

        map.Clear();
        Assert.False(map.ContainsKey(3));

        map[3] = "new";
        Assert.Equal("new", map[3]);
        Assert.Single(map);
    }

    [Fact]
    public void Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var map = new SparseMap<int>(16);

        map.Clear();
        map.Clear();

        Assert.Empty(map);
    }

    [Fact]
    public void Clear_ShouldReleaseValueReferences_ForAReferenceHoldingTValue()
    {
        // The split Clear contract, reference side: a TValue that can hold a reference must not
        // keep one past the call, so the dense value prefix is cleared.
        var map = new SparseMap<string>(64);
        for (int i = 0; i < 10; i++)
            map[i] = $"v{i}";

        map.Clear();

        string?[] values = ReadValues<string>(map);
        for (int i = 0; i < 10; i++)
            Assert.Null(values[i]);
    }

    [Fact]
    public void Clear_ShouldTouchNothing_ForAReferenceFreeTValue()
    {
        // The split Clear contract, O(1) side: with no reference to release there is nothing to
        // clear, so the stale values stay exactly where they were — unreachable, because the
        // round-trip rejects every slot once the count is zero.
        var map = new SparseMap<int>(64);
        for (int i = 0; i < 10; i++)
            map[i] = i + 1;

        map.Clear();

        int[] values = ReadValues<int>(map);
        for (int i = 0; i < 10; i++)
            Assert.Equal(i + 1, values[i]);

        Assert.Empty(map);
        Assert.False(map.ContainsKey(0));
    }

    [Fact]
    public void Constructor_ShouldExposeUniverse()
    {
        Assert.Equal(128, new SparseMap<int>(128).Universe);
        Assert.Equal(0, new SparseMap<int>(0).Universe);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenUniverseIsNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SparseMap<int>(-1));
        Assert.Equal("universe", ex.ParamName);
    }

    [Fact]
    public void ZeroUniverse_ShouldStoreNothing_AndRejectEveryKey()
    {
        var map = new SparseMap<int>(0);

        Assert.Empty(map);
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Add(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.TryAdd(0, 1));
        Assert.False(map.ContainsKey(0));
        Assert.False(map.Remove(0));
    }

    [Fact]
    public void SourceConstructor_ShouldCopyEveryEntry()
    {
        var source = new Dictionary<int, string> { [1] = "a", [5] = "b", [9] = "c" };
        var map = new SparseMap<string>(16, source);

        Assert.Equal(3, map.Count);
        Assert.Equal("a", map[1]);
        Assert.Equal("b", map[5]);
        Assert.Equal("c", map[9]);
    }

    [Fact]
    public void SourceConstructor_ShouldThrow_OnDuplicateKeys()
    {
        KeyValuePair<int, string>[] source =
        [
            new(2, "first"),
            new(2, "second"),
        ];

        Assert.Throws<ArgumentException>(() => new SparseMap<string>(16, source));
    }

    [Fact]
    public void SourceConstructor_ShouldThrow_WhenSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new SparseMap<int>(16, (IEnumerable<KeyValuePair<int, int>>)null!));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void SourceConstructor_NullSource_ShouldBeatNegativeUniverse()
    {
        // Argument-order contract (#94): a null source is reported before an invalid universe,
        // even though the universe is validated by the constructor this one chains to.
        Assert.Throws<ArgumentNullException>(
            () => new SparseMap<int>(-1, (IEnumerable<KeyValuePair<int, int>>)null!));
    }

    [Fact]
    public void SourceConstructor_ShouldThrow_WhenSourceHasOutOfRangeKey()
    {
        KeyValuePair<int, int>[] source = [new(1, 1), new(20, 20)];

        Assert.Throws<ArgumentOutOfRangeException>(() => new SparseMap<int>(16, source));
    }

    [Fact]
    public void SourceConstructor_ShouldAcceptALazyNonCollectionSource()
    {
        // A non-ICollection source skips the pre-sizing path, so the dense arrays grow as they
        // fill instead.
        IEnumerable<KeyValuePair<int, int>> Lazy()
        {
            for (int i = 0; i < 25; i++)
                yield return new KeyValuePair<int, int>(i, i * 2);
        }

        var map = new SparseMap<int>(64, Lazy());

        Assert.Equal(25, map.Count);
        for (int i = 0; i < 25; i++)
            Assert.Equal(i * 2, map[i]);
    }

    [Fact]
    public void SourceConstructor_ShouldAcceptAnEmptyCollectionSource()
    {
        // Count 0 asks for no pre-sizing at all, so the dense arrays stay where the primary
        // constructor left them.
        var map = new SparseMap<int>(16, new Dictionary<int, int>());

        Assert.Empty(map);
        Assert.Equal(0, map.EnsureCapacity(0));
    }

    [Fact]
    public void SourceConstructor_ShouldClampPreSizingToUniverse()
    {
        // A lying ICollection Count larger than the universe must not size the dense arrays past
        // the most entries the map could ever hold.
        var source = new OversizedCountSource(universeExceeding: 4096);

        var map = new SparseMap<int>(8, source);

        Assert.Equal(3, map.Count);
        Assert.Equal(8, map.EnsureCapacity(0));
    }

    [Fact]
    public void EnsureCapacity_ShouldGrowTheDenseArrays()
    {
        var map = new SparseMap<int>(1000);

        Assert.True(map.EnsureCapacity(500) >= 500);
        Assert.Empty(map);

        for (int i = 0; i < 500; i++)
            map[i] = i;

        Assert.Equal(500, map.Count);
    }

    [Fact]
    public void EnsureCapacity_ShouldClampToUniverse_AndNoOpWhenAlreadyLargeEnough()
    {
        var map = new SparseMap<int>(10);

        Assert.Equal(10, map.EnsureCapacity(1000));
        Assert.Equal(10, map.EnsureCapacity(5));
    }

    [Fact]
    public void EnsureCapacity_ShouldThrow_WhenNegative()
    {
        var map = new SparseMap<int>(10);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => map.EnsureCapacity(-1));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void TrimExcess_AfterShrink_ShouldPreserveContents()
    {
        var map = new SparseMap<string>(500);
        for (int i = 0; i < 200; i++)
            map[i] = $"v{i}";

        for (int i = 3; i < 200; i++)
            map.Remove(i);

        map.TrimExcess();

        Assert.Equal(3, map.Count);
        for (int i = 0; i < 3; i++)
            Assert.Equal($"v{i}", map[i]);

        map[100] = "later";
        Assert.Equal("later", map[100]);
        Assert.Equal(4, map.Count);
    }

    [Fact]
    public void TrimExcess_ShouldBeNoOp_WhenCapacityAlreadyMatches()
    {
        var map = new SparseMap<int>(10);
        map.EnsureCapacity(10);

        map.TrimExcess(10);

        Assert.Equal(10, map.EnsureCapacity(0));
    }

    [Fact]
    public void TrimExcess_ShouldThrow_WhenCapacityBelowCount()
    {
        var map = new SparseMap<int>(10) { [0] = 0, [1] = 1 };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => map.TrimExcess(1));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void TrimExcess_ShouldThrow_WhenCapacityAboveUniverse()
    {
        var map = new SparseMap<int>(10);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => map.TrimExcess(11));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void CopyTo_ShouldCopyEveryEntry()
    {
        var map = new SparseMap<string>(32) { [1] = "a", [2] = "b", [3] = "c" };

        var array = new KeyValuePair<int, string?>[5];
        map.CopyTo(array, 1);

        Assert.Equal(0, array[0].Key);
        Assert.Null(array[0].Value);
        var copied = new Dictionary<int, string?>();
        for (int i = 1; i <= 3; i++)
            copied[array[i].Key] = array[i].Value;

        Assert.Equal(3, copied.Count);
        Assert.Equal("a", copied[1]);
        Assert.Equal("b", copied[2]);
        Assert.Equal("c", copied[3]);
    }

    [Fact]
    public void KeysAndValues_ShouldExposeTheMapThroughReadOnlyViews()
    {
        var map = new SparseMap<string>(32) { [4] = "d", [8] = "h" };

        SparseMap<string>.KeyCollection keys = map.Keys;
        SparseMap<string>.ValueCollection values = map.Values;

        Assert.Equal(2, keys.Count);
        Assert.Equal(2, values.Count);
        Assert.True(keys.IsReadOnly);
        Assert.True(values.IsReadOnly);

        Assert.True(keys.Contains(4));
        Assert.False(keys.Contains(5));
        Assert.True(values.Contains("h"));
        Assert.False(values.Contains("z"));

        var seenKeys = new List<int>();
        foreach (int key in keys)
            seenKeys.Add(key);
        Assert.Equal(new[] { 4, 8 }, seenKeys.Order());

        var seenValues = new List<string?>();
        foreach (string? value in values)
            seenValues.Add(value);
        Assert.Equal(new[] { "d", "h" }, seenValues.Order());
    }

    [Fact]
    public void KeysAndValues_CopyTo_ShouldCopyInEnumerationOrder()
    {
        var map = new SparseMap<string>(32) { [4] = "d", [8] = "h" };

        var keys = new int[3];
        map.Keys.CopyTo(keys, 1);
        Assert.Equal(0, keys[0]);
        Assert.Equal(new[] { 4, 8 }, keys[1..].Order());

        var values = new string?[3];
        map.Values.CopyTo(values, 1);
        Assert.Null(values[0]);
        Assert.Equal(new[] { "d", "h" }, values[1..].Order());
    }

    [Fact]
    public void KeysAndValues_MutatingMembers_ShouldThrow()
    {
        var map = new SparseMap<string>(32) { [1] = "a" };

        ICollection<int> keys = map.Keys;
        Assert.Throws<NotSupportedException>(() => keys.Add(2));
        Assert.Throws<NotSupportedException>(() => keys.Clear());
        Assert.Throws<NotSupportedException>(() => keys.Remove(1));

        ICollection<string?> values = map.Values;
        Assert.Throws<NotSupportedException>(() => values.Add("b"));
        Assert.Throws<NotSupportedException>(() => values.Clear());
        Assert.Throws<NotSupportedException>(() => values.Remove("a"));
    }

    [Fact]
    public void ReadOnlyDictionarySurface_ShouldMirrorTheConcreteSurface()
    {
        IReadOnlyDictionary<int, string?> map = new SparseMap<string>(32) { [1] = "a", [2] = "b" };

        Assert.Equal("a", map[1]);
        Assert.Equal(new[] { 1, 2 }, map.Keys.Order());
        Assert.Equal(new[] { "a", "b" }, map.Values.Order());
        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey(2));
        Assert.True(map.TryGetValue(2, out string? value));
        Assert.Equal("b", value);
        Assert.Equal(2, map.Count());
    }

    [Fact]
    public void ExplicitDictionaryIndexer_ShouldGetAndSet()
    {
        IDictionary<int, string?> map = new SparseMap<string>(32);

        map[1] = "a";
        Assert.Equal("a", map[1]);

        map[1] = "b";
        Assert.Equal("b", map[1]);
    }

    // Reads the private dense value array, so the two halves of the Clear contract can be
    // asserted directly rather than inferred. The field is an implementation detail on purpose —
    // nothing in the public surface can distinguish "left behind but unreachable" from "cleared".
    private static TValue[] ReadValues<TValue>(SparseMap<TValue> map)
    {
        FieldInfo field = typeof(SparseMap<TValue>)
            .GetField("_values", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (TValue[])field.GetValue(map)!;
    }

    // An ICollection<KeyValuePair<,>> whose Count is far larger than the universe, so the
    // constructor's pre-sizing path has to clamp rather than allocate what it is told.
    private sealed class OversizedCountSource : ICollection<KeyValuePair<int, int>>
    {
        private readonly KeyValuePair<int, int>[] _items = [new(0, 0), new(1, 10), new(2, 20)];

        internal OversizedCountSource(int universeExceeding) => Count = universeExceeding;

        public int Count { get; }

        public bool IsReadOnly => true;

        public IEnumerator<KeyValuePair<int, int>> GetEnumerator()
            => ((IEnumerable<KeyValuePair<int, int>>)_items).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => _items.GetEnumerator();

        public void Add(KeyValuePair<int, int> item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(KeyValuePair<int, int> item) => throw new NotSupportedException();

        public void CopyTo(KeyValuePair<int, int>[] array, int arrayIndex) => throw new NotSupportedException();

        public bool Remove(KeyValuePair<int, int> item) => throw new NotSupportedException();
    }
}
