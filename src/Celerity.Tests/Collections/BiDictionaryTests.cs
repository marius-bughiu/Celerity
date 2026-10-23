using System.Collections;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Dedicated tests for BiDictionary, mirroring LinkedDictionaryTests for the shared dictionary
// contract and adding the one-to-one surface: value lookups (ContainsValue, TryGetKey, GetKey),
// value uniqueness on every write path, ForceSet in each of its branches, RemoveValue, the Inverse
// view, in-table storage of the default key and the default value, and the sizing rules (growth,
// EnsureCapacity, TrimExcess). Removal moves the last entry into the hole, so the order assertions
// here pin that swap-last order. The chain-walk stress (every key and value in one bucket) is in
// BiDictionaryCollisionTests.
public class BiDictionaryTests
{
    private static BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher> NewIntMap(
        int capacity = 16, float loadFactor = 0.75f) => new(capacity, loadFactor);

    private static BiDictionary<int, string, Int32WangNaiveHasher, StringFnV1AHasher> NewStringMap() => new();

    private static int[] KeysOf<TValue, TKeyHasher, TValueHasher>(BiDictionary<int, TValue, TKeyHasher, TValueHasher> map)
        where TKeyHasher : struct, IHashProvider<int>
        where TValueHasher : struct, IHashProvider<TValue>
    {
        var keys = new List<int>();
        foreach (var kvp in map)
            keys.Add(kvp.Key);
        return keys.ToArray();
    }

    // ---------------------------------------------------------------
    //  Shared dictionary contract (mirror of LinkedDictionaryTests)
    // ---------------------------------------------------------------

    [Fact]
    public void Indexer_ShouldInsertAndRetrieveValue()
    {
        var map = NewIntMap();
        map[10] = 100;

        Assert.Equal(100, map[10]);
        Assert.Equal(10, map.GetKey(100));
    }

    [Fact]
    public void Indexer_ShouldThrowException_WhenKeyDoesNotExist()
    {
        var map = NewIntMap();
        Assert.Throws<KeyNotFoundException>(() => { var value = map[99]; });
    }

    [Fact]
    public void Indexer_ShouldOverwriteExistingValue_AndFreeTheOldOne()
    {
        var map = NewIntMap();
        map[5] = 500;
        map[5] = 999; // Overwrite

        Assert.Equal(999, map[5]);
        Assert.Single(map);
        Assert.False(map.ContainsValue(500));
        Assert.True(map.ContainsValue(999));
        Assert.Equal(5, map.GetKey(999));

        // The freed value can now go to another key.
        map[6] = 500;
        Assert.Equal(6, map.GetKey(500));
    }

    [Fact]
    public void Indexer_ShouldBeANoOp_WhenSettingTheSamePair()
    {
        var map = NewIntMap();
        map[1] = 10;
        map[2] = 20;
        var enumerator = map.GetEnumerator();

        map[1] = 10;

        Assert.True(enumerator.MoveNext()); // no version bump
        Assert.Equal(2, map.Count);
        Assert.Equal(10, map[1]);
    }

    [Fact]
    public void Indexer_ShouldThrow_WhenTheValueBelongsToADifferentKey_LeavingTheMapUnchanged()
    {
        var map = NewIntMap();
        map[1] = 10;
        map[2] = 20;

        var ex = Assert.Throws<ArgumentException>(() => map[1] = 20); // existing key
        Assert.Equal("value", ex.ParamName);
        ex = Assert.Throws<ArgumentException>(() => map[3] = 10);     // new key
        Assert.Equal("value", ex.ParamName);

        Assert.Equal(2, map.Count);
        Assert.Equal(10, map[1]);
        Assert.Equal(20, map[2]);
        Assert.False(map.ContainsKey(3));
        Assert.Equal(1, map.GetKey(10));
        Assert.Equal(2, map.GetKey(20));
    }

    [Fact]
    public void ContainsKey_ShouldReturnTrue_WhenKeyExists()
    {
        var map = NewIntMap();
        map[7] = 700;

        Assert.True(map.ContainsKey(7));
    }

    [Fact]
    public void ContainsKey_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        var map = NewIntMap();
        Assert.False(map.ContainsKey(7));
    }

    [Fact]
    public void ContainsValue_ShouldReportPresence()
    {
        var map = NewStringMap();
        map[1] = "one";
        map[2] = "two";

        Assert.True(map.ContainsValue("one"));
        Assert.True(map.ContainsValue("two"));
        Assert.False(map.ContainsValue("three"));
        Assert.False(map.ContainsValue(null!));
    }

    [Fact]
    public void Remove_ShouldDeleteKeyAndValueAndMakeBothUnreachable()
    {
        var map = NewIntMap();
        map[7] = 700;

        Assert.True(map.Remove(7));
        Assert.False(map.ContainsKey(7));
        Assert.False(map.ContainsValue(700));

        Assert.Throws<KeyNotFoundException>(() => { var value = map[7]; });
        Assert.Throws<KeyNotFoundException>(() => map.GetKey(700));
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        var map = NewIntMap();
        Assert.False(map.Remove(7));
        Assert.False(map.Remove(7, out int value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void RemoveWithOut_ShouldReturnTheRemovedValue()
    {
        var map = NewStringMap();
        map[1] = "one";
        map[2] = "two";

        Assert.True(map.Remove(1, out string? removed));
        Assert.Equal("one", removed);
        Assert.False(map.ContainsValue("one"));
        Assert.Equal(new[] { 2 }, KeysOf(map));
    }

    [Fact]
    public void Map_ShouldResize_WhenThresholdExceeded()
    {
        var map = NewIntMap(16, loadFactor: 0.5f);
        for (int i = 1; i <= 40; i++)
            map[i] = i * 10; // forces at least one resize

        Assert.Equal(40, map.Count);
        for (int i = 1; i <= 40; i++)
        {
            Assert.Equal(i * 10, map[i]);
            Assert.Equal(i, map.GetKey(i * 10));
        }
        Assert.Equal(Enumerable.Range(1, 40), KeysOf(map));
    }

    [Fact]
    public void Indexer_ShouldHandleZeroIntKey()
    {
        var map = NewStringMap();
        map[0] = "zero";

        Assert.True(map.ContainsKey(0));
        Assert.Equal("zero", map[0]);
        Assert.Equal(0, map.GetKey("zero"));
        Assert.Single(map);
    }

    [Fact]
    public void Indexer_ShouldOverwriteZeroIntKey()
    {
        var map = NewStringMap();
        map[0] = "zero";
        map[0] = "still-zero";

        Assert.Single(map);
        Assert.Equal("still-zero", map[0]);
        Assert.False(map.ContainsValue("zero"));
    }

    [Fact]
    public void Remove_ShouldHandleZeroIntKey()
    {
        var map = NewStringMap();
        map[0] = "zero";
        map[1] = "one";

        Assert.True(map.Remove(0));
        Assert.False(map.ContainsKey(0));
        Assert.True(map.ContainsKey(1));
        Assert.Single(map);
        Assert.False(map.Remove(0));
    }

    [Fact]
    public void DefaultKeyAndValue_ShouldSurviveResize()
    {
        var map = NewIntMap(16, loadFactor: 0.5f);
        map[0] = -1;
        map[-2] = 0;
        for (int i = 1; i <= 40; i++)
            map[i] = i * 10; // triggers resize while the default-key and default-value entries are live.

        Assert.Equal(42, map.Count);
        Assert.Equal(-1, map[0]);
        Assert.Equal(0, map[-2]);
        Assert.Equal(0, map.GetKey(-1));
        Assert.Equal(-2, map.GetKey(0));
        Assert.Equal(400, map[40]);
    }

    [Fact]
    public void TryGetValue_ShouldReturnTrueAndValue_WhenKeyExists()
    {
        var map = NewStringMap();
        map[42] = "answer";
        map[0] = "zero";

        Assert.True(map.TryGetValue(42, out var v1));
        Assert.Equal("answer", v1);

        Assert.True(map.TryGetValue(0, out var v2));
        Assert.Equal("zero", v2);
    }

    [Fact]
    public void TryGetValue_ShouldReturnFalseAndDefault_WhenKeyMissing()
    {
        var map = NewStringMap();
        Assert.False(map.TryGetValue(42, out var v1));
        Assert.Null(v1);

        Assert.False(map.TryGetValue(0, out var v2));
        Assert.Null(v2);
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        var map = NewIntMap();
        for (int i = 0; i < 32; i++)
            map[i] = i * i;
        int capacity = map.EnsureCapacity(0);

        map.Clear();

        Assert.Empty(map);
        Assert.Equal(capacity, map.EnsureCapacity(0)); // Clear keeps the capacity
        for (int i = 0; i < 32; i++)
        {
            Assert.False(map.ContainsKey(i));
            Assert.False(map.ContainsValue(i * i));
        }

        map[5] = 500;
        map[0] = 100;
        Assert.Equal(2, map.Count);
        Assert.Equal(100, map[0]);
        Assert.Equal(500, map[5]);
        Assert.Equal(new[] { 5, 0 }, KeysOf(map));
    }

    [Fact]
    public void Indexer_ShouldHandleGuidEmptyKeyAndValue()
    {
        var map = new BiDictionary<Guid, Guid, GuidIdentityHasher, GuidIdentityHasher>();
        var other = Guid.NewGuid();
        map[Guid.Empty] = other;
        map[other] = Guid.Empty;

        Assert.True(map.ContainsKey(Guid.Empty));
        Assert.Equal(other, map[Guid.Empty]);
        Assert.Equal(Guid.Empty, map[other]);
        Assert.Equal(other, map.GetKey(Guid.Empty));
        Assert.Equal(Guid.Empty, map.GetKey(other));
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void Indexer_ShouldThrowKeyNotFound_ForAbsentDefaultKey()
    {
        var map = NewIntMap();

        Assert.Throws<KeyNotFoundException>(() => _ = map[0]);
        Assert.Throws<KeyNotFoundException>(() => map.GetKey(0));
    }

    [Fact]
    public void Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var map = NewIntMap();
        var enumerator = map.GetEnumerator();

        map.Clear(); // _count == 0 early-return path: no version bump

        Assert.Empty(map);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void TryAdd_ShouldReturnFalse_WhenKeyExists()
    {
        var map = NewIntMap();
        Assert.True(map.TryAdd(1, 10));
        Assert.False(map.TryAdd(1, 20));

        Assert.Equal(10, map[1]); // unchanged
        Assert.False(map.ContainsValue(20));
        Assert.Single(map);
    }

    [Fact]
    public void TryAdd_ShouldReturnFalse_WhenValueBelongsToAnotherKey()
    {
        var map = NewIntMap();
        Assert.True(map.TryAdd(1, 10));
        Assert.False(map.TryAdd(2, 10));

        Assert.False(map.ContainsKey(2));
        Assert.Equal(1, map.GetKey(10));
        Assert.Single(map);
    }

    [Fact]
    public void Add_ShouldThrow_WhenKeyExists()
    {
        var map = NewIntMap();
        map.Add(1, 10);

        var ex = Assert.Throws<ArgumentException>(() => map.Add(1, 20));
        Assert.Equal("key", ex.ParamName);
        Assert.False(map.ContainsValue(20));
    }

    [Fact]
    public void Add_ShouldThrow_WhenValueBelongsToAnotherKey()
    {
        var map = NewIntMap();
        map.Add(1, 10);

        var ex = Assert.Throws<ArgumentException>(() => map.Add(2, 10));
        Assert.Equal("value", ex.ParamName);
        Assert.False(map.ContainsKey(2));
        Assert.Single(map);
    }

    [Fact]
    public void Add_ShouldReportTheKeyConflict_WhenBothSidesConflict()
    {
        var map = NewIntMap();
        map.Add(1, 10);
        map.Add(2, 20);

        var ex = Assert.Throws<ArgumentException>(() => map.Add(1, 20));
        Assert.Equal("key", ex.ParamName);
    }

    // ---------------------------------------------------------------
    //  Constructor validation and sizing
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldRejectNegativeCapacity()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => NewIntMap(-1));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(float.NaN)]
    public void Constructor_ShouldRejectInvalidLoadFactor(float loadFactor)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => NewIntMap(16, loadFactor));
        Assert.Equal("loadFactor", ex.ParamName);
    }

    [Theory]
    [InlineData(0, 0.75f, 1)]     // one bucket, (int)0.75 == 0 → floor of one entry
    [InlineData(1, 0.75f, 1)]
    [InlineData(16, 0.75f, 12)]
    [InlineData(17, 0.75f, 24)]   // rounds up to 32 buckets
    [InlineData(16, 0.01f, 1)]    // (int)0.16 == 0 → floor of one entry
    [InlineData(64, 0.5f, 32)]
    public void Constructor_ShouldSizeEntriesFromBucketsAndLoadFactor(int capacity, float loadFactor, int expectedEntries)
    {
        var map = NewIntMap(capacity, loadFactor);

        Assert.Equal(expectedEntries, map.EnsureCapacity(0));
    }

    [Fact]
    public void SourceConstructor_ShouldPreserveSourceOrder_InBothDirections()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(5, "five"),
            new KeyValuePair<int, string>(0, "zero"),
            new KeyValuePair<int, string>(-3, "minus-three"),
            new KeyValuePair<int, string>(42, "answer"),
        };

        var map = new BiDictionary<int, string, Int32WangNaiveHasher, StringFnV1AHasher>(source);

        Assert.Equal(new[] { 5, 0, -3, 42 }, KeysOf(map));
        Assert.Equal(new[] { "five", "zero", "minus-three", "answer" }, map.Values.ToArray());
        foreach (var kvp in source)
            Assert.Equal(kvp.Key, map.GetKey(kvp.Value));
    }

    [Fact]
    public void SourceConstructor_ShouldRejectNullSource()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher>(null!, capacity: -1, loadFactor: 5f));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void SourceConstructor_ShouldRejectNegativeCapacity()
    {
        var source = new[] { new KeyValuePair<int, int>(1, 1) };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher>(source, capacity: -1));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void SourceConstructor_ShouldRejectDuplicateKey()
    {
        var source = new[] { new KeyValuePair<int, int>(1, 10), new KeyValuePair<int, int>(1, 20) };

        var ex = Assert.Throws<ArgumentException>(() =>
            new BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher>(source));
        Assert.Equal("key", ex.ParamName);
    }

    [Fact]
    public void SourceConstructor_ShouldRejectDuplicateValue()
    {
        var source = new[] { new KeyValuePair<int, int>(1, 10), new KeyValuePair<int, int>(2, 10) };

        var ex = Assert.Throws<ArgumentException>(() =>
            new BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher>(source));
        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void SourceConstructor_ShouldPresizeForCollectionSource()
    {
        var source = Enumerable.Range(1, 100).Select(i => new KeyValuePair<int, int>(i, -i)).ToArray();

        var map = new BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher>(source, capacity: 1);

        // Exactly 100 entries over MinTableSizeFor(100, 0.75) = 256 buckets, reached without growing.
        Assert.Equal(100, map.EnsureCapacity(0));
        Assert.Equal(Enumerable.Range(1, 100), KeysOf(map));
    }

    [Fact]
    public void SourceConstructor_ShouldKeepLargerCapacity_WhenSourceIsSmaller()
    {
        var source = new[] { new KeyValuePair<int, int>(1, 1) };

        var map = new BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher>(source, capacity: 64);

        Assert.Equal(48, map.EnsureCapacity(0));
    }

    [Fact]
    public void SourceConstructor_ShouldAcceptAnEmptyCollection()
    {
        var map = new BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher>(
            new List<KeyValuePair<int, int>>(), capacity: 4);

        Assert.Empty(map);
        Assert.Equal(3, map.EnsureCapacity(0));
    }

    [Fact]
    public void SourceConstructor_ShouldNotPresize_ForLazyEnumerable()
    {
        static IEnumerable<KeyValuePair<int, int>> Lazy()
        {
            for (int i = 1; i <= 100; i++)
                yield return new KeyValuePair<int, int>(i, i + 1000);
        }

        var map = new BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher>(Lazy(), capacity: 1);

        Assert.Equal(100, map.Count);
        Assert.Equal(Enumerable.Range(1, 100), KeysOf(map));
        Assert.Equal(50, map.GetKey(1050));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(1f)]
    [InlineData(2f)]
    [InlineData(float.NaN)]
    public void SourceConstructor_ShouldRejectInvalidLoadFactor_ForCollectionSource(float loadFactor)
    {
        // A non-empty ICollection source takes the sizing helper's "count > 0" arm with a load
        // factor it must not size with; the primary constructor then rejects it.
        var source = new List<KeyValuePair<int, int>> { new(1, 1), new(2, 2) };

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher>(source, capacity: 16, loadFactor: loadFactor));
        Assert.Equal("loadFactor", ex.ParamName);
    }

    // ---------------------------------------------------------------
    //  Enumeration order: insertion order, then swap-last on removal
    // ---------------------------------------------------------------

    [Fact]
    public void Enumeration_ShouldFollowInsertionOrder_NotHashOrder()
    {
        var map = NewIntMap();
        int[] keys = { 90, 3, 57, -12, 1000, 0, 8, 41 };
        foreach (int k in keys)
            map.Add(k, k * 2);

        Assert.Equal(keys, KeysOf(map));
        Assert.Equal(keys.Select(k => k * 2).ToArray(), map.Values.ToArray());
    }

    [Fact]
    public void Remove_ShouldMoveTheLastEntryIntoTheHole()
    {
        var map = NewIntMap();
        for (int i = 1; i <= 6; i++)
            map[i] = i * 10;

        Assert.True(map.Remove(2));  // 6 moves into slot 1
        Assert.Equal(new[] { 1, 6, 3, 4, 5 }, KeysOf(map));
        Assert.True(map.Remove(5));  // already last: nothing moves
        Assert.Equal(new[] { 1, 6, 3, 4 }, KeysOf(map));
        Assert.True(map.Remove(1));  // 4 moves into slot 0
        Assert.Equal(new[] { 4, 6, 3 }, KeysOf(map));

        // Every moved entry stays reachable from both sides.
        foreach (int k in new[] { 3, 4, 6 })
        {
            Assert.Equal(k * 10, map[k]);
            Assert.Equal(k, map.GetKey(k * 10));
        }
    }

    [Fact]
    public void IndexerOverwrite_ShouldKeepTheKeysPosition()
    {
        var map = NewIntMap();
        for (int i = 1; i <= 4; i++)
            map[i] = i;

        map[2] = 200;
        map[1] = 100;

        Assert.Equal(new[] { 1, 2, 3, 4 }, KeysOf(map));
        Assert.Equal(new[] { 100, 200, 3, 4 }, map.Values.ToArray());
    }

    [Fact]
    public void TryAddDuplicate_ShouldKeepTheKeysPositionAndValue()
    {
        var map = NewIntMap();
        map[1] = 1;
        map[2] = 2;

        Assert.False(map.TryAdd(1, 100));
        Assert.False(map.TryAdd(3, 2));

        Assert.Equal(new[] { 1, 2 }, KeysOf(map));
        Assert.Equal(1, map[1]);
    }

    // ---------------------------------------------------------------
    //  Inverse lookups: TryGetKey, GetKey, RemoveValue
    // ---------------------------------------------------------------

    [Fact]
    public void TryGetKey_ShouldReturnTheOwningKey()
    {
        var map = NewStringMap();
        map[1] = "one";
        map[0] = "zero";

        Assert.True(map.TryGetKey("one", out int k1));
        Assert.Equal(1, k1);
        Assert.True(map.TryGetKey("zero", out int k0));
        Assert.Equal(0, k0);
    }

    [Fact]
    public void TryGetKey_ShouldReturnFalseAndDefault_WhenValueMissing()
    {
        var map = new BiDictionary<string, int, StringFnV1AHasher, Int32WangNaiveHasher>();
        Assert.False(map.TryGetKey(5, out string? missing));
        Assert.Null(missing);

        map["a"] = 1;
        Assert.False(map.TryGetKey(5, out missing));
        Assert.Null(missing);
        Assert.False(map.TryGetKey(0, out missing)); // the default value, absent
        Assert.Null(missing);
    }

    [Fact]
    public void GetKey_ShouldThrowKeyNotFound_WhenValueMissing()
    {
        var map = NewStringMap();
        map[1] = "one";

        Assert.Equal(1, map.GetKey("one"));
        Assert.Throws<KeyNotFoundException>(() => map.GetKey("two"));
    }

    [Fact]
    public void RemoveValue_ShouldRemoveTheOwningEntry()
    {
        var map = NewStringMap();
        map[1] = "one";
        map[2] = "two";
        map[3] = "three";

        Assert.True(map.RemoveValue("one")); // 3 moves into slot 0
        Assert.False(map.ContainsKey(1));
        Assert.False(map.ContainsValue("one"));
        Assert.Equal(new[] { 3, 2 }, KeysOf(map));

        Assert.True(map.RemoveValue("two", out int key));
        Assert.Equal(2, key);
        Assert.Equal(new[] { 3 }, KeysOf(map));
        Assert.Equal("three", map[3]);
    }

    [Fact]
    public void RemoveValue_ShouldReturnFalse_WhenValueMissing()
    {
        var map = NewStringMap();
        Assert.False(map.RemoveValue("x"));

        map[1] = "one";
        var enumerator = map.GetEnumerator();
        Assert.False(map.RemoveValue("x"));
        Assert.False(map.RemoveValue("x", out int key));
        Assert.Equal(0, key);
        Assert.True(enumerator.MoveNext()); // a miss is not a modification
        Assert.Single(map);
    }

    [Fact]
    public void RemoveValue_ThenReAdd_ShouldReuseTheValue()
    {
        var map = NewIntMap();
        map[1] = 10;
        Assert.True(map.RemoveValue(10));

        map[2] = 10;
        Assert.Equal(2, map.GetKey(10));
        Assert.False(map.ContainsKey(1));
    }

    // ---------------------------------------------------------------
    //  ForceSet — every branch
    // ---------------------------------------------------------------

    [Fact]
    public void ForceSet_ShouldAppendANewPair_WhenNeitherSideIsPresent()
    {
        var map = NewIntMap();
        map[1] = 10;
        map[2] = 20;

        Assert.False(map.ForceSet(3, 30));

        Assert.Equal(new[] { 1, 2, 3 }, KeysOf(map));
        Assert.Equal(30, map[3]);
        Assert.Equal(3, map.GetKey(30));
    }

    [Fact]
    public void ForceSet_ShouldRebindAnExistingKey_WhenTheValueIsFree()
    {
        var map = NewIntMap();
        map[1] = 10;
        map[2] = 20;

        Assert.False(map.ForceSet(1, 11));

        Assert.Equal(new[] { 1, 2 }, KeysOf(map)); // keeps its position
        Assert.Equal(11, map[1]);
        Assert.False(map.ContainsValue(10));
        Assert.Equal(1, map.GetKey(11));
    }

    [Fact]
    public void ForceSet_ShouldBeANoOp_WhenThePairIsAlreadyPresent()
    {
        var map = NewIntMap();
        map[1] = 10;
        map[2] = 20;
        var enumerator = map.GetEnumerator();

        Assert.False(map.ForceSet(1, 10));

        Assert.True(enumerator.MoveNext()); // no version bump
        Assert.Equal(2, map.Count);
        Assert.Equal(10, map[1]);
    }

    [Fact]
    public void ForceSet_ShouldEvictTheOtherKey_WhenTheKeyExistsAndIsNotLast()
    {
        var map = NewIntMap();
        map[1] = 10;
        map[2] = 20;
        map[3] = 30;

        // Key 1 (slot 0) takes value 20 from key 2 (slot 1); key 3 moves into slot 1.
        Assert.True(map.ForceSet(1, 20));

        Assert.Equal(new[] { 1, 3 }, KeysOf(map));
        Assert.Equal(20, map[1]);
        Assert.Equal(30, map[3]);
        Assert.False(map.ContainsKey(2));
        Assert.False(map.ContainsValue(10));
        Assert.Equal(1, map.GetKey(20));
        Assert.Equal(3, map.GetKey(30));
    }

    [Fact]
    public void ForceSet_ShouldFollowTheKeysEntry_WhenItWasLastAndMovedIntoTheHole()
    {
        var map = NewIntMap();
        map[1] = 10;
        map[2] = 20;
        map[3] = 30;

        // Key 3 (the last slot) takes value 10 from key 1 (slot 0). Removing key 1 moves key 3 into
        // slot 0, so the rebind must follow it there.
        Assert.True(map.ForceSet(3, 10));

        Assert.Equal(new[] { 3, 2 }, KeysOf(map));
        Assert.Equal(10, map[3]);
        Assert.Equal(20, map[2]);
        Assert.False(map.ContainsKey(1));
        Assert.False(map.ContainsValue(30));
        Assert.Equal(3, map.GetKey(10));
        Assert.Equal(2, map.GetKey(20));
    }

    [Fact]
    public void ForceSet_ShouldFollowTheKeysEntry_OnTwoEntries()
    {
        var map = NewIntMap();
        map[1] = 10;
        map[2] = 20;

        Assert.True(map.ForceSet(2, 10));

        Assert.Equal(new[] { 2 }, KeysOf(map));
        Assert.Equal(10, map[2]);
        Assert.Equal(2, map.GetKey(10));
        Assert.False(map.ContainsValue(20));
    }

    [Fact]
    public void ForceSet_ShouldEvictTheLastEntry_WithoutMovingAnything()
    {
        var map = NewIntMap();
        map[1] = 10;
        map[2] = 20;

        // The evicted entry is the last one, so nothing moves and key 1 stays at slot 0.
        Assert.True(map.ForceSet(1, 20));

        Assert.Equal(new[] { 1 }, KeysOf(map));
        Assert.Equal(20, map[1]);
        Assert.False(map.ContainsValue(10));
    }

    [Fact]
    public void ForceSet_ShouldEvictTheOtherKey_WhenTheKeyIsNew()
    {
        var map = NewIntMap();
        map[1] = 10;
        map[2] = 20;

        Assert.True(map.ForceSet(5, 10));

        Assert.Equal(new[] { 2, 5 }, KeysOf(map));
        Assert.Equal(10, map[5]);
        Assert.Equal(5, map.GetKey(10));
        Assert.False(map.ContainsKey(1));
    }

    [Fact]
    public void ForceSet_ShouldEvictTheOnlyEntry_WhenTheKeyIsNew()
    {
        var map = NewIntMap();
        map[1] = 10;

        Assert.True(map.ForceSet(2, 10));

        Assert.Equal(new[] { 2 }, KeysOf(map));
        Assert.Equal(2, map.GetKey(10));
    }

    [Fact]
    public void ForceSet_ShouldGrowTheTable_WhenAppendingToAFullTable()
    {
        var map = NewIntMap(capacity: 4); // 3 entries
        for (int i = 1; i <= 3; i++)
            map[i] = i;

        Assert.False(map.ForceSet(4, 4));

        Assert.Equal(6, map.EnsureCapacity(0));
        Assert.Equal(new[] { 1, 2, 3, 4 }, KeysOf(map));
    }

    // ---------------------------------------------------------------
    //  Default key and default value handling
    // ---------------------------------------------------------------

    [Fact]
    public void NullStringKeyAndValue_ShouldNeverReachTheHasher()
    {
        // NullRejectingStringHasher throws on null, so any null call that reached it would fail.
        var map = new BiDictionary<string, string, NullRejectingStringHasher, NullRejectingStringHasher>();
        map["a"] = "A";
        map[null!] = "N";
        map["v"] = null!;

        Assert.Equal(new[] { "a", null, "v" }, map.Keys.ToArray());
        Assert.True(map.ContainsKey(null!));
        Assert.True(map.ContainsValue(null!));
        Assert.Equal("N", map[null!]);
        Assert.Equal("v", map.GetKey(null!));
        Assert.True(map.TryGetValue(null!, out string? nv));
        Assert.Equal("N", nv);
        Assert.True(map.TryGetKey(null!, out string? nk));
        Assert.Equal("v", nk);
        Assert.False(map.TryAdd(null!, "x"));
        Assert.False(map.TryAdd("x", null!));
        Assert.Throws<ArgumentException>(() => map["x"] = null!);

        // Growth and rebuilds re-hash every entry, but must skip the hasher for null.
        for (int i = 0; i < 40; i++)
            map[$"k{i}"] = $"v{i}";
        map.TrimExcess();
        Assert.Equal("N", map[null!]);
        Assert.Equal("v", map.GetKey(null!));

        // Rebinding away from a null value re-hashes the old (null) value to unlink it.
        map["v"] = "V";
        Assert.False(map.ContainsValue(null!));
        Assert.Equal("v", map.GetKey("V"));

        // ForceSet evicting the null key re-hashes the evicted (null) key.
        Assert.True(map.ForceSet("a", "N"));
        Assert.False(map.ContainsKey(null!));
        Assert.Equal("a", map.GetKey("N"));

        // A removal that moves an entry with a null key and a null value into the hole.
        map[null!] = null!;
        Assert.True(map.Remove("a"));
        Assert.True(map.ContainsKey(null!));
        Assert.Null(map[null!]);
        Assert.Null(map.GetKey(null!));

        Assert.True(map.RemoveValue(null!, out string? removedKey));
        Assert.Null(removedKey);
        Assert.False(map.ContainsKey(null!));
        Assert.False(map.ContainsValue(null!));
        Assert.False(map.Remove(null!));
        Assert.False(map.RemoveValue(null!));
    }

    [Fact]
    public void NullStringKey_MappedToNullValue_ShouldRoundTrip()
    {
        var map = new BiDictionary<string, string, NullRejectingStringHasher, NullRejectingStringHasher>();
        map[null!] = null!;

        Assert.Single(map);
        Assert.Null(map[null!]);
        Assert.False(map.ForceSet(null!, null!)); // same pair
        Assert.Throws<ArgumentException>(() => map.Add(null!, "x"));
        Assert.Throws<ArgumentException>(() => map.Add("x", null!));
        Assert.True(map.Remove(null!, out string? removed));
        Assert.Null(removed);
        Assert.Empty(map);
    }

    [Fact]
    public void NullableKeyAndValue_WithoutValue_ShouldNeverReachTheHasher()
    {
        var map = new BiDictionary<int?, int?, NullRejectingNullableIntHasher, NullRejectingNullableIntHasher>();
        map[1] = 10;
        map[null] = 20;
        map[2] = null;

        Assert.Equal(new int?[] { 1, null, 2 }, map.Keys.ToArray());
        Assert.Equal(20, map[null]);
        Assert.Equal(2, map.GetKey(null));
        Assert.True(map.Remove(null));
        Assert.False(map.ContainsKey(null));
        Assert.True(map.RemoveValue(null));
        Assert.False(map.ContainsValue(null));
        Assert.Equal(new int?[] { 1 }, map.Keys.ToArray());
    }

    [Fact]
    public void DefaultIntKeyAndValue_ShouldNeverReachTheHasher()
    {
        var map = new BiDictionary<int, int, DefaultRejectingIntHasher, DefaultRejectingIntHasher>();
        map[5] = 0;
        map[0] = 5;

        Assert.True(map.ContainsKey(0));
        Assert.True(map.ContainsValue(0));
        Assert.Equal(5, map.GetKey(0));
        Assert.Equal(0, map.GetKey(5));

        map[5] = 7; // rebind away from the default value
        Assert.False(map.ContainsValue(0));

        Assert.True(map.ForceSet(0, 7)); // evicts key 5
        Assert.Equal(new[] { 0 }, KeysOf(map));
        Assert.True(map.Remove(0));
        Assert.Empty(map);
    }

    // ---------------------------------------------------------------
    //  Growth, EnsureCapacity, TrimExcess
    // ---------------------------------------------------------------

    [Fact]
    public void Growth_WithTinyLoadFactor_ShouldDoubleSeveralTimesInOneGrow()
    {
        // 16 buckets at 0.01 hold max(1, (int)0.16) = 1 entry. The second add must double
        // 16 → 32 → 64 → 128 → 256 before (int)(256 * 0.01) = 2 exceeds Count.
        var map = NewIntMap(capacity: 16, loadFactor: 0.01f);
        Assert.Equal(1, map.EnsureCapacity(0));

        map[1] = -1;
        Assert.Equal(1, map.EnsureCapacity(0));
        map[2] = -2;
        Assert.Equal(2, map.EnsureCapacity(0));

        for (int i = 3; i <= 50; i++)
            map[i] = -i;

        Assert.Equal(50, map.Count);
        Assert.Equal(Enumerable.Range(1, 50), KeysOf(map));
        for (int i = 1; i <= 50; i++)
        {
            Assert.Equal(-i, map[i]);
            Assert.Equal(i, map.GetKey(-i));
        }
    }

    [Fact]
    public void Growth_ShouldDoubleOnce_AtAnOrdinaryLoadFactor()
    {
        var map = NewIntMap(capacity: 4, loadFactor: 0.75f);
        Assert.Equal(3, map.EnsureCapacity(0));

        for (int i = 1; i <= 4; i++)
            map[i] = i;

        Assert.Equal(6, map.EnsureCapacity(0));
        Assert.Equal(new[] { 1, 2, 3, 4 }, KeysOf(map));
    }

    [Fact]
    public void RemoveThenAdd_ShouldReuseTheFreedSlot_WithoutGrowing()
    {
        var map = NewIntMap(capacity: 16);
        for (int i = 1; i <= 12; i++)
            map[i] = i; // exactly full: 12 entries
        Assert.Equal(12, map.EnsureCapacity(0));

        for (int round = 0; round < 100; round++)
        {
            int victim = (round % 12) + 1 + (round / 12) * 100;
            int replacement = victim + 100;
            Assert.True(map.Remove(victim));
            map[replacement] = replacement; // takes the slot the removal freed at the end
            Assert.Equal(12, map.Count);
            Assert.Equal(12, map.EnsureCapacity(0));
        }
    }

    [Fact]
    public void EnsureCapacity_ShouldRejectNegative()
    {
        var map = NewIntMap();
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => map.EnsureCapacity(-1));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void EnsureCapacity_ShouldGrowAndPreserveOrder()
    {
        var map = NewIntMap(capacity: 4);
        for (int i = 10; i >= 1; i--)
            map[i] = i * 3;
        map.Remove(5);  // 1 moves into slot 5
        map.Remove(10); // 2 moves into slot 0

        int capacity = map.EnsureCapacity(500);

        Assert.True(capacity >= 500);
        Assert.Equal(new[] { 2, 9, 8, 7, 6, 1, 4, 3 }, KeysOf(map));
        map[100] = 100;
        Assert.Equal(new[] { 2, 9, 8, 7, 6, 1, 4, 3, 100 }, KeysOf(map));
        foreach (int k in new[] { 1, 2, 3, 4, 6, 7, 8, 9 })
            Assert.Equal(k, map.GetKey(k * 3));
    }

    [Fact]
    public void EnsureCapacity_ShouldNotShrinkOrBumpVersion_WhenAlreadyLargeEnough()
    {
        var map = NewIntMap(capacity: 64);
        map[1] = 1;
        var enumerator = map.GetEnumerator();

        Assert.Equal(48, map.EnsureCapacity(10));
        Assert.Equal(48, map.EnsureCapacity(48));

        Assert.True(enumerator.MoveNext()); // no rebuild, no version bump
    }

    [Fact]
    public void EnsureCapacity_ShouldInvalidateEnumerator_WhenItGrows()
    {
        var map = NewIntMap(capacity: 4);
        map[1] = 1;
        var enumerator = map.GetEnumerator();

        map.EnsureCapacity(100);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void EnsureCapacity_OnEmpty_ShouldGrowAndStayUsable()
    {
        var map = NewIntMap(capacity: 1);

        Assert.True(map.EnsureCapacity(100) >= 100);
        Assert.Empty(map);

        map[1] = 1;
        Assert.Equal(new[] { 1 }, KeysOf(map));
        Assert.Equal(1, map.GetKey(1));
    }

    // One bucket always holds one entry, so sizing for a single entry must not apply the load
    // factor: at 1e-6 that would ask for 2^20 buckets (8 MiB across both bucket arrays) whose entry
    // capacity is still one. Entry capacity reads 1 either way, so the regression shows only in the
    // bytes allocated.
    private const float TinyLoadFactor = 0.000001f;
    private const long SingletonAllocationCeiling = 64 * 1024;

    [Fact]
    public void SourceConstructor_WithOnePair_ShouldNotOversizeForATinyLoadFactor()
    {
        var source = new List<KeyValuePair<int, int>> { new(7, 70) };

        long before = GC.GetAllocatedBytesForCurrentThread();
        var map = new BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher>(source, capacity: 16, loadFactor: TinyLoadFactor);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < SingletonAllocationCeiling, $"allocated {allocated} bytes");
        Assert.Equal(70, map[7]);
        Assert.Equal(1, map.EnsureCapacity(0));
    }

    [Fact]
    public void TrimExcess_WithOneEntry_ShouldNotOversizeForATinyLoadFactor()
    {
        var map = NewIntMap(capacity: 16, loadFactor: TinyLoadFactor);
        map[7] = 70;

        long before = GC.GetAllocatedBytesForCurrentThread();
        map.TrimExcess();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < SingletonAllocationCeiling, $"allocated {allocated} bytes");
        Assert.Equal(70, map[7]);
        Assert.Equal(7, map.GetKey(70));
        Assert.Equal(1, map.EnsureCapacity(0));
    }

    [Fact]
    public void TrimExcess_ShouldShrinkAndKeepEveryPairReachable()
    {
        var map = NewIntMap();
        for (int i = 1; i <= 200; i++)
            map[i] = -i;
        for (int i = 1; i <= 200; i++)
        {
            if (i % 10 != 0)
                map.Remove(i);
        }
        int[] before = KeysOf(map);

        map.TrimExcess();

        // Trimmed to exactly Count entries, over MinTableSizeFor(20, 0.75) = 32 buckets.
        Assert.Equal(20, map.EnsureCapacity(0));
        Assert.Equal(before, KeysOf(map)); // a rebuild keeps the order
        Assert.Equal(20, map.Count);
        for (int i = 10; i <= 200; i += 10)
        {
            Assert.Equal(-i, map[i]);
            Assert.Equal(i, map.GetKey(-i));
        }
    }

    [Fact]
    public void TrimExcess_ShouldBeANoOp_WhenAlreadyAtTheMinimumSize()
    {
        var map = NewIntMap(capacity: 4);
        map[1] = 1;
        map[2] = 2;
        map[3] = 3;
        // MinTableSizeFor(3, 0.75) = 4 buckets and 3 entries, the current size.

        var enumerator = map.GetEnumerator();
        map.TrimExcess();

        Assert.True(enumerator.MoveNext()); // no rebuild, no version bump
        Assert.Equal(3, map.EnsureCapacity(0));
    }

    [Fact]
    public void TrimExcess_ShouldInvalidateEnumerator_WhenItRebuilds()
    {
        var map = NewIntMap(capacity: 256);
        map[1] = 1;
        var enumerator = map.GetEnumerator();

        map.TrimExcess();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void TrimExcess_OnEmptyDictionaryWithABigTable_ShouldRebuildToOneBucket()
    {
        var map = NewIntMap(capacity: 1024);
        for (int i = 0; i < 100; i++)
            map[i] = i;
        for (int i = 0; i < 100; i++)
            map.Remove(i);

        map.TrimExcess(); // Rebuild with zero live entries

        Assert.Empty(map);
        Assert.Equal(1, map.EnsureCapacity(0));
        Assert.Empty(KeysOf(map));

        map[7] = 0;
        map[0] = 7;
        Assert.Equal(new[] { 7, 0 }, KeysOf(map));
        Assert.Equal(0, map.GetKey(7));
    }

    [Fact]
    public void TrimExcessWithCapacity_ShouldSizeForTheRequestedCount_AndPreserveOrder()
    {
        var map = NewIntMap(capacity: 4);
        map[3] = 30;
        map[1] = 10;
        map[2] = 20;

        map.TrimExcess(100); // grows to exactly 100 entries over MinTableSizeFor(100, 0.75) = 256 buckets

        Assert.Equal(100, map.EnsureCapacity(0));
        Assert.Equal(new[] { 3, 1, 2 }, KeysOf(map));

        map.TrimExcess(3); // shrinks back: MinTableSizeFor(3, 0.75) = 4 → 3

        Assert.Equal(3, map.EnsureCapacity(0));
        Assert.Equal(new[] { 3, 1, 2 }, KeysOf(map));
        Assert.Equal(1, map.GetKey(10));
    }

    [Fact]
    public void TrimExcessWithCapacity_ShouldRejectACapacityBelowCount()
    {
        var map = NewIntMap();
        map[1] = 1;
        map[2] = 2;

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => map.TrimExcess(1));
        Assert.Equal("capacity", ex.ParamName);
        Assert.Equal(2, map.Count);
    }

    // ---------------------------------------------------------------
    //  CopyTo
    // ---------------------------------------------------------------

    [Fact]
    public void CopyTo_ShouldCopyInOrder_AtTheOffset()
    {
        var map = NewStringMap();
        map[30] = "c";
        map[10] = "a";
        map[0] = "z";
        map[20] = "b";
        map.Remove(10); // 20 moves into slot 1

        var pairs = new KeyValuePair<int, string>[5];
        map.CopyTo(pairs, 2);
        Assert.Equal(default, pairs[0]);
        Assert.Equal(default, pairs[1]);
        Assert.Equal(new KeyValuePair<int, string>(30, "c"), pairs[2]);
        Assert.Equal(new KeyValuePair<int, string>(20, "b"), pairs[3]);
        Assert.Equal(new KeyValuePair<int, string>(0, "z"), pairs[4]);

        var keys = new int[4];
        map.Keys.CopyTo(keys, 1);
        Assert.Equal(new[] { 0, 30, 20, 0 }, keys);

        var values = new string[3];
        map.Values.CopyTo(values, 0);
        Assert.Equal(new[] { "c", "b", "z" }, values);
    }

    [Fact]
    public void CopyTo_ShouldDoNothing_WhenEmpty()
    {
        var map = NewIntMap();
        var pairs = new KeyValuePair<int, int>[0];

        map.CopyTo(pairs, 0);
        map.Keys.CopyTo(new int[0], 0);
        map.Values.CopyTo(new int[0], 0);
        map.Inverse.Keys.CopyTo(new int[0], 0);
    }

    [Fact]
    public void CopyTo_ShouldValidateItsArguments()
    {
        var map = NewIntMap();
        map[1] = 10;
        map[2] = 20;

        Assert.Throws<ArgumentNullException>(() => map.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.CopyTo(new KeyValuePair<int, int>[2], -1));
        Assert.Throws<ArgumentException>(() => map.CopyTo(new KeyValuePair<int, int>[2], 1));

        Assert.Throws<ArgumentNullException>(() => map.Keys.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Keys.CopyTo(new int[2], -1));
        Assert.Throws<ArgumentException>(() => map.Keys.CopyTo(new int[2], 1));

        Assert.Throws<ArgumentNullException>(() => map.Values.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Values.CopyTo(new int[2], -1));
        Assert.Throws<ArgumentException>(() => map.Values.CopyTo(new int[2], 1));
    }

    // ---------------------------------------------------------------
    //  Inverse view
    // ---------------------------------------------------------------

    [Fact]
    public void Inverse_ShouldLookUpKeysByValue()
    {
        var map = NewStringMap();
        map[1] = "one";
        map[0] = "zero";
        var inverse = map.Inverse;

        Assert.Equal(2, inverse.Count);
        Assert.Equal(1, inverse["one"]);
        Assert.Equal(0, inverse["zero"]);
        Assert.Throws<KeyNotFoundException>(() => inverse["two"]);
        Assert.True(inverse.ContainsKey("one"));
        Assert.False(inverse.ContainsKey("two"));
        Assert.True(inverse.TryGetValue("zero", out int k));
        Assert.Equal(0, k);
        Assert.False(inverse.TryGetValue("two", out k));
        Assert.Equal(0, k);
    }

    [Fact]
    public void Inverse_ShouldReflectLaterChanges()
    {
        var map = NewStringMap();
        var inverse = map.Inverse; // taken while empty

        map[1] = "one";
        map[2] = "two";
        Assert.Equal(2, inverse.Count);
        Assert.Equal(2, inverse["two"]);

        map[2] = "deux";
        Assert.False(inverse.ContainsKey("two"));
        Assert.Equal(2, inverse["deux"]);

        map.RemoveValue("one");
        Assert.Equal(1, inverse.Count);
        Assert.False(inverse.ContainsKey("one"));
    }

    [Fact]
    public void Inverse_KeysAndValues_ShouldBeTheDictionarysValuesAndKeys()
    {
        var map = NewStringMap();
        map[3] = "three";
        map[1] = "one";
        map[2] = "two";
        var inverse = map.Inverse;

        Assert.Equal(new[] { "three", "one", "two" }, inverse.Keys.ToArray());
        Assert.Equal(new[] { 3, 1, 2 }, inverse.Values.ToArray());
        Assert.Equal(3, inverse.Keys.Count);
        Assert.Equal(3, inverse.Values.Count);
        Assert.True(inverse.Keys.Contains("one"));
        Assert.False(inverse.Keys.Contains("four"));
        Assert.True(inverse.Values.Contains(2));
        Assert.False(inverse.Values.Contains(4));
    }

    [Fact]
    public void Inverse_ShouldEnumerateValueKeyPairsInOrder()
    {
        var map = NewStringMap();
        map[3] = "three";
        map[1] = "one";

        var seen = new List<KeyValuePair<string, int>>();
        foreach (var kvp in map.Inverse)
            seen.Add(kvp);

        Assert.Equal(new[]
        {
            new KeyValuePair<string, int>("three", 3),
            new KeyValuePair<string, int>("one", 1),
        }, seen);
    }

    [Fact]
    public void Inverse_ThroughIReadOnlyDictionary_ShouldForwardEveryMember()
    {
        var map = NewStringMap();
        map[5] = "five";
        map[0] = "zero";
        IReadOnlyDictionary<string, int> ro = map.Inverse;

        Assert.Equal(2, ro.Count);
        Assert.Equal(5, ro["five"]);
        Assert.Throws<KeyNotFoundException>(() => ro["six"]);
        Assert.True(ro.ContainsKey("zero"));
        Assert.True(ro.TryGetValue("zero", out int k));
        Assert.Equal(0, k);
        Assert.Equal(new[] { "five", "zero" }, ro.Keys.ToArray());
        Assert.Equal(new[] { 5, 0 }, ro.Values.ToArray());
        Assert.Equal(new[] { "five", "zero" }, ro.Select(p => p.Key).ToArray());
        Assert.Equal(new[] { 5, 0 }, ro.Select(p => p.Value).ToArray());
    }

    [Fact]
    public void Inverse_NonGenericEnumerator_ShouldYieldBoxedPairsInOrder()
    {
        var map = NewIntMap();
        map[2] = 20;
        map[1] = 10;

        IEnumerator e = ((IEnumerable)map.Inverse).GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal(new KeyValuePair<int, int>(20, 2), e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal(new KeyValuePair<int, int>(10, 1), e.Current);
        Assert.False(e.MoveNext());
        e.Reset();
        Assert.True(e.MoveNext());
        Assert.Equal(new KeyValuePair<int, int>(20, 2), e.Current);
        ((IDisposable)e).Dispose();
    }

    [Fact]
    public void Inverse_OfAnEmptyDictionary_ShouldBeEmpty()
    {
        var map = NewIntMap();
        var inverse = map.Inverse;

        Assert.Equal(0, inverse.Count);
        Assert.Empty(inverse);
        Assert.False(inverse.ContainsKey(0));
        Assert.Throws<KeyNotFoundException>(() => inverse[0]);
    }

    // ---------------------------------------------------------------
    //  Explicit interface members
    // ---------------------------------------------------------------

    [Fact]
    public void ICollectionOfPairs_ShouldHonourPairSemantics()
    {
        var map = NewStringMap();
        ICollection<KeyValuePair<int, string>> collection = map;

        Assert.False(collection.IsReadOnly);

        collection.Add(new KeyValuePair<int, string>(1, "one"));
        collection.Add(new KeyValuePair<int, string>(2, "two"));
        Assert.Throws<ArgumentException>(() => collection.Add(new KeyValuePair<int, string>(1, "uno")));
        Assert.Throws<ArgumentException>(() => collection.Add(new KeyValuePair<int, string>(3, "one")));

        Assert.True(collection.Contains(new KeyValuePair<int, string>(1, "one")));
        Assert.False(collection.Contains(new KeyValuePair<int, string>(1, "uno"))); // value mismatch
        Assert.False(collection.Contains(new KeyValuePair<int, string>(9, "one"))); // key missing

        Assert.False(collection.Remove(new KeyValuePair<int, string>(1, "uno"))); // stale value
        Assert.False(collection.Remove(new KeyValuePair<int, string>(1, "two"))); // another key's value
        Assert.False(collection.Remove(new KeyValuePair<int, string>(9, "one"))); // key missing
        Assert.Equal(2, map.Count);
        Assert.True(collection.Remove(new KeyValuePair<int, string>(1, "one")));
        Assert.Equal(new[] { 2 }, map.Keys.ToArray());
        Assert.False(map.ContainsValue("one"));

        var pairs = new KeyValuePair<int, string>[1];
        collection.CopyTo(pairs, 0);
        Assert.Equal(new KeyValuePair<int, string>(2, "two"), pairs[0]);
        Assert.Equal(1, collection.Count);
        collection.Clear();
        Assert.Empty(map);
    }

    [Fact]
    public void IDictionary_ShouldForwardToThePrimarySurface()
    {
        var map = NewStringMap();
        IDictionary<int, string> dict = map;

        dict.Add(2, "two");
        dict[1] = "one";
        dict[2] = "TWO";

        Assert.Equal("TWO", dict[2]);
        Assert.Throws<KeyNotFoundException>(() => dict[3]);
        Assert.Throws<ArgumentException>(() => dict[3] = "one");
        Assert.True(dict.ContainsKey(1));
        Assert.True(dict.TryGetValue(1, out string? v));
        Assert.Equal("one", v);
        Assert.Equal(new[] { 2, 1 }, dict.Keys.ToArray());
        Assert.Equal(new[] { "TWO", "one" }, dict.Values.ToArray());
        Assert.True(dict.Keys.IsReadOnly);
        Assert.True(dict.Values.IsReadOnly);
        Assert.True(dict.Remove(1));
        Assert.Single(dict);
    }

    [Fact]
    public void IReadOnlyDictionary_ShouldForwardToThePrimarySurface()
    {
        var map = NewStringMap();
        map[5] = "five";
        map[0] = "zero";
        IReadOnlyDictionary<int, string> ro = map;

        Assert.Equal("five", ro[5]);
        Assert.Throws<KeyNotFoundException>(() => ro[1]);
        Assert.Equal(new[] { 5, 0 }, ro.Keys.ToArray());
        Assert.Equal(new[] { "five", "zero" }, ro.Values.ToArray());
        Assert.Equal(new[] { 5, 0 }, ro.Select(p => p.Key).ToArray());
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldBoxedPairsInOrder()
    {
        var map = NewIntMap();
        map[2] = 20;
        map[1] = 10;

        IEnumerator e = ((IEnumerable)map).GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal(new KeyValuePair<int, int>(2, 20), e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal(new KeyValuePair<int, int>(1, 10), e.Current);
        Assert.False(e.MoveNext());
        e.Reset();
        Assert.True(e.MoveNext());
        Assert.Equal(new KeyValuePair<int, int>(2, 20), e.Current);
    }

    // ---------------------------------------------------------------
    //  A hasher that throws leaves the table intact
    // ---------------------------------------------------------------

    // Removal and re-bucketing both call the hashers (no hash is cached), so a throw part-way through
    // must not leave a pair counted and enumerated but unreachable by lookup.

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Remove_ShouldLeaveTheTableIntact_WhenHashingTheMovedEntryThrows(bool keySide)
    {
        var map = NewThrowingMap(10);
        ArmThrow(keySide, key: 10); // the last entry, which a removal of key 1 moves into the hole

        try
        {
            Assert.Throws<InvalidOperationException>(() => map.Remove(1));
        }
        finally
        {
            Disarm();
        }

        AssertThrowingMapHolds(map, 10);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Growth_ShouldLeaveTheTableIntact_WhenRehashingThrows(bool keySide)
    {
        var map = NewThrowingMap(12); // the default 16 buckets hold 12 entries; the 13th re-buckets
        ArmThrow(keySide, key: 12); // the last entry re-hashed, so every other chain link is already rewritten

        try
        {
            Assert.Throws<InvalidOperationException>(() => map.Add(13, 1013));
        }
        finally
        {
            Disarm();
        }

        AssertThrowingMapHolds(map, 12);
        Assert.False(map.ContainsKey(13));

        map.Add(13, 1013); // and the table still grows once the hasher behaves
        AssertThrowingMapHolds(map, 13);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnsureCapacityAndTrimExcess_ShouldLeaveTheTableIntact_WhenRehashingThrows(bool keySide)
    {
        var grown = NewThrowingMap(12);
        var trimmed = NewThrowingMap(12, capacity: 256); // TrimExcess re-buckets it down to 16
        ArmThrow(keySide, key: 12);

        try
        {
            Assert.Throws<InvalidOperationException>(() => grown.EnsureCapacity(1000));
            Assert.Throws<InvalidOperationException>(() => trimmed.TrimExcess());
        }
        finally
        {
            Disarm();
        }

        AssertThrowingMapHolds(grown, 12);
        AssertThrowingMapHolds(trimmed, 12);
    }

    private static BiDictionary<int, int, ThrowingKeyHasher, ThrowingValueHasher> NewThrowingMap(
        int count, int capacity = 16)
    {
        var map = new BiDictionary<int, int, ThrowingKeyHasher, ThrowingValueHasher>(capacity);
        for (int i = 1; i <= count; i++)
            map.Add(i, 1000 + i);
        return map;
    }

    private static void ArmThrow(bool keySide, int key)
    {
        if (keySide)
            ThrowingKeyHasher.ThrowOn = key;
        else
            ThrowingValueHasher.ThrowOn = 1000 + key;
    }

    private static void Disarm()
    {
        ThrowingKeyHasher.ThrowOn = null;
        ThrowingValueHasher.ThrowOn = null;
    }

    private static void AssertThrowingMapHolds(BiDictionary<int, int, ThrowingKeyHasher, ThrowingValueHasher> map, int count)
    {
        Assert.Equal(count, map.Count);
        Assert.Equal(Enumerable.Range(1, count), KeysOf(map).OrderBy(k => k));
        for (int i = 1; i <= count; i++)
        {
            Assert.Equal(1000 + i, map[i]);
            Assert.Equal(i, map.GetKey(1000 + i));
        }
    }

    // Test-only hashers that throw on one armed input. Each is used by this class alone, and xUnit
    // runs a class's tests one at a time, so the static switch is not shared across threads. The hash
    // puts every entry in one chain at 16 buckets and splits odd from even at 32, so a re-bucketing
    // that stops part-way would leave chain links that skip live entries.
    private struct ThrowingKeyHasher : IHashProvider<int>
    {
        public static int? ThrowOn;

        public int Hash(int key) =>
            key == ThrowOn ? throw new InvalidOperationException("Armed key hasher.") : (key & 1) << 4;
    }

    private struct ThrowingValueHasher : IHashProvider<int>
    {
        public static int? ThrowOn;

        public int Hash(int value) =>
            value == ThrowOn ? throw new InvalidOperationException("Armed value hasher.") : (value & 1) << 4;
    }

    // A test-only hasher used to exercise the Guid path. Uses the GUID's hash
    // code; not part of the public library.
    private struct GuidIdentityHasher : IHashProvider<Guid>
    {
        public int Hash(Guid key) => key.GetHashCode();
    }
}
