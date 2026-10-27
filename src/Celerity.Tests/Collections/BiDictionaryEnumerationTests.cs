using System.Collections;
using System.Collections.Generic;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Keys / Values / Inverse / GetEnumerator on BiDictionary.
//
// Mirror of LinkedDictionaryEnumerationTests. BiDictionary enumerates its dense entry array, so the
// order is insertion order until the first removal, after which the last entry occupies the removed
// one's slot. Four enumerator shapes share one cursor — the pair enumerator, Keys, Values and the
// Inverse view — and every one of them must fail fast on a structural change, including one made
// after the enumerator ran off the end. A value overwrite (indexer or ForceSet without an eviction)
// is not structural, matching BCL Dictionary<,>.
public class BiDictionaryEnumerationTests
{
    private static BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher> NewIntMap(int capacity = 16)
        => new(capacity);

    private static BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher> Filled(int count)
    {
        var map = NewIntMap();
        for (int i = 1; i <= count; i++)
            map[i] = i * 10;
        return map;
    }

    // The four enumerator shapes, each driven through its struct type and exposed as one delegate pair.
    public enum Shape
    {
        Pairs,
        Keys,
        Values,
        Inverse,
    }

    private sealed class Probe
    {
        public required Func<bool> MoveNext { get; init; }
        public required Action Reset { get; init; }
        public required Func<object?> Current { get; init; }
    }

    private static Probe ProbeFor(BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher> map, Shape shape)
    {
        switch (shape)
        {
            case Shape.Pairs:
            {
                var e = map.GetEnumerator();
                return new Probe { MoveNext = () => e.MoveNext(), Reset = () => e.Reset(), Current = () => e.Current };
            }
            case Shape.Keys:
            {
                var e = map.Keys.GetEnumerator();
                return new Probe { MoveNext = () => e.MoveNext(), Reset = () => e.Reset(), Current = () => e.Current };
            }
            case Shape.Values:
            {
                var e = map.Values.GetEnumerator();
                return new Probe { MoveNext = () => e.MoveNext(), Reset = () => e.Reset(), Current = () => e.Current };
            }
            default:
            {
                var e = map.Inverse.GetEnumerator();
                return new Probe { MoveNext = () => e.MoveNext(), Reset = () => e.Reset(), Current = () => e.Current };
            }
        }
    }

    // Every structural mutation, applied to a map holding keys 1..3 mapped to 10, 20, 30.
    public enum Mutation
    {
        IndexerAdd,
        Add,
        TryAdd,
        ForceSetNewKey,
        ForceSetEvicting,
        Remove,
        RemoveOut,
        RemoveValue,
        RemoveValueOut,
        RemovePair,
        AddPair,
        Clear,
        EnsureCapacityGrow,
        TrimExcessRebuild,
        GrowOnAdd,
    }

    private static void Apply(BiDictionary<int, int, Int32WangNaiveHasher, Int32WangNaiveHasher> map, Mutation mutation)
    {
        switch (mutation)
        {
            case Mutation.IndexerAdd: map[4] = 40; break;
            case Mutation.Add: map.Add(4, 40); break;
            case Mutation.TryAdd: Assert.True(map.TryAdd(4, 40)); break;
            case Mutation.ForceSetNewKey: Assert.False(map.ForceSet(4, 40)); break;
            case Mutation.ForceSetEvicting: Assert.True(map.ForceSet(1, 20)); break;
            case Mutation.Remove: Assert.True(map.Remove(2)); break;
            case Mutation.RemoveOut: Assert.True(map.Remove(2, out _)); break;
            case Mutation.RemoveValue: Assert.True(map.RemoveValue(20)); break;
            case Mutation.RemoveValueOut: Assert.True(map.RemoveValue(20, out _)); break;
            case Mutation.RemovePair: Assert.True(((ICollection<KeyValuePair<int, int>>)map).Remove(new(2, 20))); break;
            case Mutation.AddPair: ((ICollection<KeyValuePair<int, int>>)map).Add(new(4, 40)); break;
            case Mutation.Clear: map.Clear(); break;
            case Mutation.EnsureCapacityGrow: map.EnsureCapacity(1000); break;
            case Mutation.TrimExcessRebuild: map.TrimExcess(); break;
            case Mutation.GrowOnAdd:
                for (int i = 4; i <= 40; i++)
                    map[i] = i * 10;
                break;
        }
    }

    public static IEnumerable<object[]> EveryShapeAndMutation()
    {
        foreach (Shape shape in Enum.GetValues<Shape>())
        {
            foreach (Mutation mutation in Enum.GetValues<Mutation>())
                yield return new object[] { shape, mutation };
        }
    }

    public static IEnumerable<object[]> EveryShape()
    {
        foreach (Shape shape in Enum.GetValues<Shape>())
            yield return new object[] { shape };
    }

    // ---------------------------------------------------------------
    //  Basic enumeration
    // ---------------------------------------------------------------

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var map = NewIntMap();

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (var kvp in map)
            pairs.Add(kvp);

        Assert.Empty(pairs);
        Assert.Empty(map.Keys);
        Assert.Empty(map.Values);
        Assert.Empty(map.Inverse);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldSingleEntry()
    {
        var map = NewIntMap();
        map[7] = 70;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (var kvp in map)
            pairs.Add(kvp);

        var single = Assert.Single(pairs);
        Assert.Equal(7, single.Key);
        Assert.Equal(70, single.Value);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryEntryExactlyOnce_InInsertionOrder()
    {
        var map = NewIntMap();
        for (int i = 50; i >= 1; i--)
            map[i] = i * 10;

        var seen = new List<KeyValuePair<int, int>>();
        foreach (var kvp in map)
            seen.Add(kvp);

        Assert.Equal(Enumerable.Range(1, 50).Reverse().Select(i => new KeyValuePair<int, int>(i, i * 10)), seen);
    }

    [Fact]
    public void GetEnumerator_ShouldIncludeDefaultKeyAndValue_AtTheirInsertionPositions()
    {
        var map = new BiDictionary<int, string, Int32WangNaiveHasher, StringFnV1AHasher>();
        map[1] = "one";
        map[0] = "zero";
        map[42] = null!;

        var seen = new List<KeyValuePair<int, string?>>();
        foreach (var kvp in map)
            seen.Add(kvp!);

        Assert.Equal(new[]
        {
            new KeyValuePair<int, string?>(1, "one"),
            new KeyValuePair<int, string?>(0, "zero"),
            new KeyValuePair<int, string?>(42, null),
        }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldIncludeNullKeyEntry_AtItsInsertionPosition_ForStringKey()
    {
        var map = new BiDictionary<string, int, StringFnV1AHasher, Int32WangNaiveHasher>();
        map["alpha"] = 1;
        map[null!] = 999;
        map["beta"] = 2;

        var keys = new List<string?>();
        var values = new List<int>();
        foreach (var kvp in map)
        {
            keys.Add(kvp.Key);
            values.Add(kvp.Value);
        }

        Assert.Equal(new[] { "alpha", null, "beta" }, keys);
        Assert.Equal(new[] { 1, 999, 2 }, values);
    }

    [Fact]
    public void GetEnumerator_ShouldPutTheLastEntryInTheRemovedSlot()
    {
        var map = Filled(5);
        map.Remove(2);

        var seen = new List<int>();
        foreach (var kvp in map)
            seen.Add(kvp.Key);

        Assert.Equal(new[] { 1, 5, 3, 4 }, seen);
        Assert.Equal(new[] { 1, 5, 3, 4 }, map.Keys.ToArray());
        Assert.Equal(new[] { 10, 50, 30, 40 }, map.Values.ToArray());
        Assert.Equal(new[] { 10, 50, 30, 40 }, map.Inverse.Select(p => p.Key).ToArray());
    }

    [Fact]
    public void GetEnumerator_ShouldKeepInsertionOrder_WhenTheLastEntryIsRemoved()
    {
        var map = Filled(4);
        map.Remove(4);
        map[9] = 90;

        Assert.Equal(new[] { 1, 2, 3, 9 }, map.Keys.ToArray());
    }

    [Fact]
    public void GetEnumerator_ShouldKeepAReboundKeysSlot()
    {
        var map = Filled(4);
        map[2] = 200;
        Assert.False(map.ForceSet(3, 300));

        Assert.Equal(new[] { 1, 2, 3, 4 }, map.Keys.ToArray());
        Assert.Equal(new[] { 10, 200, 300, 40 }, map.Values.ToArray());
    }

    [Fact]
    public void GetEnumerator_ShouldReflectClear()
    {
        var map = Filled(10);
        map.Clear();

        int count = 0;
        foreach (var _ in map)
            count++;

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetEnumerator_ShouldSurviveResize_InOrder()
    {
        var map = NewIntMap(capacity: 16);
        for (int i = 1; i <= 200; i++)
            map[i] = -i;

        var seen = new List<int>();
        foreach (var kvp in map)
        {
            Assert.Equal(-kvp.Key, kvp.Value);
            seen.Add(kvp.Key);
        }

        Assert.Equal(Enumerable.Range(1, 200), seen);
    }

    // ---------------------------------------------------------------
    //  Current, Reset, Dispose — every shape
    // ---------------------------------------------------------------

    [Fact]
    public void Current_ShouldBeDefault_BeforeFirstMoveNextAndAfterTheEnd()
    {
        var map = NewIntMap();
        map[3] = 30;

        var enumerator = map.GetEnumerator();
        Assert.Equal(default, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(new KeyValuePair<int, int>(3, 30), enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(default, enumerator.Current);
        Assert.False(enumerator.MoveNext()); // stays exhausted
        enumerator.Dispose();
    }

    [Fact]
    public void Current_ShouldBeDefault_BeforeAndAfter_ForEveryView()
    {
        var map = NewIntMap();
        map[3] = 30;

        var keys = map.Keys.GetEnumerator();
        Assert.Equal(0, keys.Current);
        Assert.True(keys.MoveNext());
        Assert.Equal(3, keys.Current);
        Assert.False(keys.MoveNext());
        Assert.Equal(0, keys.Current);
        keys.Dispose();

        var values = map.Values.GetEnumerator();
        Assert.Equal(0, values.Current);
        Assert.True(values.MoveNext());
        Assert.Equal(30, values.Current);
        Assert.False(values.MoveNext());
        Assert.Equal(0, values.Current);
        values.Dispose();

        var inverse = map.Inverse.GetEnumerator();
        Assert.Equal(default, inverse.Current);
        Assert.True(inverse.MoveNext());
        Assert.Equal(new KeyValuePair<int, int>(30, 3), inverse.Current);
        Assert.False(inverse.MoveNext());
        Assert.Equal(default, inverse.Current);
        inverse.Dispose();
    }

    [Fact]
    public void Enumerator_ShouldBeReusableViaReset()
    {
        var map = Filled(2);

        var enumerator = map.GetEnumerator();
        var firstPass = new List<int>();
        while (enumerator.MoveNext()) firstPass.Add(enumerator.Current.Key);

        enumerator.Reset();
        var secondPass = new List<int>();
        while (enumerator.MoveNext()) secondPass.Add(enumerator.Current.Key);

        Assert.Equal(new[] { 1, 2 }, firstPass);
        Assert.Equal(firstPass, secondPass);
    }

    [Theory]
    [MemberData(nameof(EveryShape))]
    public void Reset_Midway_ShouldRestartFromTheFirstSlot(Shape shape)
    {
        var map = Filled(3);
        var probe = ProbeFor(map, shape);

        Assert.True(probe.MoveNext());
        object? first = probe.Current();
        Assert.True(probe.MoveNext());
        probe.Reset();
        Assert.True(probe.MoveNext());
        Assert.Equal(first, probe.Current());
    }

    [Fact]
    public void ResetMidway_ShouldClearCurrent_ForEveryShape()
    {
        var map = Filled(3);

        var pairs = map.GetEnumerator();
        Assert.True(pairs.MoveNext());
        pairs.Reset();
        Assert.Equal(default, pairs.Current);

        var keys = map.Keys.GetEnumerator();
        Assert.True(keys.MoveNext());
        keys.Reset();
        Assert.Equal(0, keys.Current);

        var values = map.Values.GetEnumerator();
        Assert.True(values.MoveNext());
        values.Reset();
        Assert.Equal(0, values.Current);

        var inverse = map.Inverse.GetEnumerator();
        Assert.True(inverse.MoveNext());
        inverse.Reset();
        Assert.Equal(default, inverse.Current);
    }

    // ---------------------------------------------------------------
    //  Fail-fast on every structural mutation, for every shape
    // ---------------------------------------------------------------

    [Theory]
    [MemberData(nameof(EveryShapeAndMutation))]
    public void MoveNext_ShouldThrow_AfterAStructuralMutation(Shape shape, Mutation mutation)
    {
        var map = Filled(3);
        var probe = ProbeFor(map, shape);
        Assert.True(probe.MoveNext());

        Apply(map, mutation);

        Assert.Throws<InvalidOperationException>(() => probe.MoveNext());
        Assert.Throws<InvalidOperationException>(() => probe.Reset());
    }

    [Theory]
    [MemberData(nameof(EveryShapeAndMutation))]
    public void MoveNext_ShouldThrow_AfterAStructuralMutation_EvenOnceExhausted(Shape shape, Mutation mutation)
    {
        var map = Filled(3);
        var probe = ProbeFor(map, shape);
        while (probe.MoveNext()) { }
        Assert.False(probe.MoveNext()); // exhausted, not yet invalid

        Apply(map, mutation);

        Assert.Throws<InvalidOperationException>(() => probe.MoveNext());
        Assert.Throws<InvalidOperationException>(() => probe.Reset());
    }

    [Theory]
    [MemberData(nameof(EveryShape))]
    public void MoveNext_ShouldThrow_BeforeTheFirstMoveNext_WhenMutatedAfterCreation(Shape shape)
    {
        var map = Filled(3);
        var probe = ProbeFor(map, shape);

        map.Remove(1);

        Assert.Throws<InvalidOperationException>(() => probe.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenDefaultKeyInsertedDuringEnumeration()
    {
        var map = NewIntMap();
        map[1] = 10;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        map[0] = 0; // the default key and value are ordinary in-table entries here

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    // ---------------------------------------------------------------
    //  Non-structural operations must not invalidate
    // ---------------------------------------------------------------

    [Fact]
    public void MoveNext_ShouldNotThrow_AfterNonStructuralOperationsDuringEnumeration()
    {
        // Misses, no-ops and value overwrites leave the entry array as it is, so they must not
        // invalidate the enumerator.
        var map = Filled(3);

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(map.Remove(99));
        Assert.False(map.Remove(99, out _));
        Assert.False(map.RemoveValue(99));
        Assert.False(map.RemoveValue(99, out _));
        Assert.False(((ICollection<KeyValuePair<int, int>>)map).Remove(new(2, 99)));
        Assert.False(map.TryAdd(2, 0));      // duplicate key
        Assert.False(map.TryAdd(9, 20));     // duplicate value
        Assert.Throws<ArgumentException>(() => map.Add(2, 0));
        Assert.Throws<ArgumentException>(() => map.Add(9, 20));
        Assert.Throws<ArgumentException>(() => map[9] = 20); // value owned by another key
        map[1] = 10;                         // same pair
        Assert.False(map.ForceSet(1, 10));   // same pair
        map[2] = 200;                        // value overwrite
        Assert.False(map.ForceSet(3, 300));  // value overwrite, no eviction
        map.EnsureCapacity(1);               // already large enough
        Assert.True(map.ContainsValue(200));
        Assert.True(map.TryGetKey(300, out _));

        var rest = new List<KeyValuePair<int, int>>();
        while (enumerator.MoveNext())
            rest.Add(enumerator.Current);

        Assert.Equal(new[] { new KeyValuePair<int, int>(2, 200), new KeyValuePair<int, int>(3, 300) }, rest);
    }

    [Theory]
    [MemberData(nameof(EveryShape))]
    public void ValueOverwrite_ShouldNotInvalidate_AnyShape(Shape shape)
    {
        var map = Filled(3);
        var probe = ProbeFor(map, shape);
        Assert.True(probe.MoveNext());

        map[1] = 11;
        Assert.False(map.ForceSet(2, 22));
        Assert.False(map.TryAdd(3, 33));
        Assert.False(map.TryAdd(4, 30));

        Assert.True(probe.MoveNext());
        Assert.True(probe.MoveNext());
        Assert.False(probe.MoveNext());
        probe.Reset();
    }

    [Fact]
    public void Clear_OnEmpty_ShouldNotInvalidate()
    {
        var map = NewIntMap();
        var enumerator = map.Inverse.GetEnumerator();

        map.Clear();

        Assert.False(enumerator.MoveNext());
    }

    // ---------------------------------------------------------------
    //  Keys / Values views
    // ---------------------------------------------------------------

    [Fact]
    public void Keys_ShouldYieldEveryKeyInOrder_IncludingDefaultKey()
    {
        var map = NewIntMap();
        map[5] = 105;
        map[0] = 100;
        map[42] = 142;
        map[1] = 101;

        var seen = new List<int>();
        foreach (int k in map.Keys)
            seen.Add(k);

        Assert.Equal(new[] { 5, 0, 42, 1 }, seen);
    }

    [Fact]
    public void Values_ShouldYieldEveryValueInOrder_IncludingDefaultValue()
    {
        var map = new BiDictionary<int, string, Int32WangNaiveHasher, StringFnV1AHasher>();
        map[2] = "two";
        map[0] = null!;
        map[1] = "one";

        var seen = new List<string?>();
        foreach (string? v in map.Values)
            seen.Add(v);

        Assert.Equal(new[] { "two", null, "one" }, seen);
    }

    [Fact]
    public void KeysValuesAndInverse_Count_ShouldMatchDictionaryCount()
    {
        var map = NewIntMap();
        Assert.Equal(0, map.Keys.Count);
        Assert.Equal(0, map.Values.Count);
        Assert.Equal(0, map.Inverse.Count);

        map[0] = 1;
        map[1] = 0;
        Assert.Equal(2, map.Keys.Count);
        Assert.Equal(2, map.Values.Count);
        Assert.Equal(2, map.Inverse.Count);

        map.Remove(0);
        Assert.Equal(1, map.Keys.Count);
        Assert.Equal(1, map.Values.Count);
        Assert.Equal(1, map.Inverse.Count);
    }

    [Fact]
    public void KeysAndValues_ShouldBeReadOnlyCollections()
    {
        var map = NewIntMap();
        map[1] = 10;

        ICollection<int> keys = map.Keys;
        ICollection<int> values = map.Values;

        Assert.True(map.Keys.IsReadOnly);
        Assert.True(map.Values.IsReadOnly);
        Assert.True(keys.IsReadOnly);
        Assert.True(values.IsReadOnly);
        Assert.Equal(1, keys.Count);
        Assert.Equal(1, values.Count);
        Assert.Throws<NotSupportedException>(() => keys.Add(2));
        Assert.Throws<NotSupportedException>(() => keys.Clear());
        Assert.Throws<NotSupportedException>(() => keys.Remove(1));
        Assert.Throws<NotSupportedException>(() => values.Add(2));
        Assert.Throws<NotSupportedException>(() => values.Clear());
        Assert.Throws<NotSupportedException>(() => values.Remove(10));
        Assert.Single(map);
    }

    [Fact]
    public void KeysAndValues_Contains_ShouldForwardToTheDictionary()
    {
        var map = new BiDictionary<int, string, Int32WangNaiveHasher, StringFnV1AHasher>();
        map[1] = "one";
        map[0] = "zero";

        Assert.True(map.Keys.Contains(1));
        Assert.True(map.Keys.Contains(0));
        Assert.False(map.Keys.Contains(2));
        Assert.True(map.Values.Contains("zero"));
        Assert.False(map.Values.Contains("two"));
        Assert.False(map.Values.Contains(null!));

        ICollection<int> keys = map.Keys;
        ICollection<string> values = map.Values;
        Assert.True(keys.Contains(1));
        Assert.True(values.Contains("one"));
    }

    [Fact]
    public void KeyCollection_ShouldBeIterableThroughIEnumerable_InOrder()
    {
        var map = NewIntMap();
        map[1] = 10;
        map[2] = 20;
        map[0] = 0;

        IEnumerable<int> keys = map.Keys;
        var seen = new List<int>();
        foreach (int k in keys)
            seen.Add(k);

        Assert.Equal(new[] { 1, 2, 0 }, seen);
    }

    [Fact]
    public void ValueCollection_ShouldBeIterableThroughIEnumerable_InOrder()
    {
        var map = NewIntMap();
        map[1] = 10;
        map[2] = 20;
        map[0] = 99;

        IEnumerable<int> values = map.Values;
        var seen = new List<int>();
        foreach (int v in values)
            seen.Add(v);

        Assert.Equal(new[] { 10, 20, 99 }, seen);
    }

    [Fact]
    public void InverseKeysAndValues_ShouldBeIterableThroughIReadOnlyDictionary()
    {
        var map = NewIntMap();
        map[1] = 10;
        map[2] = 20;
        IReadOnlyDictionary<int, int> inverse = map.Inverse;

        var keys = new List<int>();
        foreach (int k in inverse.Keys)
            keys.Add(k);
        var values = new List<int>();
        foreach (int v in inverse.Values)
            values.Add(v);

        Assert.Equal(new[] { 10, 20 }, keys);
        Assert.Equal(new[] { 1, 2 }, values);
    }

    // ---------------------------------------------------------------
    //  Boxed (IEnumerable / IEnumerator) paths
    // ---------------------------------------------------------------

    [Fact]
    public void KeyCollection_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var map = NewIntMap();
        for (int i = 4; i >= 1; i--) map[i] = i * 10;

        var (first, second) = DrainNonGenericTwice<int>(map.Keys);

        Assert.Equal(new[] { 4, 3, 2, 1 }, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ValueCollection_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var map = NewIntMap();
        for (int i = 4; i >= 1; i--) map[i] = i * 10;

        var (first, second) = DrainNonGenericTwice<int>(map.Values);

        Assert.Equal(new[] { 40, 30, 20, 10 }, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Inverse_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var map = NewIntMap();
        map[2] = 20;
        map[1] = 10;

        var (first, second) = DrainNonGenericTwice<KeyValuePair<int, int>>(map.Inverse);

        Assert.Equal(new[] { new KeyValuePair<int, int>(20, 2), new KeyValuePair<int, int>(10, 1) }, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Dictionary_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var map = NewIntMap();
        map[2] = 20;
        map[1] = 10;

        var (first, second) = DrainNonGenericTwice<KeyValuePair<int, int>>(map);

        Assert.Equal(new[] { new KeyValuePair<int, int>(2, 20), new KeyValuePair<int, int>(1, 10) }, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Dictionary_ShouldEnumerateThroughGenericIEnumerable_InOrder()
    {
        var map = NewIntMap();
        map[3] = 30;
        map[1] = 10;
        map[2] = 20;

        IEnumerable<KeyValuePair<int, int>> pairs = map;
        IEnumerable<KeyValuePair<int, int>> inverse = map.Inverse;

        Assert.Equal(new[] { 3, 1, 2 }, pairs.Select(p => p.Key).ToArray());
        Assert.Equal(new[] { 30, 10, 20 }, inverse.Select(p => p.Key).ToArray());
    }

    [Fact]
    public void BoxedEnumerators_ShouldFailFast_ForEveryShape()
    {
        var map = Filled(2);
        IEnumerator[] boxed =
        {
            ((IEnumerable)map).GetEnumerator(),
            ((IEnumerable<KeyValuePair<int, int>>)map).GetEnumerator(),
            ((IEnumerable)map.Keys).GetEnumerator(),
            ((IEnumerable<int>)map.Keys).GetEnumerator(),
            ((IEnumerable)map.Values).GetEnumerator(),
            ((IEnumerable<int>)map.Values).GetEnumerator(),
            ((IEnumerable)map.Inverse).GetEnumerator(),
            ((IEnumerable<KeyValuePair<int, int>>)map.Inverse).GetEnumerator(),
        };
        foreach (IEnumerator e in boxed)
            Assert.True(e.MoveNext());

        map.Remove(1);

        foreach (IEnumerator e in boxed)
        {
            Assert.Throws<InvalidOperationException>(() => e.MoveNext());
            Assert.Throws<InvalidOperationException>(() => e.Reset());
            ((IDisposable)e).Dispose();
        }
    }

    private static (List<T> first, List<T> second) DrainNonGenericTwice<T>(IEnumerable view)
    {
        IEnumerator e = view.GetEnumerator();
        var first = new List<T>();
        while (e.MoveNext()) first.Add((T)e.Current!);
        e.Reset();
        var second = new List<T>();
        while (e.MoveNext()) second.Add((T)e.Current!);
        return (first, second);
    }
}
