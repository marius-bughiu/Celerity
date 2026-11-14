using System.Collections;
using System.Numerics;
using System.Reflection;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class FrozenCeleritySetTests
{
    private static FrozenCeleritySet Build(params string[] items)
        => new FrozenCeleritySet(items);

    // ── Construction & basic membership ───────────────────────────────────────

    [Fact]
    public void Build_FromItems_AllRoundTrip()
    {
        var set = Build("alice", "bob", "carol");

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains("alice"));
        Assert.True(set.Contains("bob"));
        Assert.True(set.Contains("carol"));
    }

    [Fact]
    public void Contains_AbsentElement_ReturnsFalse()
    {
        var set = Build("alice", "bob");
        Assert.False(set.Contains("carol"));
    }

    // ── Empty string is a regular element ─────────────────────────────────────

    [Fact]
    public void EmptyStringElement_IsRegularElement()
    {
        var set = Build("", "a");

        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(""));
        Assert.True(set.Contains("a"));
    }

    // ── Null element out-of-band ──────────────────────────────────────────────

    [Fact]
    public void NullElement_RoundTripsOutOfBand()
    {
        var set = new FrozenCeleritySet(new[] { null!, "present" });

        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(null!));
        Assert.True(set.Contains("present"));
    }

    [Fact]
    public void NullElement_Absent_Misses()
    {
        var set = Build("a");

        Assert.False(set.Contains(null!));
    }

    // ── Validation & set semantics ────────────────────────────────────────────

    [Fact]
    public void Build_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FrozenCeleritySet(null!));
    }

    [Fact]
    public void Build_DuplicateElements_AreSilentlyDeduped()
    {
        // Unlike the frozen dictionary (which throws on duplicate keys), a set
        // de-duplicates — its defining property.
        var set = Build("a", "b", "a", "c", "b", "a");

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains("a"));
        Assert.True(set.Contains("b"));
        Assert.True(set.Contains("c"));
    }

    [Fact]
    public void Build_DuplicateNullElements_AreSilentlyDeduped()
    {
        var set = new FrozenCeleritySet(new[] { "a", null!, "b", null!, "c", null! });

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(null!));
        Assert.True(set.Contains("a"));
        Assert.True(set.Contains("b"));
        Assert.True(set.Contains("c"));
    }

    [Fact]
    public void Build_Empty_HasZeroCountAndMissesEverything()
    {
        var set = new FrozenCeleritySet(Array.Empty<string>());

        Assert.Empty(set);
        Assert.False(set.Contains("anything"));
        Assert.False(set.Contains(null!));
    }

    // ── Perfect-hash fast path vs fallback ────────────────────────────────────

    [Fact]
    public void NormalElements_ArePerfectlyHashed()
    {
        var set = new FrozenCeleritySet<StringMurmur3Hasher>(
            Enumerable.Range(0, 12).Select(i => "key-" + i));

        Assert.True(set.IsPerfectlyHashed);
        for (int i = 0; i < 12; i++)
            Assert.True(set.Contains("key-" + i));
    }

    [Fact]
    public void BaseHashCollidingElements_FallBackButStayCorrect()
    {
        // StringFnV1AHasher folds only the low byte, so 'A' (U+0041) and 'Ł' (U+0141)
        // produce the SAME raw hash code. No mixing seed can separate two elements
        // that share a base hash, so the build cannot be perfect — but the equality
        // check on the probe path must still keep them distinct and correct.
        var fnv = new StringFnV1AHasher();
        Assert.Equal(fnv.Hash("A"), fnv.Hash("Ł"));

        var set = new FrozenCeleritySet<StringFnV1AHasher>(new[] { "A", "Ł" });

        Assert.False(set.IsPerfectlyHashed);
        Assert.Equal(2, set.Count);
        Assert.True(set.Contains("A"));
        Assert.True(set.Contains("Ł"));
        // A third element sharing the same low byte but absent must still miss.
        Assert.False(set.Contains("ŁA"));
    }

    [Fact]
    public void AbsentElement_HashingIntoOccupiedSlot_Misses()
    {
        // Membership tests of absent elements must return false even when they mix to
        // a slot that holds a different element (the equality check rejects them) —
        // the subtle correctness point for both the perfect and fallback paths.
        var set = new FrozenCeleritySet<StringMurmur3Hasher>(
            Enumerable.Range(0, 50).Select(i => "present-" + i));

        for (int i = 0; i < 5000; i++)
            Assert.False(set.Contains("absent-" + i));
    }

    // Every element hashes to the same raw code, so no (size, seed) the perfect-hash
    // search tries can ever be collision-free. The search runs every seed at the one
    // candidate size the birthday bound leaves plausible for 200 elements (2,048 slots),
    // fails at all of them, and falls back to linear probing.
    private struct ConstantStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 7;
    }

    [Fact]
    public void AllElementsBaseHashColliding_ExhaustSearchThenFallBackCorrectly()
    {
        // A degenerate constant hasher forces the perfect-hash search to fail at every
        // candidate size, so the size-doubling loop runs to completion and the build falls
        // back. The fallback table must still terminate (a missing element's
        // probe walks to a guaranteed-empty slot) and answer membership exactly.
        var items = Enumerable.Range(0, 200).Select(i => "item-" + i).ToArray();

        var set = new FrozenCeleritySet<ConstantStringHasher>(items);

        Assert.False(set.IsPerfectlyHashed);
        Assert.Equal(200, set.Count);
        foreach (var item in items)
            Assert.True(set.Contains(item));

        // Absent elements that mix into the densely-clustered table must still miss
        // (the probe terminates at the vacant slot the fallback sizing guarantees).
        for (int i = 200; i < 400; i++)
            Assert.False(set.Contains("item-" + i));
    }

    // ── Larger random sweep ───────────────────────────────────────────────────

    [Fact]
    public void ManyElements_AllRoundTripAndAbsentMiss()
    {
        var items = Enumerable.Range(0, 1000)
            .Select(i => $"celerity/key/{i}/{i * 7}")
            .ToArray();

        var set = new FrozenCeleritySet(items);

        Assert.Equal(1000, set.Count);
        foreach (var item in items)
            Assert.True(set.Contains(item));

        for (int i = 1000; i < 2000; i++)
            Assert.False(set.Contains($"celerity/key/{i}/{i * 7}"));
    }

    // ── Build layout: the bounded search and the half-full fallback ───────────

    private static string[] IdentifierItems(int count)
        => Enumerable.Range(0, count).Select(i => $"celerity/key/{i}/{i * 7}").ToArray();

    // The length of the slot array the build settled on.
    private static int TableLength<THasher>(FrozenCeleritySet<THasher> set)
        where THasher : struct, IHashProvider<string>
        => ((string?[])typeof(FrozenCeleritySet<THasher>)
            .GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(set)!).Length;

    [Theory]
    [InlineData(300)]  // the search runs, at 4,096 slots, and fails
    [InlineData(2000)] // the search is skipped
    public void Build_DoesNotAllocateATablePerSearchAttempt(int count)
    {
        // The perfect-hash search used to allocate a fresh table-sized array for each of its up
        // to 2,052 (size, seed) attempts: about 32 MB to build 300 elements and 126 MB for 2,000.
        // It now reuses one stamp buffer, and skips the sizes the birthday bound rules out (all
        // of them at 2,000 elements), so a build allocates about its own table.
        var items = IdentifierItems(count);
        _ = new FrozenCeleritySet(items); // warm every path outside the windows

        long allocated = AllocationProbe.Quietest(() => _ = new FrozenCeleritySet(items));

        Assert.True(allocated < 1_000_000, $"Building {count} elements allocated {allocated:N0} bytes.");
    }

    [Theory]
    [InlineData(334)]  // from here up the build never runs the search
    [InlineData(1000)]
    [InlineData(1024)]
    [InlineData(5000)]
    public void LargeSet_FallsBackToATableAtMostHalfFull(int count)
    {
        // Past 333 elements no seed is plausibly collision-free at any candidate size, so the
        // build takes the linear-probing table. It must keep at least half its slots empty so
        // probe runs stay short: the old sizing put 1,000 elements in 1,024 slots.
        var items = IdentifierItems(count);
        var set = new FrozenCeleritySet(items);

        Assert.False(set.IsPerfectlyHashed);
        int length = TableLength(set);
        Assert.True(BitOperations.IsPow2(length));
        Assert.InRange(length, 2 * count, 4 * count - 1);
        foreach (var item in items)
            Assert.True(set.Contains(item));
        Assert.False(set.Contains("celerity/key/absent"));

        // Enumeration walks the occupied slots only; every element must still come back once.
        Assert.Equal(items.Order(StringComparer.Ordinal), set.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void MidSizeSet_SearchGoesOnPastHopelessSizes_ToAPerfectPlacement()
    {
        // 150 elements cannot plausibly land collision-free in 256 or 512 slots, so the search
        // skips those sizes. It must go on to the larger candidates rather than give up at the
        // first skipped one: for these elements under Murmur3 the first seed that works is at
        // 2,048 slots.
        var items = Enumerable.Range(0, 150).Select(i => "item-" + i).ToArray();

        var set = new FrozenCeleritySet<StringMurmur3Hasher>(items);

        Assert.True(set.IsPerfectlyHashed);
        Assert.Equal(2048, TableLength(set));
        foreach (var item in items)
            Assert.True(set.Contains(item));
    }

    // ── Enumeration ───────────────────────────────────────────────────────────

    [Fact]
    public void Enumeration_YieldsAllElementsIncludingNull()
    {
        var set = new FrozenCeleritySet(new[] { "a", "b", null! });

        var collected = new List<string>();
        bool sawNull = false;
        foreach (var item in set)
        {
            if (item is null) sawNull = true;
            else collected.Add(item);
        }

        Assert.True(sawNull);
        collected.Sort();
        Assert.Equal(new[] { "a", "b" }, collected);
    }

    [Fact]
    public void Enumeration_RoundTripsAllElements()
    {
        var set = Build("a", "b", "c");

        var list = set.ToList();
        list.Sort();
        Assert.Equal(new[] { "a", "b", "c" }, list);
    }

    // ── IReadOnlySet surface ──────────────────────────────────────────────────

    [Fact]
    public void UsableAsIReadOnlySet()
    {
        IReadOnlySet<string> ro = Build("a", "b");

        Assert.Equal(2, ro.Count);
        Assert.True(ro.Contains("a"));
        Assert.False(ro.Contains("z"));
        Assert.Equal(2, ro.Count());   // LINQ over the boxed enumerable
    }

    [Fact]
    public void SetEquals_Behaviour()
    {
        var set = Build("a", "b", "c");

        Assert.True(set.SetEquals(new[] { "c", "b", "a" }));
        Assert.True(set.SetEquals(new[] { "a", "b", "c", "a" })); // duplicates ignored
        Assert.False(set.SetEquals(new[] { "a", "b" }));
        Assert.False(set.SetEquals(new[] { "a", "b", "c", "d" }));
    }

    [Fact]
    public void SetEquals_WithNullElement()
    {
        var set = new FrozenCeleritySet(new[] { "a", null!, "b" });

        Assert.True(set.SetEquals(new[] { null!, "b", "a" }));
        Assert.False(set.SetEquals(new[] { "a", "b" }));
    }

    [Fact]
    public void IsSubsetOf_Behaviour()
    {
        var set = Build("a", "b");

        Assert.True(set.IsSubsetOf(new[] { "a", "b", "c" }));
        Assert.True(set.IsSubsetOf(new[] { "a", "b" }));          // equal sets are subsets
        Assert.True(set.IsSubsetOf(new[] { "a", "b", "b", "c" })); // duplicates in other
        Assert.False(set.IsSubsetOf(new[] { "a", "c" }));
        Assert.False(set.IsSubsetOf(Array.Empty<string>()));
    }

    [Fact]
    public void IsProperSubsetOf_Behaviour()
    {
        var set = Build("a", "b");

        Assert.True(set.IsProperSubsetOf(new[] { "a", "b", "c" }));
        Assert.False(set.IsProperSubsetOf(new[] { "a", "b" }));   // equal is not proper
        Assert.False(set.IsProperSubsetOf(new[] { "a", "c" }));
    }

    [Fact]
    public void IsSupersetOf_Behaviour()
    {
        var set = Build("a", "b", "c");

        Assert.True(set.IsSupersetOf(new[] { "a", "b" }));
        Assert.True(set.IsSupersetOf(new[] { "a", "b", "c" }));
        Assert.True(set.IsSupersetOf(Array.Empty<string>()));
        Assert.False(set.IsSupersetOf(new[] { "a", "z" }));
    }

    [Fact]
    public void IsProperSupersetOf_Behaviour()
    {
        var set = Build("a", "b", "c");

        Assert.True(set.IsProperSupersetOf(new[] { "a", "b" }));
        Assert.True(set.IsProperSupersetOf(new[] { "a", "b", "b" })); // duplicates in other
        Assert.False(set.IsProperSupersetOf(new[] { "a", "b", "c" })); // equal is not proper
        Assert.False(set.IsProperSupersetOf(new[] { "a", "z" }));
    }

    [Fact]
    public void Overlaps_Behaviour()
    {
        var set = Build("a", "b", "c");

        Assert.True(set.Overlaps(new[] { "z", "b" }));
        Assert.False(set.Overlaps(new[] { "x", "y", "z" }));
        Assert.False(set.Overlaps(Array.Empty<string>()));
    }

    [Fact]
    public void SetAlgebra_NullOther_Throws()
    {
        var set = Build("a");

        Assert.Throws<ArgumentNullException>(() => set.SetEquals(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsSubsetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsProperSubsetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsSupersetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsProperSupersetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.Overlaps(null!));
    }

    // ── Custom hasher overload & default convenience type ─────────────────────

    [Fact]
    public void CustomHasherOverload_Works()
    {
        var set = new FrozenCeleritySet<StringXxHash3Hasher>(new[] { "alpha", "beta" });

        Assert.True(set.Contains("alpha"));
        Assert.True(set.Contains("beta"));
        Assert.False(set.Contains("gamma"));
    }

    [Fact]
    public void DefaultConvenienceType_UsesFnv1aAndRoundTrips()
    {
        FrozenCeleritySet set = Build("one", "two", "three");

        Assert.True(set.Contains("one"));
        Assert.True(set.Contains("two"));
        Assert.True(set.Contains("three"));
        Assert.False(set.Contains("four"));
    }

    // ── Non-generic enumeration surface ───────────────────────────────────────

    [Fact]
    public void Enumerator_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var set = Build("a", "b", "c");

        IEnumerator e = ((IEnumerable)set).GetEnumerator();
        var first = new List<string>();
        while (e.MoveNext()) first.Add((string)e.Current!);
        e.Reset();
        var second = new List<string>();
        while (e.MoveNext()) second.Add((string)e.Current!);

        first.Sort();
        second.Sort();
        Assert.Equal(new[] { "a", "b", "c" }, first);
        Assert.Equal(first, second);
    }
}
