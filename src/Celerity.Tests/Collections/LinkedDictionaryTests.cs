using System.Collections;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Dedicated tests for LinkedDictionary, mirroring SwissDictionaryTests for the
// shared dictionary contract and adding the ordered surface: insertion order
// that survives removals, MoveToEnd / MoveToFront, TryGetFirst / TryGetLast,
// TryRemoveFirst / TryRemoveLast, in-table storage of the default key, and the
// sizing rules (growth, EnsureCapacity, TrimExcess, free-list reuse). The
// chain-walk stress (every key in one bucket) is in LinkedDictionaryCollisionTests.
public class LinkedDictionaryTests
{
    private static int[] KeysOf<TValue, THasher>(LinkedDictionary<int, TValue, THasher> map)
        where THasher : struct, IHashProvider<int>
    {
        var keys = new List<int>();
        foreach (var kvp in map)
            keys.Add(kvp.Key);
        return keys.ToArray();
    }

    // ---------------------------------------------------------------
    //  Shared dictionary contract (mirror of SwissDictionaryTests)
    // ---------------------------------------------------------------

    [Fact]
    public void Indexer_ShouldInsertAndRetrieveValue()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[10] = 100;

        Assert.Equal(100, map[10]);
    }

    [Fact]
    public void Indexer_ShouldThrowException_WhenKeyDoesNotExist()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        Assert.Throws<KeyNotFoundException>(() => { var value = map[99]; });
    }

    [Fact]
    public void Indexer_ShouldOverwriteExistingValue()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[5] = 500;
        map[5] = 999; // Overwrite

        Assert.Equal(999, map[5]);
        Assert.Single(map);
    }

    [Fact]
    public void ContainsKey_ShouldReturnTrue_WhenKeyExists()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[7] = 700;

        Assert.True(map.ContainsKey(7));
    }

    [Fact]
    public void ContainsKey_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        Assert.False(map.ContainsKey(7));
    }

    [Fact]
    public void Remove_ShouldDeleteKeyAndMakeItUnreachable()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[7] = 700;

        Assert.True(map.Remove(7));
        Assert.False(map.ContainsKey(7));

        Assert.Throws<KeyNotFoundException>(() => { var value = map[7]; });
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        Assert.False(map.Remove(7));
        Assert.False(map.Remove(7, out int value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void Map_ShouldResize_WhenThresholdExceeded()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(16, loadFactor: 0.5f);
        for (int i = 1; i <= 40; i++)
            map[i] = i * 10; // forces at least one resize

        Assert.Equal(40, map.Count);
        for (int i = 1; i <= 40; i++)
            Assert.Equal(i * 10, map[i]);
        Assert.Equal(Enumerable.Range(1, 40), KeysOf(map));
    }

    [Fact]
    public void Indexer_ShouldHandleZeroIntKey()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        map[0] = "zero";

        Assert.True(map.ContainsKey(0));
        Assert.Equal("zero", map[0]);
        Assert.Single(map);
    }

    [Fact]
    public void Indexer_ShouldOverwriteZeroIntKey()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        map[0] = "zero";
        map[0] = "still-zero";

        Assert.Single(map);
        Assert.Equal("still-zero", map[0]);
    }

    [Fact]
    public void Remove_ShouldHandleZeroIntKey()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        map[0] = "zero";
        map[1] = "one";

        Assert.True(map.Remove(0));
        Assert.False(map.ContainsKey(0));
        Assert.True(map.ContainsKey(1));
        Assert.Single(map);
        Assert.False(map.Remove(0));
    }

    [Fact]
    public void DefaultKey_ShouldSurviveResize()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(16, loadFactor: 0.5f);
        map[0] = -1;
        for (int i = 1; i <= 40; i++)
            map[i] = i * 10; // triggers resize while the default-key entry is live.

        Assert.Equal(41, map.Count);
        Assert.Equal(-1, map[0]);
        Assert.Equal(10, map[1]);
        Assert.Equal(400, map[40]);
        Assert.Equal(Enumerable.Range(0, 41), KeysOf(map));
    }

    [Fact]
    public void TryGetValue_ShouldReturnTrueAndValue_WhenKeyExists()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
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
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        Assert.False(map.TryGetValue(42, out var v1));
        Assert.Null(v1);

        Assert.False(map.TryGetValue(0, out var v2));
        Assert.Null(v2);
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 0; i < 32; i++)
            map[i] = i * i;
        int capacity = map.EnsureCapacity(0);

        map.Clear();

        Assert.Empty(map);
        Assert.Equal(capacity, map.EnsureCapacity(0)); // Clear keeps the capacity
        for (int i = 0; i < 32; i++)
            Assert.False(map.ContainsKey(i));
        Assert.False(map.TryGetFirst(out _, out _));
        Assert.False(map.TryGetLast(out _, out _));

        map[5] = 500;
        map[0] = 100;
        Assert.Equal(2, map.Count);
        Assert.Equal(100, map[0]);
        Assert.Equal(500, map[5]);
        Assert.Equal(new[] { 5, 0 }, KeysOf(map));
    }

    [Fact]
    public void Indexer_ShouldHandleGuidEmptyKey()
    {
        var map = new LinkedDictionary<Guid, string, GuidIdentityHasher>();
        map[Guid.Empty] = "empty";
        var other = Guid.NewGuid();
        map[other] = "other";

        Assert.True(map.ContainsKey(Guid.Empty));
        Assert.Equal("empty", map[Guid.Empty]);
        Assert.Equal("other", map[other]);
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void Indexer_ShouldThrowKeyNotFound_ForAbsentDefaultKey()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();

        Assert.Throws<KeyNotFoundException>(() => _ = map[0]);
    }

    [Fact]
    public void Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        var enumerator = map.GetEnumerator();

        map.Clear(); // _count == 0 early-return path: no version bump

        Assert.Empty(map);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void TryAdd_ShouldReturnFalse_WhenKeyExists()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        Assert.True(map.TryAdd(1, 10));
        Assert.False(map.TryAdd(1, 20));

        Assert.Equal(10, map[1]); // unchanged
        Assert.Single(map);
    }

    [Fact]
    public void Add_ShouldThrow_WhenKeyExists()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map.Add(1, 10);

        Assert.Throws<ArgumentException>(() => map.Add(1, 20));
    }

    // ---------------------------------------------------------------
    //  Constructor sizing
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(0, 0.75f, 1)]     // one bucket, (int)0.75 == 0 → floor of one entry
    [InlineData(1, 0.75f, 1)]
    [InlineData(16, 0.75f, 12)]
    [InlineData(17, 0.75f, 24)]   // rounds up to 32 buckets
    [InlineData(16, 0.01f, 1)]    // (int)0.16 == 0 → floor of one entry
    [InlineData(64, 0.5f, 32)]
    public void Constructor_ShouldSizeEntriesFromBucketsAndLoadFactor(int capacity, float loadFactor, int expectedEntries)
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(capacity, loadFactor);

        Assert.Equal(expectedEntries, map.EnsureCapacity(0));
    }

    [Fact]
    public void SourceConstructor_ShouldPreserveSourceOrder()
    {
        var source = new[]
        {
            new KeyValuePair<int, string>(5, "five"),
            new KeyValuePair<int, string>(0, "zero"),
            new KeyValuePair<int, string>(-3, "minus-three"),
            new KeyValuePair<int, string>(42, "answer"),
        };

        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>(source);

        Assert.Equal(new[] { 5, 0, -3, 42 }, KeysOf(map));
        Assert.Equal(new[] { "five", "zero", "minus-three", "answer" }, map.Values.ToArray());
    }

    [Fact]
    public void SourceConstructor_ShouldPresizeForCollectionSource()
    {
        var source = Enumerable.Range(1, 100).Select(i => new KeyValuePair<int, int>(i, i)).ToArray();

        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(source, capacity: 1);

        // MinTableSizeFor(100, 0.75) = 256 buckets → 192 entries, reached without growing.
        Assert.Equal(192, map.EnsureCapacity(0));
        Assert.Equal(Enumerable.Range(1, 100), KeysOf(map));
    }

    [Fact]
    public void SourceConstructor_ShouldKeepLargerCapacity_WhenSourceIsSmaller()
    {
        var source = new[] { new KeyValuePair<int, int>(1, 1) };

        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(source, capacity: 64);

        Assert.Equal(48, map.EnsureCapacity(0));
    }

    [Fact]
    public void SourceConstructor_ShouldNotPresize_ForLazyEnumerable()
    {
        static IEnumerable<KeyValuePair<int, int>> Lazy()
        {
            for (int i = 1; i <= 100; i++)
                yield return new KeyValuePair<int, int>(i, i);
        }

        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(Lazy(), capacity: 1);

        Assert.Equal(100, map.Count);
        Assert.Equal(Enumerable.Range(1, 100), KeysOf(map));
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
            new LinkedDictionary<int, int, Int32WangNaiveHasher>(source, capacity: 16, loadFactor: loadFactor));
        Assert.Equal("loadFactor", ex.ParamName);
    }

    // ---------------------------------------------------------------
    //  Insertion order
    // ---------------------------------------------------------------

    [Fact]
    public void Enumeration_ShouldFollowInsertionOrder_NotHashOrder()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        int[] keys = { 90, 3, 57, -12, 1000, 0, 8, 41 };
        foreach (int k in keys)
            map.Add(k, k * 2);

        Assert.Equal(keys, KeysOf(map));
        Assert.Equal(keys.Select(k => k * 2).ToArray(), map.Values.ToArray());
    }

    [Fact]
    public void Remove_ShouldKeepTheRelativeOrderOfTheRest()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 8; i++)
            map[i] = i;

        Assert.True(map.Remove(1)); // head
        Assert.True(map.Remove(8)); // tail
        Assert.True(map.Remove(4)); // middle

        Assert.Equal(new[] { 2, 3, 5, 6, 7 }, KeysOf(map));
    }

    [Fact]
    public void ReAddAfterRemove_ShouldAppendAtTheEnd_EvenWhenReusingAFreedSlot()
    {
        // Contrast with BCL Dictionary, whose freed slot puts the re-added key back
        // where the removed one was.
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 5; i++)
            map[i] = i;

        Assert.True(map.Remove(2));
        map[2] = 20;
        map[9] = 90;

        Assert.Equal(new[] { 1, 3, 4, 5, 2, 9 }, KeysOf(map));
        Assert.Equal(20, map[2]);
    }

    [Fact]
    public void IndexerOverwrite_ShouldKeepTheKeysPosition()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
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
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 1;
        map[2] = 2;

        Assert.False(map.TryAdd(1, 100));

        Assert.Equal(new[] { 1, 2 }, KeysOf(map));
        Assert.Equal(1, map[1]);
    }

    [Fact]
    public void DefaultKey_ShouldKeepItsOrderPosition()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        map[3] = "three";
        map[0] = "zero";
        map[7] = "seven";

        Assert.Equal(new[] { 3, 0, 7 }, KeysOf(map));

        Assert.True(map.Remove(3));
        Assert.True(map.TryGetFirst(out int first, out string? firstValue));
        Assert.Equal(0, first);
        Assert.Equal("zero", firstValue);

        Assert.True(map.MoveToEnd(0));
        Assert.Equal(new[] { 7, 0 }, KeysOf(map));
    }

    [Fact]
    public void NullStringKey_ShouldKeepItsOrderPosition_AndNeverReachTheHasher()
    {
        // NullRejectingStringHasher throws on null, so any null-key call that reached it would fail.
        var map = new LinkedDictionary<string, int, NullRejectingStringHasher>();
        map["a"] = 1;
        map[null!] = 0;
        map["b"] = 2;

        Assert.Equal(new[] { "a", null, "b" }, map.Keys.ToArray());
        Assert.True(map.ContainsKey(null!));
        Assert.Equal(0, map[null!]);
        Assert.True(map.TryGetValue(null!, out int v));
        Assert.Equal(0, v);
        Assert.False(map.TryAdd(null!, 9));
        map[null!] = 10;
        Assert.Equal(new[] { "a", null, "b" }, map.Keys.ToArray());
        Assert.True(map.MoveToFront(null!));
        Assert.Equal(new[] { null, "a", "b" }, map.Keys.ToArray());
        Assert.True(map.MoveToEnd(null!));
        Assert.Equal(new[] { "a", "b", null }, map.Keys.ToArray());

        // Growth and rebuilds reuse the stored hash, so they never re-hash the null key either.
        for (int i = 0; i < 40; i++)
            map[$"k{i}"] = i;
        map.TrimExcess();
        Assert.Equal(10, map[null!]);

        Assert.True(map.Remove(null!, out int removed));
        Assert.Equal(10, removed);
        Assert.False(map.ContainsKey(null!));
        Assert.False(map.Remove(null!));
    }

    [Fact]
    public void NullStringKey_ShouldBeRemovableFromEitherEnd_WithoutReachingTheHasher()
    {
        var map = new LinkedDictionary<string, int, NullRejectingStringHasher>();
        map[null!] = 1;
        map["x"] = 2;
        map["y"] = 3;

        Assert.True(map.TryRemoveFirst(out string? first, out int firstValue));
        Assert.Null(first);
        Assert.Equal(1, firstValue);

        map[null!] = 4;
        Assert.True(map.TryRemoveLast(out string? last, out int lastValue));
        Assert.Null(last);
        Assert.Equal(4, lastValue);
        Assert.Equal(new[] { "x", "y" }, map.Keys.ToArray());
    }

    [Fact]
    public void NullableKey_WithoutValue_ShouldNeverReachTheHasher()
    {
        var map = new LinkedDictionary<int?, string, NullRejectingNullableIntHasher>();
        map[1] = "one";
        map[null] = "null";
        map[2] = "two";

        Assert.Equal(new int?[] { 1, null, 2 }, map.Keys.ToArray());
        Assert.Equal("null", map[null]);
        Assert.True(map.Remove(null));
        Assert.False(map.ContainsKey(null));
    }

    [Fact]
    public void DefaultIntKey_ShouldNeverReachTheHasher()
    {
        var map = new LinkedDictionary<int, int, DefaultRejectingIntHasher>();
        map[5] = 5;
        map[0] = 0;

        Assert.True(map.ContainsKey(0));
        Assert.True(map.Remove(0));
        Assert.Equal(new[] { 5 }, KeysOf(map));
    }

    // ---------------------------------------------------------------
    //  MoveToEnd / MoveToFront
    // ---------------------------------------------------------------

    [Fact]
    public void MoveToEnd_ShouldMoveAPresentKeyToTheEnd_KeepingItsValue()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 4; i++)
            map[i] = i * 10;

        Assert.True(map.MoveToEnd(1)); // from the head
        Assert.Equal(new[] { 2, 3, 4, 1 }, KeysOf(map));
        Assert.True(map.MoveToEnd(3)); // from the middle
        Assert.Equal(new[] { 2, 4, 1, 3 }, KeysOf(map));
        Assert.Equal(10, map[1]);
        Assert.Equal(30, map[3]);
        Assert.Equal(4, map.Count);

        Assert.True(map.TryGetLast(out int last, out int lastValue));
        Assert.Equal(3, last);
        Assert.Equal(30, lastValue);
    }

    [Fact]
    public void MoveToEnd_ShouldReturnFalse_WhenKeyIsAbsent()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        Assert.False(map.MoveToEnd(1));

        map[1] = 1;
        var enumerator = map.GetEnumerator();
        Assert.False(map.MoveToEnd(2));
        Assert.True(enumerator.MoveNext()); // a miss is not a modification
    }

    [Fact]
    public void MoveToEnd_ShouldBeANoOp_WhenKeyIsAlreadyLast()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.MoveToEnd(3));

        // No version bump: the enumerator keeps going.
        Assert.True(enumerator.MoveNext());
        Assert.Equal(2, enumerator.Current.Key);
        Assert.Equal(new[] { 1, 2, 3 }, KeysOf(map));
    }

    [Fact]
    public void MoveToEnd_ShouldBeANoOp_OnASingleEntry()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 1;
        var enumerator = map.GetEnumerator();

        Assert.True(map.MoveToEnd(1));
        Assert.True(map.MoveToFront(1));

        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveToEnd_ShouldInvalidateEnumerator_WhenItMovesTheEntry()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.MoveToEnd(1));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveToFront_ShouldMoveAPresentKeyToTheFront_KeepingItsValue()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 4; i++)
            map[i] = i * 10;

        Assert.True(map.MoveToFront(4)); // from the tail
        Assert.Equal(new[] { 4, 1, 2, 3 }, KeysOf(map));
        Assert.True(map.MoveToFront(2)); // from the middle
        Assert.Equal(new[] { 2, 4, 1, 3 }, KeysOf(map));
        Assert.Equal(40, map[4]);
        Assert.Equal(20, map[2]);

        Assert.True(map.TryGetFirst(out int first, out int firstValue));
        Assert.Equal(2, first);
        Assert.Equal(20, firstValue);
        Assert.True(map.TryGetLast(out int last, out _));
        Assert.Equal(3, last);
    }

    [Fact]
    public void MoveToFront_ShouldReturnFalse_WhenKeyIsAbsent()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        Assert.False(map.MoveToFront(1));

        map[1] = 1;
        Assert.False(map.MoveToFront(2));
        Assert.Equal(new[] { 1 }, KeysOf(map));
    }

    [Fact]
    public void MoveToFront_ShouldBeANoOp_WhenKeyIsAlreadyFirst()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.MoveToFront(1));

        Assert.True(enumerator.MoveNext());
        Assert.Equal(2, enumerator.Current.Key);
    }

    [Fact]
    public void MoveToFront_ShouldInvalidateEnumerator_WhenItMovesTheEntry()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.MoveToFront(3));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveToFrontThenMoveToEnd_ShouldRoundTrip_OnTwoEntries()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 1;
        map[2] = 2;

        Assert.True(map.MoveToFront(2));
        Assert.Equal(new[] { 2, 1 }, KeysOf(map));
        Assert.True(map.MoveToEnd(2));
        Assert.Equal(new[] { 1, 2 }, KeysOf(map));
    }

    // ---------------------------------------------------------------
    //  TryGetFirst / TryGetLast / TryRemoveFirst / TryRemoveLast
    // ---------------------------------------------------------------

    [Fact]
    public void TryGetFirstAndLast_ShouldReturnFalseAndDefaults_WhenEmpty()
    {
        var map = new LinkedDictionary<string, string, StringFnV1AHasher>();

        Assert.False(map.TryGetFirst(out string? k1, out string? v1));
        Assert.Null(k1);
        Assert.Null(v1);
        Assert.False(map.TryGetLast(out string? k2, out string? v2));
        Assert.Null(k2);
        Assert.Null(v2);
    }

    [Fact]
    public void TryGetFirstAndLast_ShouldReturnTheOldestAndNewest()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        map[9] = "nine";
        map[4] = "four";
        map[6] = "six";

        Assert.True(map.TryGetFirst(out int firstKey, out string? firstValue));
        Assert.Equal(9, firstKey);
        Assert.Equal("nine", firstValue);
        Assert.True(map.TryGetLast(out int lastKey, out string? lastValue));
        Assert.Equal(6, lastKey);
        Assert.Equal("six", lastValue);
        Assert.Equal(3, map.Count); // a peek removes nothing
    }

    [Fact]
    public void TryGetFirstAndLast_ShouldAgree_OnASingleEntry()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 7;

        Assert.True(map.TryGetFirst(out int first, out int firstValue));
        Assert.True(map.TryGetLast(out int last, out int lastValue));
        Assert.Equal(0, first);
        Assert.Equal(0, last);
        Assert.Equal(7, firstValue);
        Assert.Equal(7, lastValue);
    }

    [Fact]
    public void TryRemoveFirstAndLast_ShouldReturnFalse_WhenEmpty()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        var enumerator = map.GetEnumerator();

        Assert.False(map.TryRemoveFirst(out int k1, out string? v1));
        Assert.Equal(0, k1);
        Assert.Null(v1);
        Assert.False(map.TryRemoveLast(out int k2, out string? v2));
        Assert.Equal(0, k2);
        Assert.Null(v2);

        Assert.False(enumerator.MoveNext()); // no version bump
    }

    [Fact]
    public void TryRemoveFirst_ShouldDrainInFifoOrder()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        int[] keys = { 5, 0, 3, 9, 1 };
        foreach (int k in keys)
            map[k] = k * 100;

        var drained = new List<int>();
        while (map.TryRemoveFirst(out int key, out int value))
        {
            Assert.Equal(key * 100, value);
            Assert.False(map.ContainsKey(key));
            drained.Add(key);
        }

        Assert.Equal(keys, drained);
        Assert.Empty(map);
    }

    [Fact]
    public void TryRemoveLast_ShouldDrainInLifoOrder()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        int[] keys = { 5, 0, 3, 9, 1 };
        foreach (int k in keys)
            map[k] = k * 100;

        var drained = new List<int>();
        while (map.TryRemoveLast(out int key, out int value))
        {
            Assert.Equal(key * 100, value);
            Assert.False(map.ContainsKey(key));
            drained.Add(key);
        }

        Assert.Equal(Enumerable.Reverse(keys), drained);
        Assert.Empty(map);
    }

    [Fact]
    public void TryRemoveFirst_ShouldInvalidateEnumerator()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 1;
        map[2] = 2;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(map.TryRemoveFirst(out _, out _));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void TryRemoveFirstAndLast_ShouldLeaveTheTableUsable()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 6; i++)
            map[i] = i;

        Assert.True(map.TryRemoveFirst(out int first, out _));
        Assert.True(map.TryRemoveLast(out int last, out _));
        Assert.Equal(1, first);
        Assert.Equal(6, last);

        map[7] = 7;
        map[1] = 1;
        Assert.Equal(new[] { 2, 3, 4, 5, 7, 1 }, KeysOf(map));
        for (int i = 1; i <= 5; i++)
            Assert.True(map.ContainsKey(i));
        Assert.False(map.ContainsKey(6));
    }

    // ---------------------------------------------------------------
    //  Growth, free-list reuse, EnsureCapacity, TrimExcess
    // ---------------------------------------------------------------

    [Fact]
    public void Growth_WithTinyLoadFactor_ShouldDoubleSeveralTimesInOneGrow()
    {
        // 16 buckets at 0.01 hold max(1, (int)0.16) = 1 entry. The second add must double
        // 16 → 32 → 64 → 128 → 256 before (int)(256 * 0.01) = 2 exceeds Count.
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(capacity: 16, loadFactor: 0.01f);
        Assert.Equal(1, map.EnsureCapacity(0));

        map[1] = 1;
        Assert.Equal(1, map.EnsureCapacity(0));
        map[2] = 2;
        Assert.Equal(2, map.EnsureCapacity(0));

        for (int i = 3; i <= 50; i++)
            map[i] = i;

        Assert.Equal(50, map.Count);
        Assert.Equal(Enumerable.Range(1, 50), KeysOf(map));
        for (int i = 1; i <= 50; i++)
            Assert.Equal(i, map[i]);
    }

    [Fact]
    public void Growth_ShouldDoubleOnce_AtAnOrdinaryLoadFactor()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(capacity: 4, loadFactor: 0.75f);
        Assert.Equal(3, map.EnsureCapacity(0));

        for (int i = 1; i <= 4; i++)
            map[i] = i;

        Assert.Equal(6, map.EnsureCapacity(0));
        Assert.Equal(new[] { 1, 2, 3, 4 }, KeysOf(map));
    }

    [Fact]
    public void FreeListReuse_ShouldNotGrowTheTable()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(capacity: 16);
        for (int i = 1; i <= 12; i++)
            map[i] = i; // exactly full: 12 entries
        Assert.Equal(12, map.EnsureCapacity(0));

        for (int round = 0; round < 100; round++)
        {
            int victim = (round % 12) + 1 + (round / 12) * 100;
            int replacement = victim + 100;
            Assert.True(map.Remove(victim));
            map[replacement] = replacement; // takes the freed slot
            Assert.Equal(12, map.Count);
            Assert.Equal(12, map.EnsureCapacity(0));
        }
    }

    [Fact]
    public void FreeListReuse_ShouldTakeMultipleFreedSlots_BeforeGrowing()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(capacity: 16);
        for (int i = 1; i <= 12; i++)
            map[i] = i;

        Assert.True(map.Remove(3));
        Assert.True(map.Remove(7));
        Assert.True(map.Remove(11));

        map[20] = 20;
        map[21] = 21;
        map[22] = 22;
        Assert.Equal(12, map.EnsureCapacity(0)); // three freed slots reused

        map[23] = 23; // free list empty and table full → grows now
        Assert.Equal(24, map.EnsureCapacity(0));
        Assert.Equal(new[] { 1, 2, 4, 5, 6, 8, 9, 10, 12, 20, 21, 22, 23 }, KeysOf(map));
    }

    [Fact]
    public void EnsureCapacity_ShouldGrowAndPreserveOrder()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(capacity: 4);
        for (int i = 10; i >= 1; i--)
            map[i] = i;
        map.Remove(5);
        map.Remove(10);

        int capacity = map.EnsureCapacity(500);

        Assert.True(capacity >= 500);
        Assert.Equal(new[] { 9, 8, 7, 6, 4, 3, 2, 1 }, KeysOf(map));
        map[100] = 100;
        Assert.Equal(new[] { 9, 8, 7, 6, 4, 3, 2, 1, 100 }, KeysOf(map));
        Assert.True(map.TryGetFirst(out int first, out _));
        Assert.Equal(9, first);
    }

    [Fact]
    public void EnsureCapacity_ShouldNotShrinkOrBumpVersion_WhenAlreadyLargeEnough()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(capacity: 64);
        map[1] = 1;
        var enumerator = map.GetEnumerator();

        Assert.Equal(48, map.EnsureCapacity(10));
        Assert.Equal(48, map.EnsureCapacity(48));

        Assert.True(enumerator.MoveNext()); // no rebuild, no version bump
    }

    [Fact]
    public void EnsureCapacity_ShouldInvalidateEnumerator_WhenItGrows()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(capacity: 4);
        map[1] = 1;
        var enumerator = map.GetEnumerator();

        map.EnsureCapacity(100);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void EnsureCapacity_OnEmpty_ShouldGrowAndStayUsable()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(capacity: 1);

        Assert.True(map.EnsureCapacity(100) >= 100);
        Assert.Empty(map);
        Assert.False(map.TryGetFirst(out _, out _));

        map[1] = 1;
        Assert.Equal(new[] { 1 }, KeysOf(map));
    }

    [Fact]
    public void TrimExcess_ShouldShrinkAndPreserveOrder()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 200; i++)
            map[i] = i;
        for (int i = 1; i <= 200; i++)
        {
            if (i % 10 != 0)
                map.Remove(i);
        }
        Assert.True(map.MoveToFront(100));

        map.TrimExcess();

        // MinTableSizeFor(20, 0.75) = 32 buckets → 24 entries.
        Assert.Equal(24, map.EnsureCapacity(0));
        Assert.Equal(new[] { 100, 10, 20, 30, 40, 50, 60, 70, 80, 90, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200 }, KeysOf(map));
        for (int i = 10; i <= 200; i += 10)
            Assert.Equal(i, map[i]);

        Assert.True(map.TryGetLast(out int last, out _));
        Assert.Equal(200, last);
        Assert.True(map.TryRemoveLast(out _, out _));
        map[5] = 5;
        Assert.True(map.TryGetLast(out last, out _));
        Assert.Equal(5, last);
    }

    [Fact]
    public void TrimExcess_ShouldBeANoOp_WhenAlreadyAtTheMinimumSize()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(capacity: 4);
        map[1] = 1;
        map[2] = 2;
        // MinTableSizeFor(2, 0.75) = 4 buckets, the current size.

        var enumerator = map.GetEnumerator();
        map.TrimExcess();

        Assert.True(enumerator.MoveNext()); // no rebuild, no version bump
        Assert.Equal(3, map.EnsureCapacity(0));
    }

    [Fact]
    public void TrimExcess_ShouldInvalidateEnumerator_WhenItRebuilds()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(capacity: 256);
        map[1] = 1;
        var enumerator = map.GetEnumerator();

        map.TrimExcess();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void TrimExcess_OnEmptyDictionaryWithABigTable_ShouldRebuildToOneBucket()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(capacity: 1024);
        for (int i = 0; i < 100; i++)
            map[i] = i;
        for (int i = 0; i < 100; i++)
            map.Remove(i);

        map.TrimExcess(); // Rebuild with zero live entries

        Assert.Empty(map);
        Assert.Equal(1, map.EnsureCapacity(0));
        Assert.False(map.TryGetFirst(out _, out _));
        Assert.False(map.TryGetLast(out _, out _));
        Assert.Empty(KeysOf(map));

        map[7] = 7;
        map[0] = 0;
        Assert.Equal(new[] { 7, 0 }, KeysOf(map));
    }

    [Fact]
    public void TrimExcessWithCapacity_ShouldSizeForTheRequestedCount_AndPreserveOrder()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(capacity: 4);
        map[3] = 3;
        map[1] = 1;
        map[2] = 2;

        map.TrimExcess(100); // grows: MinTableSizeFor(100, 0.75) = 256 buckets → 192

        Assert.Equal(192, map.EnsureCapacity(0));
        Assert.Equal(new[] { 3, 1, 2 }, KeysOf(map));

        map.TrimExcess(3); // shrinks back: MinTableSizeFor(3, 0.75) = 4 → 3

        Assert.Equal(3, map.EnsureCapacity(0));
        Assert.Equal(new[] { 3, 1, 2 }, KeysOf(map));
    }

    // ---------------------------------------------------------------
    //  ContainsValue, CopyTo
    // ---------------------------------------------------------------

    [Fact]
    public void ContainsValue_ShouldNotSeeRemovedOrFreedEntries()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        map[1] = "one";
        map[2] = "two";
        map[3] = "three";

        map.Remove(2);
        Assert.False(map.ContainsValue("two"));
        Assert.False(map.ContainsValue(null)); // freed slot holds a cleared value, but is unlinked
        Assert.True(map.ContainsValue("one"));
        Assert.True(map.ContainsValue("three"));

        map.TryRemoveFirst(out _, out _);
        map.TryRemoveLast(out _, out _);
        Assert.False(map.ContainsValue("one"));
        Assert.False(map.ContainsValue("three"));
    }

    [Fact]
    public void CopyTo_ShouldCopyInOrder_AtTheOffset()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        map[30] = "c";
        map[10] = "a";
        map[0] = "z";
        map[20] = "b";
        map.Remove(10);
        map.MoveToFront(20);

        var pairs = new KeyValuePair<int, string?>[5];
        map.CopyTo(pairs, 2);
        Assert.Equal(default, pairs[0]);
        Assert.Equal(default, pairs[1]);
        Assert.Equal(new KeyValuePair<int, string?>(20, "b"), pairs[2]);
        Assert.Equal(new KeyValuePair<int, string?>(30, "c"), pairs[3]);
        Assert.Equal(new KeyValuePair<int, string?>(0, "z"), pairs[4]);

        var keys = new int[4];
        map.Keys.CopyTo(keys, 1);
        Assert.Equal(new[] { 0, 20, 30, 0 }, keys);

        var values = new string?[3];
        map.Values.CopyTo(values, 0);
        Assert.Equal(new[] { "b", "c", "z" }, values);
    }

    [Fact]
    public void CopyTo_ShouldDoNothing_WhenEmpty()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        var pairs = new KeyValuePair<int, int>[0];

        map.CopyTo(pairs, 0);
        map.Keys.CopyTo(new int[0], 0);
        map.Values.CopyTo(new int[0], 0);
    }

    // ---------------------------------------------------------------
    //  Explicit interface members
    // ---------------------------------------------------------------

    [Fact]
    public void ICollectionOfPairs_ShouldHonourPairSemantics()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        ICollection<KeyValuePair<int, string?>> collection = map;

        Assert.False(collection.IsReadOnly);

        collection.Add(new KeyValuePair<int, string?>(1, "one"));
        collection.Add(new KeyValuePair<int, string?>(2, "two"));
        Assert.Throws<ArgumentException>(() => collection.Add(new KeyValuePair<int, string?>(1, "uno")));

        Assert.True(collection.Contains(new KeyValuePair<int, string?>(1, "one")));
        Assert.False(collection.Contains(new KeyValuePair<int, string?>(1, "uno"))); // value mismatch
        Assert.False(collection.Contains(new KeyValuePair<int, string?>(9, "one"))); // key missing

        Assert.False(collection.Remove(new KeyValuePair<int, string?>(1, "uno"))); // stale value
        Assert.False(collection.Remove(new KeyValuePair<int, string?>(9, "one"))); // key missing
        Assert.Equal(2, map.Count);
        Assert.True(collection.Remove(new KeyValuePair<int, string?>(1, "one")));
        Assert.Equal(new[] { 2 }, map.Keys.ToArray());
    }

    [Fact]
    public void IDictionary_ShouldForwardToThePrimarySurface()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        IDictionary<int, string?> dict = map;

        dict.Add(2, "two");
        dict[1] = "one";
        dict[2] = "TWO";

        Assert.Equal("TWO", dict[2]);
        Assert.Throws<KeyNotFoundException>(() => dict[3]);
        Assert.Equal(new[] { 2, 1 }, dict.Keys.ToArray());
        Assert.Equal(new[] { "TWO", "one" }, dict.Values.ToArray());
        Assert.True(dict.Keys.IsReadOnly);
        Assert.True(dict.Values.IsReadOnly);
    }

    [Fact]
    public void IReadOnlyDictionary_ShouldForwardToThePrimarySurface()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        map[5] = "five";
        map[0] = "zero";
        IReadOnlyDictionary<int, string?> ro = map;

        Assert.Equal("five", ro[5]);
        Assert.Throws<KeyNotFoundException>(() => ro[1]);
        Assert.Equal(new[] { 5, 0 }, ro.Keys.ToArray());
        Assert.Equal(new[] { "five", "zero" }, ro.Values.ToArray());
        Assert.Equal(new[] { 5, 0 }, ro.Select(p => p.Key).ToArray());
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldBoxedPairsInOrder()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
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

    // A test-only hasher used to exercise the Guid path. Uses the GUID's hash
    // code; not part of the public library.
    private struct GuidIdentityHasher : IHashProvider<Guid>
    {
        public int Hash(Guid key) => key.GetHashCode();
    }
}
