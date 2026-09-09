using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// The public surface of <see cref="PersistentHashMap{TKey, TValue, THasher}"/>: construction, the read
/// paths, the four persistent operations and every documented guard.
///
/// <para>
/// The randomized reconciliation against a <see cref="Dictionary{TKey, TValue}"/> oracle — and with it the
/// trie's shape changes as nodes split and collapse — lives in
/// <see cref="PersistentHashMapDifferentialTests"/>; full-hash collisions live in
/// <see cref="PersistentHashMapCollisionTests"/>. What is pinned here is what a caller can read from the
/// documentation: that every operation returns a <i>new</i> map and leaves the receiver alone, that a no-op
/// write hands back the receiver itself, that the out-of-band <c>default(TKey)</c> slot behaves like any
/// other entry, and that the exceptions are the ones the XML docs promise.
/// </para>
/// </summary>
public class PersistentHashMapTests
{
    // Enough keys that the trie is three levels deep in places, so the reads and writes under test are not
    // all answered by the root's own inline entries.
    private const int LargeCount = 5_000;

    private static PersistentHashMap<int, string, Int32IdentityHasher> EmptyMap =>
        PersistentHashMap<int, string, Int32IdentityHasher>.Empty;

    // ── Empty ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Empty_HasNoEntries()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap;

        Assert.Equal(0, map.Count);
        Assert.True(map.IsEmpty);
        Assert.False(map.ContainsKey(1));
        Assert.False(map.TryGetValue(1, out string? value));
        Assert.Null(value);
        Assert.Empty(map);
    }

    [Fact]
    public void Empty_IsTheSameInstanceEveryTime()
    {
        Assert.Same(EmptyMap, PersistentHashMap<int, string, Int32IdentityHasher>.Empty);
    }

    [Fact]
    public void Indexer_ShouldThrow_WhenKeyIsAbsent()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(1, "one");

        Assert.Throws<KeyNotFoundException>(() => map[2]);
    }

    // ── Add / SetItem ────────────────────────────────────────────────────────────

    [Fact]
    public void Add_ShouldReturnANewMap_AndLeaveTheReceiverAlone()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> before = EmptyMap.Add(1, "one");
        PersistentHashMap<int, string, Int32IdentityHasher> after = before.Add(2, "two");

        Assert.Equal(1, before.Count);
        Assert.False(before.ContainsKey(2));
        Assert.Equal(2, after.Count);
        Assert.Equal("one", after[1]);
        Assert.Equal("two", after[2]);
    }

    [Fact]
    public void Add_ShouldThrow_OnADuplicateKey()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(1, "one");

        var ex = Assert.Throws<ArgumentException>(() => map.Add(1, "uno"));

        Assert.Equal("key", ex.ParamName);
    }

    [Fact]
    public void Add_ShouldThrow_OnADuplicateKey_EvenWhenTheValueMatches()
    {
        // Deliberately stricter than ImmutableDictionary<,>.Add, which tolerates a duplicate whose value is
        // equal. This family matches Dictionary<,>.Add instead, and the docs say so.
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(1, "one");

        Assert.Throws<ArgumentException>(() => map.Add(1, "one"));
    }

    [Fact]
    public void SetItem_ShouldOverwrite_WithoutTouchingTheReceiver()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> before = EmptyMap.Add(1, "one");
        PersistentHashMap<int, string, Int32IdentityHasher> after = before.SetItem(1, "uno");

        Assert.Equal("one", before[1]);
        Assert.Equal("uno", after[1]);
        Assert.Equal(1, after.Count);
    }

    [Fact]
    public void SetItem_ShouldReturnTheReceiver_WhenTheValueIsAlreadyEqual()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(1, "one");

        Assert.Same(map, map.SetItem(1, "one"));
    }

    [Fact]
    public void SetItem_ShouldInsert_WhenTheKeyIsAbsent()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.SetItem(7, "seven");

        Assert.Equal(1, map.Count);
        Assert.Equal("seven", map[7]);
    }

    [Fact]
    public void SetItem_ShouldWriteThroughEveryTrieDepth()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = BuildRange(LargeCount);

        PersistentHashMap<int, string, Int32IdentityHasher> updated = map.SetItem(4_321, "changed");

        Assert.Equal("changed", updated[4_321]);
        Assert.Equal("4321", map[4_321]);
        Assert.Equal(LargeCount - 1, updated.Count);
    }

    // ── Bulk writes ──────────────────────────────────────────────────────────────

    [Fact]
    public void SetItems_ShouldApplyEveryEntry_AndLetLaterEntriesWin()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(1, "one");

        PersistentHashMap<int, string, Int32IdentityHasher> updated = map.SetItems(
        [
            new KeyValuePair<int, string>(1, "uno"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(2, "dos"),
        ]);

        Assert.Equal(2, updated.Count);
        Assert.Equal("uno", updated[1]);
        Assert.Equal("dos", updated[2]);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void SetItems_ShouldReturnTheReceiver_WhenNothingChanges()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(1, "one").Add(2, "two");

        Assert.Same(map, map.SetItems([new KeyValuePair<int, string>(1, "one")]));
        Assert.Same(map, map.SetItems([]));
    }

    [Fact]
    public void SetItems_ShouldThrow_WhenTheSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => EmptyMap.SetItems(null!));

        Assert.Equal("items", ex.ParamName);
    }

    [Fact]
    public void RemoveRange_ShouldDropEveryPresentKey_AndIgnoreTheRest()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = BuildRange(64);

        PersistentHashMap<int, string, Int32IdentityHasher> trimmed = map.RemoveRange([3, 9, 1_000]);

        Assert.Equal(map.Count - 2, trimmed.Count);
        Assert.False(trimmed.ContainsKey(3));
        Assert.False(trimmed.ContainsKey(9));
        Assert.True(map.ContainsKey(3));
    }

    [Fact]
    public void RemoveRange_ShouldReturnTheReceiver_WhenNoKeyIsPresent()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = BuildRange(16);

        Assert.Same(map, map.RemoveRange([9_001, 9_002]));
        Assert.Same(map, map.RemoveRange([]));
    }

    [Fact]
    public void RemoveRange_ShouldThrow_WhenTheSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => EmptyMap.RemoveRange(null!));

        Assert.Equal("keys", ex.ParamName);
    }

    // ── Remove ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_ShouldReturnTheReceiver_WhenTheKeyIsAbsent()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(1, "one");

        Assert.Same(map, map.Remove(2));
        Assert.Same(EmptyMap, EmptyMap.Remove(2));
    }

    [Fact]
    public void Remove_ShouldReturnEmpty_WhenTheLastEntryGoes()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(1, "one");

        Assert.Same(EmptyMap, map.Remove(1));
    }

    [Fact]
    public void Remove_ShouldLeaveTheReceiverAlone()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> before = EmptyMap.Add(1, "one").Add(2, "two");
        PersistentHashMap<int, string, Int32IdentityHasher> after = before.Remove(1);

        Assert.Equal(2, before.Count);
        Assert.Equal("one", before[1]);
        Assert.Equal(1, after.Count);
        Assert.False(after.ContainsKey(1));
    }

    [Fact]
    public void Remove_ShouldCollapseTheTrie_SoADrainedMapEqualsTheOneBuiltWithoutThoseKeys()
    {
        // The inlining rule in Remove is what this pins: after draining back to two keys, a lookup for a
        // survivor must still land, which it cannot if an entry were stranded under a node kept alive only to
        // reach it.
        PersistentHashMap<int, string, Int32IdentityHasher> map = BuildRange(2_000);

        for (int key = 3; key < 2_000; key++)
            map = map.Remove(key);

        Assert.Equal(2, map.Count);
        Assert.Equal("1", map[1]);
        Assert.Equal("2", map[2]);
    }

    [Fact]
    public void AddThenDrain_ShouldEndAtEmpty()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = BuildRange(LargeCount);

        for (int key = 1; key < LargeCount; key++)
        {
            map = map.Remove(key);
            Assert.Equal(LargeCount - 1 - key, map.Count);
        }

        Assert.Same(EmptyMap, map);
    }

    // ── Deep tries ───────────────────────────────────────────────────────────────

    [Fact]
    public void EveryKeyIsFound_InAMapLargeEnoughToBeSeveralLevelsDeep()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = BuildRange(LargeCount);

        Assert.Equal(LargeCount - 1, map.Count);

        for (int key = 1; key < LargeCount; key++)
        {
            Assert.True(map.TryGetValue(key, out string? value));
            Assert.Equal(key.ToString(), value);
        }

        Assert.False(map.ContainsKey(LargeCount + 1));
    }

    [Fact]
    public void KeysSharingEveryBitButTheTop_ShouldStillBeTold_Apart()
    {
        // Identity hashing puts these two at the same slot on all six lower levels; only the shift-30 level,
        // which reads the top two bits, separates them. That is the deepest merge a non-colliding pair can
        // produce.
        const int Low = 1;
        const int High = 1 | (1 << 30);

        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(Low, "low").Add(High, "high");

        Assert.Equal(2, map.Count);
        Assert.Equal("low", map[Low]);
        Assert.Equal("high", map[High]);

        PersistentHashMap<int, string, Int32IdentityHasher> withoutHigh = map.Remove(High);
        Assert.Equal(1, withoutHigh.Count);
        Assert.Equal("low", withoutHigh[Low]);
    }

    [Fact]
    public void NegativeKeys_ShouldRoundTrip()
    {
        // Identity hashing makes a negative key a hash with the sign bit set; the descent must shift it in
        // unsigned or the top level reads the wrong slot.
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap
            .Add(-1, "minus one")
            .Add(int.MinValue, "min")
            .Add(int.MaxValue, "max");

        Assert.Equal("minus one", map[-1]);
        Assert.Equal("min", map[int.MinValue]);
        Assert.Equal("max", map[int.MaxValue]);
    }

    // ── The out-of-band default(TKey) slot ───────────────────────────────────────

    [Fact]
    public void DefaultKey_ShouldBehaveLikeAnyOtherEntry()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(0, "zero").Add(1, "one");

        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey(0));
        Assert.Equal("zero", map[0]);
        Assert.Equal(
            new (int Key, string? Value)[] { (0, "zero"), (1, "one") },
            map.Select(entry => (entry.Key, entry.Value)).OrderBy(pair => pair.Key).ToArray());
    }

    [Fact]
    public void DefaultKey_Add_ShouldThrowOnADuplicate()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(0, "zero");

        Assert.Throws<ArgumentException>(() => map.Add(0, "nil"));
    }

    [Fact]
    public void DefaultKey_SetItem_ShouldOverwrite_AndReturnTheReceiverWhenUnchanged()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(0, "zero");

        Assert.Same(map, map.SetItem(0, "zero"));

        PersistentHashMap<int, string, Int32IdentityHasher> updated = map.SetItem(0, "nil");
        Assert.Equal("nil", updated[0]);
        Assert.Equal("zero", map[0]);
        Assert.Equal(1, updated.Count);
    }

    [Fact]
    public void DefaultKey_Remove_ShouldLeaveTheTrieAlone()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(0, "zero").Add(1, "one");

        PersistentHashMap<int, string, Int32IdentityHasher> without = map.Remove(0);

        Assert.Equal(1, without.Count);
        Assert.False(without.ContainsKey(0));
        Assert.Equal("one", without[1]);
        Assert.True(map.ContainsKey(0));
    }

    [Fact]
    public void DefaultKey_Remove_ShouldBeANoOp_WhenAbsent()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(1, "one");

        Assert.Same(map, map.Remove(0));
    }

    [Fact]
    public void DefaultKey_Remove_ShouldReturnEmpty_WhenItIsTheOnlyEntry()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(0, "zero");

        Assert.Same(EmptyMap, map.Remove(0));
    }

    [Fact]
    public void NullKey_ShouldWorkWithAHasherThatWouldThrowOnOne()
    {
        // DefaultHasher<string> forwards to EqualityComparer<string>.Default.GetHashCode, which throws on a
        // null argument. The out-of-band slot is what keeps the null key from ever reaching it.
        PersistentHashMap<string, int, DefaultHasher<string>> map =
            PersistentHashMap<string, int, DefaultHasher<string>>.Empty
                .Add(null!, 0)
                .Add("a", 1);

        Assert.Equal(2, map.Count);
        Assert.Equal(0, map[null!]);
        Assert.Equal(1, map["a"]);
        Assert.Equal(1, map.Remove(null!).Count);
    }

    // ── Reads ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ContainsValue_ShouldFindValuesInTheTrieAndInTheDefaultSlot()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap
            .Add(0, "zero")
            .Add(1, "one")
            .Add(2, "two");

        Assert.True(map.ContainsValue("zero"));
        Assert.True(map.ContainsValue("two"));
        Assert.False(map.ContainsValue("three"));
        Assert.False(EmptyMap.ContainsValue("zero"));
    }

    [Fact]
    public void KeysAndValues_ShouldExposeEveryEntry()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = BuildRange(200).Add(0, "zero");

        Assert.Equal(map.Count, map.Keys.Count);
        Assert.Equal(map.Count, map.Values.Count);
        Assert.Equal(Enumerable.Range(0, 200).ToArray(), map.Keys.OrderBy(key => key).ToArray());
        Assert.Equal(map.Count, map.Values.Distinct().Count());
    }

    // ── Construction from a sequence ─────────────────────────────────────────────

    [Fact]
    public void SequenceConstructor_ShouldCopyEveryEntry()
    {
        KeyValuePair<int, string>[] source =
        [
            new(0, "zero"),
            new(1, "one"),
            new(2, "two"),
        ];

        var map = new PersistentHashMap<int, string, Int32IdentityHasher>(source);

        Assert.Equal(3, map.Count);
        Assert.Equal("zero", map[0]);
        Assert.Equal("two", map[2]);
    }

    [Fact]
    public void SequenceConstructor_ShouldThrow_WhenTheSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new PersistentHashMap<int, string, Int32IdentityHasher>(null!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void SequenceConstructor_ShouldThrow_OnDuplicateKeys()
    {
        KeyValuePair<int, string>[] source = [new(1, "one"), new(1, "uno")];

        Assert.Throws<ArgumentException>(
            () => new PersistentHashMap<int, string, Int32IdentityHasher>(source));
    }

    [Fact]
    public void SequenceConstructor_ShouldThrow_OnADuplicateDefaultKey()
    {
        KeyValuePair<int, string>[] source = [new(0, "zero"), new(0, "nil")];

        Assert.Throws<ArgumentException>(
            () => new PersistentHashMap<int, string, Int32IdentityHasher>(source));
    }

    [Fact]
    public void SequenceConstructor_ShouldAcceptAnEmptySource()
    {
        var map = new PersistentHashMap<int, string, Int32IdentityHasher>([]);

        Assert.True(map.IsEmpty);
    }

    // Keys 1..count-1 mapped to their decimal spelling; key 0 is deliberately left out so callers that want
    // the out-of-band slot exercised have to add it themselves.
    private static PersistentHashMap<int, string, Int32IdentityHasher> BuildRange(int count)
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap;
        for (int key = 1; key < count; key++)
            map = map.Add(key, key.ToString());

        return map;
    }
}
