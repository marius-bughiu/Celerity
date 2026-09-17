using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// The public surface of <see cref="PersistentHashSet{T, THasher}"/>: construction, the read paths, the
/// persistent edits, the set-algebra operations and every documented guard.
///
/// <para>
/// The randomized reconciliation against a <see cref="HashSet{T}"/> oracle — and with it the trie's shape
/// changes as nodes split and collapse — lives in <see cref="PersistentHashSetDifferentialTests"/>; full-hash
/// collisions live in <see cref="PersistentHashSetCollisionTests"/>. What is pinned here is what a caller
/// can read from the documentation: that an edit which changes something returns a <i>new</i> set, that an
/// edit which changes nothing hands back the receiver itself, that either way the receiver is left alone,
/// that the out-of-band <c>default(T)</c> element behaves like any other, and that the exceptions are the
/// ones the XML docs promise.
/// </para>
/// </summary>
public class PersistentHashSetTests
{
    // Enough elements that the trie is three levels deep in places, so the reads and writes under test are
    // not all answered by the root's own inline elements.
    private const int LargeCount = 5_000;

    private static PersistentHashSet<int, Int32IdentityHasher> EmptySet =>
        PersistentHashSet<int, Int32IdentityHasher>.Empty;

    // ── Empty ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Empty_HasNoElements()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet;

        Assert.Equal(0, set.Count);
        Assert.True(set.IsEmpty);
        Assert.False(set.Contains(1));
        Assert.False(set.Contains(0));
        Assert.Empty(set);
    }

    [Fact]
    public void Empty_IsTheSameInstanceEveryTime()
    {
        Assert.Same(EmptySet, PersistentHashSet<int, Int32IdentityHasher>.Empty);
    }

    // ── Add ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_ShouldReturnANewSet_AndLeaveTheReceiverAlone()
    {
        PersistentHashSet<int, Int32IdentityHasher> before = EmptySet.Add(1);
        PersistentHashSet<int, Int32IdentityHasher> after = before.Add(2);

        Assert.Equal(1, before.Count);
        Assert.False(before.Contains(2));
        Assert.Equal(2, after.Count);
        Assert.True(after.Contains(1));
        Assert.True(after.Contains(2));
    }

    [Fact]
    public void Add_ShouldReturnTheReceiver_WhenTheElementIsAlreadyPresent()
    {
        // Set semantics, and the same answer ImmutableHashSet<T>.Add gives: a duplicate is a no-op, not an
        // error, and a no-op allocates nothing.
        PersistentHashSet<int, Int32IdentityHasher> set = BuildRange(200);

        Assert.Same(set, set.Add(7));
        Assert.Same(set, set.Add(199));
    }

    [Fact]
    public void Add_ShouldWriteThroughEveryTrieDepth()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = BuildRange(LargeCount);

        PersistentHashSet<int, Int32IdentityHasher> grown = set.Add(LargeCount + 17);

        Assert.True(grown.Contains(LargeCount + 17));
        Assert.False(set.Contains(LargeCount + 17));
        Assert.Equal(set.Count + 1, grown.Count);
    }

    // ── Remove ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_ShouldReturnTheReceiver_WhenTheElementIsAbsent()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(1);

        Assert.Same(set, set.Remove(2));
        Assert.Same(EmptySet, EmptySet.Remove(2));
    }

    [Fact]
    public void Remove_ShouldReturnTheReceiver_WhenTheSlotHoldsADifferentElement()
    {
        // Identity hashing: 1 and 33 share the root slot, so removing 33 from a set holding only 1 reaches an
        // occupied data slot whose resident is not the one asked for.
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(1);

        Assert.Same(set, set.Remove(33));
    }

    [Fact]
    public void Remove_ShouldReturnEmpty_WhenTheLastElementGoes()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(1);

        Assert.Same(EmptySet, set.Remove(1));
    }

    [Fact]
    public void Remove_ShouldLeaveTheReceiverAlone()
    {
        PersistentHashSet<int, Int32IdentityHasher> before = EmptySet.Add(1).Add(2);
        PersistentHashSet<int, Int32IdentityHasher> after = before.Remove(1);

        Assert.Equal(2, before.Count);
        Assert.True(before.Contains(1));
        Assert.Equal(1, after.Count);
        Assert.False(after.Contains(1));
    }

    [Fact]
    public void Remove_ShouldCollapseTheTrie_SoADrainedSetStillFindsItsSurvivors()
    {
        // The inlining rule in Remove is what this pins: after draining back to two elements, a lookup for a
        // survivor must still land, which it cannot if an element were stranded under a node kept alive only
        // to reach it.
        PersistentHashSet<int, Int32IdentityHasher> set = BuildRange(2_000);

        for (int item = 3; item < 2_000; item++)
            set = set.Remove(item);

        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(2));
        Assert.Equal(set.Count, PersistentHashMapShape.AssertCanonical(set));
    }

    [Fact]
    public void AddThenDrain_ShouldEndAtEmpty()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = BuildRange(LargeCount);

        for (int item = 1; item < LargeCount; item++)
        {
            set = set.Remove(item);
            Assert.Equal(LargeCount - 1 - item, set.Count);
        }

        Assert.Same(EmptySet, set);
    }

    // ── Deep tries ───────────────────────────────────────────────────────────────

    [Fact]
    public void EveryElementIsFound_InASetLargeEnoughToBeSeveralLevelsDeep()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = BuildRange(LargeCount);

        Assert.Equal(LargeCount - 1, set.Count);

        for (int item = 1; item < LargeCount; item++)
            Assert.True(set.Contains(item));

        Assert.False(set.Contains(LargeCount + 1));
        Assert.Equal(set.Count, PersistentHashMapShape.AssertCanonical(set));
    }

    [Fact]
    public void ElementsSharingEveryBitButTheTop_ShouldStillBeTold_Apart()
    {
        // Identity hashing puts these two at the same slot on all six lower levels; only the shift-30 level,
        // which reads the top two bits, separates them. That is the deepest merge a non-colliding pair can
        // produce.
        const int Low = 1;
        const int High = 1 | (1 << 30);

        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(Low).Add(High);

        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(Low));
        Assert.True(set.Contains(High));

        PersistentHashSet<int, Int32IdentityHasher> withoutHigh = set.Remove(High);
        Assert.Equal(1, withoutHigh.Count);
        Assert.True(withoutHigh.Contains(Low));
        Assert.Equal(1, PersistentHashMapShape.AssertCanonical(withoutHigh));
    }

    [Fact]
    public void NegativeElements_ShouldRoundTrip()
    {
        // Identity hashing makes a negative element a hash with the sign bit set; the descent must shift it
        // in unsigned or the top level reads the wrong slot.
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(-1).Add(int.MinValue).Add(int.MaxValue);

        Assert.True(set.Contains(-1));
        Assert.True(set.Contains(int.MinValue));
        Assert.True(set.Contains(int.MaxValue));
        Assert.False(set.Contains(-2));
    }

    // ── The out-of-band default(T) element ───────────────────────────────────────

    [Fact]
    public void DefaultElement_ShouldBehaveLikeAnyOtherElement()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(0).Add(1);

        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(0));
        Assert.Equal(new[] { 0, 1 }, set.OrderBy(item => item).ToArray());
    }

    [Fact]
    public void DefaultElement_Add_ShouldReturnTheReceiver_WhenAlreadyPresent()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(0);

        Assert.Same(set, set.Add(0));
    }

    [Fact]
    public void DefaultElement_Remove_ShouldLeaveTheTrieAlone()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(0).Add(1);

        PersistentHashSet<int, Int32IdentityHasher> without = set.Remove(0);

        Assert.Equal(1, without.Count);
        Assert.False(without.Contains(0));
        Assert.True(without.Contains(1));
        Assert.True(set.Contains(0));
    }

    [Fact]
    public void DefaultElement_Remove_ShouldBeANoOp_WhenAbsent()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(1);

        Assert.Same(set, set.Remove(0));
    }

    [Fact]
    public void DefaultElement_Remove_ShouldReturnEmpty_WhenItIsTheOnlyElement()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(0);

        Assert.Same(EmptySet, set.Remove(0));
    }

    [Fact]
    public void NullElement_ShouldWorkWithAHasherThatThrowsOnOne()
    {
        // StringFnV1AHasher throws ArgumentNullException on a null key — a hash of its characters has
        // nothing to read — and the assertion below proves it, so this test cannot quietly stop covering the
        // thing it is named for. The out-of-band flag is what keeps the null element from ever reaching it.
        Assert.Throws<ArgumentNullException>(() => new StringFnV1AHasher().Hash(null!));

        PersistentHashSet<string, StringFnV1AHasher> set =
            PersistentHashSet<string, StringFnV1AHasher>.Empty.Add(null!).Add("a");

        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(null!));
        Assert.True(set.Contains("a"));
        Assert.Equal(1, set.Remove(null!).Count);
        Assert.False(set.Contains("missing"));
    }

    // ── TryGetValue ──────────────────────────────────────────────────────────────

    [Fact]
    public void TryGetValue_ShouldReturnTheStoredInstance_NotTheArgument()
    {
        // Two equal strings that are distinct objects: the point of TryGetValue is to recover the one the set
        // holds, which is what makes it usable for canonicalizing references.
        string stored = new('x', 4);
        string probe = new('x', 4);
        Assert.NotSame(stored, probe);

        PersistentHashSet<string, StringFnV1AHasher> set =
            PersistentHashSet<string, StringFnV1AHasher>.Empty.Add(stored).Add("other");

        Assert.True(set.TryGetValue(probe, out string actual));
        Assert.Same(stored, actual);
    }

    [Fact]
    public void TryGetValue_ShouldHandBackTheArgument_OnAMiss()
    {
        PersistentHashSet<string, StringFnV1AHasher> set =
            PersistentHashSet<string, StringFnV1AHasher>.Empty.Add("a").Add("b");

        string probe = new('z', 3);

        Assert.False(set.TryGetValue(probe, out string actual));
        Assert.Same(probe, actual);
    }

    [Fact]
    public void TryGetValue_ShouldAnswerForTheDefaultElement()
    {
        PersistentHashSet<string, StringFnV1AHasher> without = PersistentHashSet<string, StringFnV1AHasher>.Empty.Add("a");
        PersistentHashSet<string, StringFnV1AHasher> with = without.Add(null!);

        Assert.False(without.TryGetValue(null!, out string? missing));
        Assert.Null(missing);
        Assert.True(with.TryGetValue(null!, out string? found));
        Assert.Null(found);
    }

    // ── Union ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Union_ShouldAddEveryNewElement_AndLeaveTheReceiverAlone()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(1).Add(2);

        PersistentHashSet<int, Int32IdentityHasher> union = set.Union([2, 3, 3, 0]);

        Assert.Equal(new[] { 0, 1, 2, 3 }, union.OrderBy(item => item).ToArray());
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void Union_ShouldReturnTheReceiver_WhenNothingIsNew()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = BuildRange(64);

        Assert.Same(set, set.Union([1, 2, 3]));
        Assert.Same(set, set.Union([]));
        Assert.Same(set, set.Union(set));
    }

    [Fact]
    public void Union_ShouldThrow_WhenOtherIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => EmptySet.Union(null!));

        Assert.Equal("other", ex.ParamName);
    }

    // ── Except ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Except_ShouldDropEveryPresentElement_AndIgnoreTheRest()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = BuildRange(64);

        PersistentHashSet<int, Int32IdentityHasher> trimmed = set.Except([3, 9, 1_000]);

        Assert.Equal(set.Count - 2, trimmed.Count);
        Assert.False(trimmed.Contains(3));
        Assert.False(trimmed.Contains(9));
        Assert.True(set.Contains(3));
    }

    [Fact]
    public void Except_ShouldReturnTheReceiver_WhenNoElementIsPresent()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = BuildRange(16);

        Assert.Same(set, set.Except([9_001, 9_002]));
        Assert.Same(set, set.Except([]));
    }

    [Fact]
    public void Except_WithItself_ShouldReturnEmpty()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = BuildRange(16);

        Assert.Same(EmptySet, set.Except(set));
    }

    [Fact]
    public void SelfDifference_OnADistinctEmptySet_ShouldReturnTheReceiver()
    {
        // Regression: the self-aliasing fast paths returned Empty unconditionally. The sequence constructor
        // builds an empty set that is not the Empty singleton, and its difference with itself removes nothing —
        // so the no-op contract hands back the receiver, not a different empty instance.
        var empty = new PersistentHashSet<int, Int32IdentityHasher>([]);
        Assert.NotSame(EmptySet, empty);

        Assert.Same(empty, empty.Except(empty));
        Assert.Same(empty, empty.SymmetricExcept(empty));
    }

    [Fact]
    public void Except_ShouldThrow_WhenOtherIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => EmptySet.Except(null!));

        Assert.Equal("other", ex.ParamName);
    }

    // ── Intersect ────────────────────────────────────────────────────────────────

    [Fact]
    public void Intersect_ShouldKeepOnlyTheCommonElements()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(0).Add(1).Add(2).Add(3);

        PersistentHashSet<int, Int32IdentityHasher> common = set.Intersect([0, 2, 2, 9]);

        Assert.Equal(new[] { 0, 2 }, common.OrderBy(item => item).ToArray());
        Assert.Equal(4, set.Count);
    }

    [Fact]
    public void Intersect_ShouldReturnTheReceiver_WhenEveryElementIsInOther()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(1).Add(2);

        Assert.Same(set, set.Intersect([1, 2, 3, 4]));
        Assert.Same(set, set.Intersect(set));
    }

    [Fact]
    public void Intersect_ShouldReturnEmpty_WhenNothingIsShared()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(1).Add(2);

        Assert.Same(EmptySet, set.Intersect([7, 8]));
        Assert.Same(EmptySet, set.Intersect([]));
    }

    [Fact]
    public void Intersect_OnAnEmptySet_ShouldReturnTheReceiver()
    {
        Assert.Same(EmptySet, EmptySet.Intersect([1, 2]));
    }

    [Fact]
    public void Intersect_ShouldKeepTheStoredInstances()
    {
        string stored = new('q', 2);
        string probe = new('q', 2);

        PersistentHashSet<string, StringFnV1AHasher> set =
            PersistentHashSet<string, StringFnV1AHasher>.Empty.Add(stored).Add("r");

        PersistentHashSet<string, StringFnV1AHasher> common = set.Intersect([probe]);

        Assert.Equal(1, common.Count);
        Assert.Same(stored, common.Single());
    }

    [Fact]
    public void Intersect_ShouldThrow_WhenOtherIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => EmptySet.Intersect(null!));

        Assert.Equal("other", ex.ParamName);
    }

    // ── SymmetricExcept ──────────────────────────────────────────────────────────

    [Fact]
    public void SymmetricExcept_ShouldToggleEachDistinctElementOnce()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(0).Add(1).Add(2);

        // 2 appears twice in `other`; a toggle per occurrence would remove it and add it back.
        PersistentHashSet<int, Int32IdentityHasher> toggled = set.SymmetricExcept([2, 2, 3, 0]);

        Assert.Equal(new[] { 1, 3 }, toggled.OrderBy(item => item).ToArray());
        Assert.Equal(3, set.Count);
    }

    [Fact]
    public void SymmetricExcept_ShouldReturnTheReceiver_WhenOtherIsEmpty()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = BuildRange(10);

        Assert.Same(set, set.SymmetricExcept([]));
    }

    [Fact]
    public void SymmetricExcept_WithItself_ShouldReturnEmpty()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = BuildRange(10);

        Assert.Same(EmptySet, set.SymmetricExcept(set));
    }

    [Fact]
    public void SymmetricExcept_ShouldThrow_WhenOtherIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => EmptySet.SymmetricExcept(null!));

        Assert.Equal("other", ex.ParamName);
    }

    // ── IReadOnlySet<T> queries ──────────────────────────────────────────────────
    // The full true/false table against a HashSet oracle lives in ReadOnlySetInterfaceTests and the
    // differential suite; these pin the edges each query special-cases.

    [Fact]
    public void Queries_ShouldHandleTheEmptySet()
    {
        Assert.True(EmptySet.IsSubsetOf([]));
        Assert.True(EmptySet.IsSubsetOf([1]));
        Assert.False(EmptySet.IsProperSubsetOf([]));
        Assert.True(EmptySet.IsProperSubsetOf([1]));
        Assert.True(EmptySet.IsSupersetOf([]));
        Assert.False(EmptySet.IsProperSupersetOf([]));
        Assert.False(EmptySet.Overlaps([1]));
        Assert.True(EmptySet.SetEquals([]));
    }

    [Fact]
    public void Queries_ShouldAnswerForTheSetItself()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = BuildRange(40).Add(0);

        Assert.True(set.SetEquals(set));
        Assert.True(set.IsSubsetOf(set));
        Assert.False(set.IsProperSubsetOf(set));
        Assert.True(set.IsSupersetOf(set));
        Assert.False(set.IsProperSupersetOf(set));
        Assert.True(set.Overlaps(set));
    }

    [Fact]
    public void Queries_ShouldNotMaterializeTheSetItself()
    {
        // Regression: only SetEquals short-circuited on `other` being the set itself; the subset and proper-superset
        // shapes copied all 10,000 elements into a HashSet<T> (~200 KB each) to answer a question with a fixed answer.
        PersistentHashSet<int, Int32IdentityHasher> set = BuildRange(10_000);
        bool[] answers = AskEveryQueryOfItself(set);   // warm up

        long before = GC.GetAllocatedBytesForCurrentThread();
        answers = AskEveryQueryOfItself(set);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal([true, true, false, true, false, true], answers);
        Assert.InRange(allocated, 0, 1_024);

        static bool[] AskEveryQueryOfItself(PersistentHashSet<int, Int32IdentityHasher> s) =>
            [s.SetEquals(s), s.IsSubsetOf(s), s.IsProperSubsetOf(s), s.IsSupersetOf(s), s.IsProperSupersetOf(s), s.Overlaps(s)];
    }

    [Fact]
    public void SetEquals_ShouldIgnoreDuplicatesInOther_AndSeeTheDefaultElement()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(0).Add(1).Add(2);

        Assert.True(set.SetEquals([2, 1, 0, 1, 2]));
        Assert.False(set.SetEquals([1, 2, 3]));
        Assert.False(set.SetEquals([1, 2]));
    }

    [Fact]
    public void ProperQueries_ShouldRejectAnEqualSet_AndAcceptAStrictlyLargerOrSmallerOne()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(1).Add(2);

        Assert.True(set.IsProperSubsetOf([1, 2, 3]));
        Assert.False(set.IsProperSubsetOf([1, 3, 4]));
        Assert.True(set.IsProperSupersetOf([1]));
        Assert.False(set.IsProperSupersetOf([1, 3]));
        Assert.False(set.IsSubsetOf([1]));
        Assert.False(set.IsSubsetOf([1, 3]));
        Assert.False(set.IsSupersetOf([1, 3]));
        Assert.False(set.Overlaps([5, 6]));
    }

    [Fact]
    public void Queries_ShouldThrow_WhenOtherIsNull()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(1);

        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => set.SetEquals(null!)).ParamName);
        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => set.IsSubsetOf(null!)).ParamName);
        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => set.IsProperSubsetOf(null!)).ParamName);
        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => set.IsSupersetOf(null!)).ParamName);
        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => set.IsProperSupersetOf(null!)).ParamName);
        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => set.Overlaps(null!)).ParamName);

        // The empty set's short-circuits must not skip the guard.
        Assert.Throws<ArgumentNullException>(() => EmptySet.IsSubsetOf(null!));
        Assert.Throws<ArgumentNullException>(() => EmptySet.Overlaps(null!));
    }

    // ── Construction from a sequence ─────────────────────────────────────────────

    [Fact]
    public void SequenceConstructor_ShouldCopyEveryDistinctElement()
    {
        var set = new PersistentHashSet<int, Int32IdentityHasher>([0, 1, 2, 1, 0]);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(2));
    }

    [Fact]
    public void SequenceConstructor_ShouldThrow_WhenTheSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new PersistentHashSet<int, Int32IdentityHasher>(null!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void SequenceConstructor_ShouldAcceptAnEmptySource()
    {
        var set = new PersistentHashSet<int, Int32IdentityHasher>([]);

        Assert.True(set.IsEmpty);
    }

    // Elements 1..count-1; 0 is deliberately left out so callers that want the out-of-band element exercised
    // have to add it themselves.
    private static PersistentHashSet<int, Int32IdentityHasher> BuildRange(int count)
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet;
        for (int item = 1; item < count; item++)
            set = set.Add(item);

        return set;
    }
}
