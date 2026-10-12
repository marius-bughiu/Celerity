using System.Reflection;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

// SlotMap<TValue>: the generational slot map. Mirrors SparseMapTests for the dictionary-shaped
// surface, and adds the part a slot map exists for — that a key, once retired, never resolves
// again, even after its slot is reused.
public class SlotMapTests
{
    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldCreateAnEmptyMap()
    {
        var map = new SlotMap<int>();

        Assert.Equal(0, map.Count);
        Assert.Equal(0, map.Capacity);
    }

    [Fact]
    public void Constructor_WithCapacity_ShouldPreSizeTheSlotArray()
    {
        var map = new SlotMap<int>(10);

        Assert.Equal(10, map.Capacity);

        for (int i = 0; i < 10; i++)
            map.Add(i);

        Assert.Equal(10, map.Capacity);
    }

    [Fact]
    public void Constructor_WithNegativeCapacity_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SlotMap<int>(-1));
        Assert.Equal("capacity", ex.ParamName);
    }

    // ── Add and lookup ────────────────────────────────────────────────────────

    [Fact]
    public void Add_ShouldIssueDistinctKeysThatResolve()
    {
        var map = new SlotMap<string>();

        SlotKey a = map.Add("a");
        SlotKey b = map.Add("b");
        SlotKey c = map.Add("c");

        Assert.Equal(3, map.Count);
        Assert.NotEqual(a, b);
        Assert.NotEqual(b, c);
        Assert.Equal("a", map[a]);
        Assert.Equal("b", map[b]);
        Assert.Equal("c", map[c]);
    }

    [Fact]
    public void Add_ShouldAcceptDuplicateAndNullValues()
    {
        var map = new SlotMap<string?>();

        SlotKey first = map.Add(null);
        SlotKey second = map.Add(null);

        Assert.NotEqual(first, second);
        Assert.Equal(2, map.Count);
        Assert.True(map.TryGetValue(first, out string? value));
        Assert.Null(value);
    }

    [Fact]
    public void Add_ShouldGrowPastAnyInitialCapacity()
    {
        var map = new SlotMap<int>();
        var keys = new List<SlotKey>();

        for (int i = 0; i < 1_000; i++)
            keys.Add(map.Add(i));

        Assert.Equal(1_000, map.Count);
        Assert.True(map.Capacity >= 1_000);
        for (int i = 0; i < keys.Count; i++)
            Assert.Equal(i, map[keys[i]]);
    }

    [Fact]
    public void FirstKeyEverIssued_ShouldNotEqualTheDefaultKey()
    {
        var map = new SlotMap<int>();

        SlotKey first = map.Add(42);

        Assert.NotEqual(default, first);
        Assert.Equal("#0.1", first.ToString());
    }

    [Fact]
    public void DefaultKey_ShouldBeAbsent_FromAnEmptyAndANonEmptyMap()
    {
        var map = new SlotMap<int>();
        AssertAbsent(map, default);

        map.Add(1);
        map.Add(2);

        AssertAbsent(map, default);
    }

    [Fact]
    public void ContainsKey_ShouldReportLiveKeysOnly()
    {
        var map = new SlotMap<int>();
        SlotKey live = map.Add(1);
        SlotKey retired = map.Add(2);
        map.Remove(retired);

        Assert.True(map.ContainsKey(live));
        Assert.False(map.ContainsKey(retired));
    }

    // ── The generational guarantee ───────────────────────────────────────────

    [Fact]
    public void Remove_ShouldRetireTheKey_EvenOnceItsSlotIsReused()
    {
        var map = new SlotMap<string>();
        SlotKey old = map.Add("old");

        Assert.True(map.Remove(old));
        SlotKey reused = map.Add("new");

        // The slot was reused (the free list is consulted before a new slot is issued) but under a
        // new version, so the old key does not see the new occupant — the ABA hazard a List plus a
        // free list of indices has. Versions step on fill and on vacate, so live ones are odd.
        Assert.Equal("#0.1", old.ToString());
        Assert.Equal("#0.3", reused.ToString());
        Assert.NotEqual(old, reused);
        AssertAbsent(map, old);
        Assert.Equal("new", map[reused]);
    }

    [Fact]
    public void Clear_ShouldRetireEveryKey_AndReuseTheSlotsUnderNewVersions()
    {
        var map = new SlotMap<int>();
        var before = new List<SlotKey>();
        for (int i = 0; i < 8; i++)
            before.Add(map.Add(i));

        int capacity = map.Capacity;
        map.Clear();

        Assert.Equal(0, map.Count);
        Assert.Equal(capacity, map.Capacity);
        foreach (SlotKey key in before)
            AssertAbsent(map, key);

        var after = new List<SlotKey>();
        for (int i = 0; i < 8; i++)
            after.Add(map.Add(100 + i));

        Assert.Equal(capacity, map.Capacity);
        Assert.Empty(before.Intersect(after));
        foreach (SlotKey key in before)
            AssertAbsent(map, key);
    }

    [Fact]
    public void Clear_ShouldNotStepTheVersionOfASlotThatWasAlreadyVacant()
    {
        // A vacant slot's version must move only when *that* slot is vacated, or the documented
        // 2^31-reuse period is not the real one: an unrelated Clear would walk it round faster.
        var map = new SlotMap<int>();
        SlotKey a = map.Add(1); // slot 0, version 1
        map.Add(2);             // slot 1, version 1
        map.Remove(a);          // slot 0 -> version 2, vacant

        map.Clear();            // slot 1 -> version 2; slot 0 must stay at 2

        SlotKey c = map.Add(3); // the free list hands back slot 1 first (vacated last)
        SlotKey d = map.Add(4); // then slot 0

        Assert.Equal("#1.3", c.ToString());
        Assert.Equal("#0.3", d.ToString());
    }

    [Fact]
    public void Versions_ShouldWrapToAVacantZero_SoTheDefaultKeyNeverResolves()
    {
        var map = new SlotMap<int>();
        SlotKey first = map.Add(1);

        // Park live slot 0 at the last odd version, then vacate it: the wrap lands on 0, which is even
        // and so vacant, and the next fill is version 1 again. A live slot is never at version 0, which
        // is what keeps the default key from ever resolving.
        SetSlotVersion(map, 0, uint.MaxValue);
        SlotKey atCeiling = map.Keys.Single();
        Assert.Equal($"#0.{uint.MaxValue}", atCeiling.ToString());

        Assert.True(map.Remove(atCeiling));
        SlotKey wrapped = map.Add(2);

        Assert.Equal("#0.1", wrapped.ToString());
        AssertAbsent(map, default);

        // The documented ceiling, stated rather than hidden: the very first key issued for this slot
        // carries version 1 too, so after a full cycle it resolves again.
        Assert.Equal(first, wrapped);
    }

    [Fact]
    public void DefaultKey_ShouldStayAbsent_WhileSlotZeroSitsVacantAtTheWrappedVersion()
    {
        // Regression: vacating a slot at uint.MaxValue wraps its version to 0 — the version the
        // default key carries — while the slot is below the high-water mark. Version equality alone
        // would resolve the default key to that vacant slot and read a released value.
        var map = new SlotMap<string>();
        map.Add("a");
        map.Add("b");
        SetSlotVersion(map, 0, uint.MaxValue);

        Assert.True(map.Remove(map.Keys.First()));

        AssertAbsent(map, default);
        Assert.False(map.Remove(default));
        Assert.Throws<KeyNotFoundException>(() => map[default] = "x");
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void KeyFromAnotherMap_ShouldNeverResolveAVacantSlot()
    {
        // Every issued key carries an odd version and every vacant slot an even one, so a foreign key
        // that lands on a vacant slot cannot match it, whatever the two maps' histories.
        var a = new SlotMap<int>();
        SlotKey ax = a.Add(10);
        a.Add(20);
        a.Remove(ax);

        var b = new SlotMap<int>();
        b.Remove(b.Add(0));
        SlotKey foreign = b.Add(99);
        Assert.Equal("#0.3", foreign.ToString());

        AssertAbsent(a, foreign);
        Assert.Equal(1, a.Count);
    }

    [Fact]
    public void KeyBeyondTheIssuedSlots_ShouldBeAbsent()
    {
        var big = new SlotMap<int>();
        SlotKey far = default;
        for (int i = 0; i < 10; i++)
            far = big.Add(i);

        var small = new SlotMap<int>(64);
        small.Add(1);

        // Slot 9 lies inside small's capacity but beyond its high-water mark.
        AssertAbsent(small, far);
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_ShouldKeepEveryOtherKeyValid()
    {
        var map = new SlotMap<int>();
        var keys = new List<SlotKey>();
        for (int i = 0; i < 10; i++)
            keys.Add(map.Add(i * 10));

        Assert.True(map.Remove(keys[0]));
        Assert.True(map.Remove(keys[3]));
        Assert.True(map.Remove(keys[9]));

        Assert.Equal(7, map.Count);
        for (int i = 0; i < keys.Count; i++)
        {
            if (i is 0 or 3 or 9)
                AssertAbsent(map, keys[i]);
            else
                Assert.Equal(i * 10, map[keys[i]]);
        }
    }

    [Fact]
    public void Remove_OfTheOnlyValue_ShouldEmptyTheMap()
    {
        var map = new SlotMap<int>();
        SlotKey key = map.Add(7);

        Assert.True(map.Remove(key, out int value));

        Assert.Equal(7, value);
        Assert.Equal(0, map.Count);
        AssertAbsent(map, key);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_ForARetiredOrDefaultKey()
    {
        var map = new SlotMap<int>();
        SlotKey key = map.Add(7);
        map.Remove(key);

        Assert.False(map.Remove(key));
        Assert.False(map.Remove(key, out int value));
        Assert.Equal(0, value);
        Assert.False(map.Remove(default));
    }

    [Fact]
    public void Remove_ShouldReleaseTheReference()
    {
        var map = new SlotMap<string>();
        SlotKey a = map.Add("a");
        map.Add("b");

        map.Remove(a);

        string?[] values = ReadValues(map);
        Assert.Null(values[0]);
        Assert.Equal("b", values[1]);
    }

    [Fact]
    public void Clear_ShouldReleaseEveryReference()
    {
        var map = new SlotMap<string>();
        map.Add("a");
        map.Add("b");

        map.Clear();

        Assert.All(ReadValues(map), Assert.Null);
    }

    [Fact]
    public void Clear_OnAnEmptyMap_ShouldBeANoOp()
    {
        var map = new SlotMap<int>(4);

        map.Clear();

        Assert.Equal(0, map.Count);
        Assert.Equal(4, map.Capacity);
    }

    // ── Indexer ───────────────────────────────────────────────────────────────

    [Fact]
    public void IndexerSet_ShouldOverwriteInPlace()
    {
        var map = new SlotMap<string>();
        SlotKey key = map.Add("a");

        map[key] = "b";

        Assert.Equal("b", map[key]);
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void IndexerSet_OnARetiredKey_ShouldThrowAndNotInsert()
    {
        var map = new SlotMap<string>();
        SlotKey key = map.Add("a");
        map.Remove(key);

        Assert.Throws<KeyNotFoundException>(() => map[key] = "b");
        Assert.Throws<KeyNotFoundException>(() => map[default] = "b");
        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void IndexerGet_OnARetiredKey_ShouldThrowNamingTheKey()
    {
        var map = new SlotMap<string>();
        SlotKey key = map.Add("a");
        map.Remove(key);

        var ex = Assert.Throws<KeyNotFoundException>(() => map[key]);
        Assert.Contains("#0.1", ex.Message);
    }

    // ── Capacity ──────────────────────────────────────────────────────────────

    [Fact]
    public void EnsureCapacity_ShouldGrowAndNeverShrink()
    {
        var map = new SlotMap<int>();
        SlotKey key = map.Add(5);

        Assert.Equal(100, map.EnsureCapacity(100));
        Assert.Equal(100, map.Capacity);
        Assert.Equal(100, map.EnsureCapacity(10));
        Assert.Equal(5, map[key]);
    }

    [Fact]
    public void EnsureCapacity_WithNegativeCapacity_ShouldThrow()
    {
        var map = new SlotMap<int>();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => map.EnsureCapacity(-1));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void EnsureCapacity_ShouldInvalidateEnumerators_OnlyWhenItGrows()
    {
        var map = new SlotMap<int>(8);
        map.Add(1);

        var unchanged = map.GetEnumerator();
        map.EnsureCapacity(4);
        Assert.True(unchanged.MoveNext());

        var grown = map.GetEnumerator();
        map.EnsureCapacity(64);
        Assert.Throws<InvalidOperationException>(() => grown.MoveNext());
    }

    // ── Views and interfaces ─────────────────────────────────────────────────

    [Fact]
    public void KeyCollection_ShouldBeAReadOnlyView()
    {
        var map = new SlotMap<string>();
        SlotKey a = map.Add("a");
        SlotKey b = map.Add("b");
        ICollection<SlotKey> keys = map.Keys;

        Assert.Equal(2, keys.Count);
        Assert.True(keys.IsReadOnly);
        Assert.True(keys.Contains(a));
        map.Remove(b);
        Assert.False(map.Keys.Contains(b));

        Assert.Throws<NotSupportedException>(() => keys.Add(a));
        Assert.Throws<NotSupportedException>(() => keys.Clear());
        Assert.Throws<NotSupportedException>(() => keys.Remove(a));
    }

    [Fact]
    public void ValueCollection_ShouldBeAReadOnlyView()
    {
        var map = new SlotMap<string>();
        map.Add("a");
        map.Add("b");
        ICollection<string?> values = map.Values;

        Assert.Equal(2, values.Count);
        Assert.True(values.IsReadOnly);
        Assert.True(values.Contains("b"));
        Assert.False(values.Contains("z"));

        Assert.Throws<NotSupportedException>(() => values.Add("c"));
        Assert.Throws<NotSupportedException>(() => values.Clear());
        Assert.Throws<NotSupportedException>(() => values.Remove("a"));
    }

    [Fact]
    public void CopyTo_ShouldEmitEveryPairAtTheOffset()
    {
        var map = new SlotMap<string>();
        SlotKey a = map.Add("a");
        SlotKey b = map.Add("b");

        var pairs = new KeyValuePair<SlotKey, string?>[4];
        map.CopyTo(pairs, 1);
        var keys = new SlotKey[3];
        map.Keys.CopyTo(keys, 1);
        var values = new string?[3];
        map.Values.CopyTo(values, 1);

        Assert.Equal(default, pairs[0].Key);
        Assert.Equal(new[] { a, b }, pairs.Skip(1).Take(2).Select(p => p.Key).Order(KeyOrder));
        Assert.Equal(new[] { a, b }, keys.Skip(1).Order(KeyOrder));
        Assert.Equal(new[] { "a", "b" }, values.Skip(1).Order());
    }

    [Fact]
    public void ReadOnlyDictionarySurface_ShouldMirrorTheConcreteSurface()
    {
        var concrete = new SlotMap<string>();
        SlotKey a = concrete.Add("a");
        SlotKey b = concrete.Add("b");
        IReadOnlyDictionary<SlotKey, string?> map = concrete;

        Assert.Equal("a", map[a]);
        Assert.Equal(new[] { a, b }, map.Keys.Order(KeyOrder));
        Assert.Equal(new[] { "a", "b" }, map.Values.Order());
        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey(b));
        Assert.True(map.TryGetValue(b, out string? value));
        Assert.Equal("b", value);
        Assert.Equal(2, map.Count());
        Assert.Throws<KeyNotFoundException>(() => map[default]);
    }

    // ── SlotKey ───────────────────────────────────────────────────────────────

    [Fact]
    public void SlotKey_EqualityMembers_ShouldAgree()
    {
        var map = new SlotMap<int>();
        SlotKey a = map.Add(1);
        SlotKey b = map.Add(2);
        SlotKey aCopy = map.Keys.First(k => k == a);

        Assert.True(a == aCopy);
        Assert.False(a != aCopy);
        Assert.True(a != b);
        Assert.False(a == b);
        Assert.True(a.Equals(aCopy));
        Assert.True(a.Equals((object)aCopy));
        Assert.False(a.Equals((object)b));
        Assert.False(a.Equals("#0.1"));
        Assert.Equal(a.GetHashCode(), aCopy.GetHashCode());
    }

    [Fact]
    public void SlotKey_ShouldDistinguishTheVersion_NotOnlyTheSlot()
    {
        var map = new SlotMap<int>();
        SlotKey old = map.Add(1);
        map.Remove(old);
        SlotKey reused = map.Add(2);

        Assert.NotEqual(old, reused);
        Assert.False(old.Equals((object)reused));
    }

    [Fact]
    public void SlotKey_Default_ShouldRenderAsSlotZeroVersionZero()
    {
        Assert.Equal("#0.0", default(SlotKey).ToString());
    }

    [Fact]
    public void SlotKey_ShouldWorkAsADictionaryKey()
    {
        var map = new SlotMap<string>();
        var names = new Dictionary<SlotKey, string>();
        for (int i = 0; i < 50; i++)
        {
            SlotKey key = map.Add($"v{i}");
            names[key] = $"v{i}";
        }

        foreach (KeyValuePair<SlotKey, string?> entry in map)
            Assert.Equal(names[entry.Key], entry.Value);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly Comparer<SlotKey> KeyOrder =
        Comparer<SlotKey>.Create((x, y) => string.CompareOrdinal(x.ToString(), y.ToString()));

    private static void AssertAbsent<TValue>(SlotMap<TValue> map, SlotKey key)
    {
        Assert.False(map.ContainsKey(key));
        Assert.False(map.TryGetValue(key, out TValue? value));
        Assert.Equal(default, value);
        Assert.Throws<KeyNotFoundException>(() => map[key]);
        Assert.False(map.Keys.Contains(key));
    }

    // Reads every slot's stored value, so reference release can be asserted directly: nothing in the
    // public surface can distinguish "left behind but unreachable" from "cleared".
    private static TValue?[] ReadValues<TValue>(SlotMap<TValue> map)
    {
        var slots = (Array)typeof(SlotMap<TValue>)
            .GetField("_slots", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(map)!;

        var values = new TValue?[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            object entry = slots.GetValue(i)!;
            values[i] = (TValue?)entry.GetType().GetField("Value")!.GetValue(entry);
        }

        return values;
    }

    // Writes one slot's version through reflection. Each remove-and-refill cycle steps a version twice, so
    // reaching the live version uint.MaxValue from 1 honestly takes 2^31 - 1 cycles of the same slot (about
    // two billion); this parks it there so the wrap itself can be observed.
    private static void SetSlotVersion<TValue>(SlotMap<TValue> map, int slot, uint version)
    {
        FieldInfo slotsField = typeof(SlotMap<TValue>)
            .GetField("_slots", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var slots = (Array)slotsField.GetValue(map)!;

        object entry = slots.GetValue(slot)!;
        entry.GetType().GetField("Version")!.SetValue(entry, version);
        slots.SetValue(entry, slot);
    }
}
