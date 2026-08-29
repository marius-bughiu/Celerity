using System.Collections;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// The cross-collection contract for <c>Clear()</c>: <b>a <c>Clear()</c> that removes nothing does not bump
/// the version</b>, so it leaves active enumerators valid, while a <c>Clear()</c> that actually empties a
/// populated collection still invalidates them.
///
/// <para>
/// This is one of the family-wide invariants the library holds itself to — the same reasoning behind
/// <see cref="FenwickTree{T}"/> documenting that a zero delta is a no-op, <c>BTreeDictionary</c> documenting
/// that a rejected duplicate <c>TryAdd</c> is a true no-op, and <c>LruCache</c> skipping the bump for a hit
/// on the already-MRU entry. A caller who clears defensively before reusing a collection should not have
/// their in-flight enumeration torn down by a call that changed nothing observable.
/// </para>
///
/// <para>
/// It was previously pinned only per-collection and only for a handful of types (see
/// <see cref="EnumeratorInvalidationAndClearCoverageTests"/>), which let <see cref="Deque{T}"/> ship as the
/// single count-based outlier: its version bump sat <i>outside</i> the guard that skipped the array clearing,
/// so clearing an already-empty deque invalidated every live enumerator. This class asserts the rule once per
/// collection so a future type — or a future edit to an existing one — cannot drift back out of the family
/// without failing a test.
/// </para>
///
/// <para>
/// <b>Deliberate exceptions.</b> <see cref="BitSet"/>, <see cref="FenwickTree{T}"/> and
/// <see cref="SegmentTree{T, TMonoid}"/> are fixed-length, so "already empty" means "every word / cell already
/// holds the neutral value" — establishing that costs a full scan, which is the same work as the unconditional
/// clear it would be trying to skip. All three therefore bump the version every time, they agree with each
/// other, and the three tests at the bottom pin that judgement so it reads as a decision rather than as the
/// same oversight. The probabilistic sketches (<c>BloomFilter</c>,
/// <c>CountMinSketch</c>, <c>CuckooFilter</c>, <c>HyperLogLog</c>, <c>TopKSketch</c>) are out of scope
/// entirely: they track no version and expose no enumerator, so there is nothing for a redundant
/// <c>Clear()</c> to invalidate.
/// </para>
/// </summary>
public class ClearNoOpVersionTests
{
    /// <summary>
    /// Drives one collection through the three <c>Clear()</c> states that matter. The enumerators are taken
    /// through the non-generic <see cref="IEnumerator"/> so a single helper can serve every element shape in
    /// the family (elements, <c>KeyValuePair</c>s, <c>(element, count)</c> pairs); the boxed copy still carries
    /// the version snapshot the struct enumerator took at construction, which is what is under test.
    /// </summary>
    /// <param name="enumerate">Takes a fresh enumerator over the collection.</param>
    /// <param name="clear">Invokes the collection's <c>Clear()</c>.</param>
    /// <param name="count">Reads the collection's element count.</param>
    /// <param name="populate">Adds at least one element, so the next <c>Clear()</c> has work to do.</param>
    private static void AssertClearBumpsVersionOnlyWhenItRemovesSomething(
        Func<IEnumerator> enumerate,
        Action clear,
        Func<int> count,
        Action populate)
    {
        // 1. Never populated: Clear() must be a true no-op and the live enumerator must still drain.
        Assert.Equal(0, count());
        IEnumerator neverPopulated = enumerate();
        clear();
        Assert.Equal(0, count());
        Assert.False(neverPopulated.MoveNext());

        // 2. Populated: Clear() is a real structural change and must invalidate the live enumerator.
        populate();
        Assert.True(count() > 0);
        IEnumerator doomed = enumerate();
        clear();
        Assert.Equal(0, count());
        Assert.Throws<InvalidOperationException>(() => doomed.MoveNext());

        // 3. Emptied by that Clear(): the redundant defensive clear is a no-op again. This is the state a
        //    caller actually reaches — clear, refill, clear — so it is the one most likely to be hit.
        IEnumerator afterClear = enumerate();
        clear();
        Assert.Equal(0, count());
        Assert.False(afterClear.MoveNext());
    }

    // ---- Hashed dictionaries -----------------------------------------------------------------------

    [Fact]
    public void CelerityDictionaryClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var dict = new CelerityDictionary<int, int, Int32WangHasher>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => dict.GetEnumerator(), dict.Clear, () => dict.Count, () => dict.Add(1, 10));
    }

    [Fact]
    public void IntDictionaryClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var dict = new IntDictionary<int>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => dict.GetEnumerator(), dict.Clear, () => dict.Count, () => dict.Add(1, 10));
    }

    [Fact]
    public void LongDictionaryClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var dict = new LongDictionary<int>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => dict.GetEnumerator(), dict.Clear, () => dict.Count, () => dict.Add(1L, 10));
    }

    [Fact]
    public void HashCachingDictionaryClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var dict = new HashCachingDictionary<int, int, Int32WangHasher>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => dict.GetEnumerator(), dict.Clear, () => dict.Count, () => dict.Add(1, 10));
    }

    [Fact]
    public void RobinHoodDictionaryClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var dict = new RobinHoodDictionary<int, int, Int32WangHasher>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => dict.GetEnumerator(), dict.Clear, () => dict.Count, () => dict.Add(1, 10));
    }

    [Fact]
    public void SwissDictionaryClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var dict = new SwissDictionary<int, int, Int32WangHasher>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => dict.GetEnumerator(), dict.Clear, () => dict.Count, () => dict.Add(1, 10));
    }

    [Fact]
    public void PooledCelerityDictionaryClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        using var dict = new PooledCelerityDictionary<int, int, Int32WangHasher>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => dict.GetEnumerator(), dict.Clear, () => dict.Count, () => dict.Add(1, 10));
    }

    [Fact]
    public void SmallDictionaryClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var dict = new SmallDictionary<int, int>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => dict.GetEnumerator(), dict.Clear, () => dict.Count, () => dict.Add(1, 10));
    }

    [Fact]
    public void BTreeDictionaryClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var dict = new BTreeDictionary<int, int>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => dict.GetEnumerator(), dict.Clear, () => dict.Count, () => dict.Add(1, 10));
    }

    [Fact]
    public void EnumMapClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var map = new EnumMap<EnumSetColor, int>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => map.GetEnumerator(), map.Clear, () => map.Count, () => map.Add(EnumSetColor.Green, 10));
    }

    [Fact]
    public void TrieClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var trie = new Trie<int>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => trie.GetEnumerator(), trie.Clear, () => trie.Count, () => trie.Add("alpha", 10));
    }

    // ---- Sets -------------------------------------------------------------------------------------

    [Fact]
    public void CeleritySetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var set = new CeleritySet<int, Int32WangHasher>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => set.GetEnumerator(), set.Clear, () => set.Count, () => set.Add(1));
    }

    [Fact]
    public void IntSetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var set = new IntSet();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => set.GetEnumerator(), set.Clear, () => set.Count, () => set.Add(1));
    }

    [Fact]
    public void LongSetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var set = new LongSet();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => set.GetEnumerator(), set.Clear, () => set.Count, () => set.Add(1L));
    }

    [Fact]
    public void HashCachingSetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var set = new HashCachingSet<int, Int32WangHasher>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => set.GetEnumerator(), set.Clear, () => set.Count, () => set.Add(1));
    }

    [Fact]
    public void RobinHoodSetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var set = new RobinHoodSet<int, Int32WangHasher>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => set.GetEnumerator(), set.Clear, () => set.Count, () => set.Add(1));
    }

    [Fact]
    public void SwissSetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var set = new SwissSet<int, Int32WangHasher>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => set.GetEnumerator(), set.Clear, () => set.Count, () => set.Add(1));
    }

    [Fact]
    public void PooledCeleritySetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        using var set = new PooledCeleritySet<int, Int32WangHasher>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => set.GetEnumerator(), set.Clear, () => set.Count, () => set.Add(1));
    }

    [Fact]
    public void SmallSetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var set = new SmallSet<int>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => set.GetEnumerator(), set.Clear, () => set.Count, () => set.Add(1));
    }

    [Fact]
    public void BTreeSetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var set = new BTreeSet<int>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => set.GetEnumerator(), set.Clear, () => set.Count, () => set.Add(1));
    }

    [Fact]
    public void RankedSetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var set = new RankedSet<int>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => set.GetEnumerator(), set.Clear, () => set.Count, () => set.Add(1));
    }

    [Fact]
    public void SparseSetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var set = new SparseSet(universe: 16);
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => set.GetEnumerator(), set.Clear, () => set.Count, () => set.Add(1));
    }

    [Fact]
    public void CompressedIntSetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var set = new CompressedIntSet();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => set.GetEnumerator(), set.Clear, () => set.Count, () => set.Add(1));
    }

    [Fact]
    public void EnumSetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var set = new EnumSet<EnumSetColor>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => set.GetEnumerator(), set.Clear, () => set.Count, () => set.Add(EnumSetColor.Cyan));
    }

    [Fact]
    public void DisjointSetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var set = new DisjointSet<int>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => set.GetEnumerator(), set.Clear, () => set.Count, () => set.Add(1));
    }

    [Fact]
    public void StringInternTableClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var table = new StringInternTable();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => table.GetEnumerator(), table.Clear, () => table.Count, () => table.GetOrAdd("alpha"));
    }

    // ---- One-to-many, caches, queues, deques ------------------------------------------------------

    [Fact]
    public void CelerityMultiMapClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var map = new CelerityMultiMap<int, int, Int32WangHasher>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => map.GetEnumerator(), map.Clear, () => map.Count, () => map.Add(1, 10));
    }

    [Fact]
    public void CelerityMultiSetClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var bag = new CelerityMultiSet<int, Int32WangHasher>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => bag.GetEnumerator(), bag.Clear, () => bag.Count, () => bag.Add(1));
    }

    [Fact]
    public void LruCacheClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var cache = new LruCache<int, string, Int32WangHasher>(capacity: 4);
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => cache.GetEnumerator(), cache.Clear, () => cache.Count, () => cache.Add(1, "a"));
    }

    [Fact]
    public void IndexedPriorityQueueClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var queue = new IndexedPriorityQueue<int, int, Int32WangHasher>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => queue.GetEnumerator(), queue.Clear, () => queue.Count, () => queue.Enqueue(1, 10));
    }

    [Fact]
    public void DequeClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        // The regression this class was written around: Deque bumped unconditionally.
        var deque = new Deque<int>();
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => deque.GetEnumerator(), deque.Clear, () => deque.Count, () => deque.PushBack(1));
    }

    [Fact]
    public void SpatialGridClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var grid = new SpatialGrid<int>(0, 0, 10, 10, 1);
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => grid.GetEnumerator(), grid.Clear, () => grid.Count, () => grid.Add(1, 1, 1));
    }

    [Fact]
    public void TimerWheelClear_ShouldNotBumpTheVersion_WhenAlreadyEmpty()
    {
        var wheel = new TimerWheel<int>(4, 2);
        AssertClearBumpsVersionOnlyWhenItRemovesSomething(
            () => wheel.GetEnumerator(), wheel.Clear, () => wheel.Count, () => wheel.Schedule(3, 1));
    }

    // ---- The two documented exceptions ------------------------------------------------------------

    [Fact]
    public void BitSetClear_ShouldBumpTheVersionUnconditionally_BecauseEmptinessCostsAScan()
    {
        // Fixed-length: "already empty" is "every word is zero", which costs the same scan as the clear
        // itself. Pinned so the unconditional bump reads as a decision, not as the Deque oversight repeated.
        var bits = new BitSet(128);

        IEnumerator overAllZeroes = bits.GetEnumerator();
        bits.Clear();
        Assert.Throws<InvalidOperationException>(() => overAllZeroes.MoveNext());

        bits.Set(7, true);
        IEnumerator overOneSetBit = bits.GetEnumerator();
        bits.Clear();
        Assert.Throws<InvalidOperationException>(() => overOneSetBit.MoveNext());
        Assert.Equal(0, bits.Count);
    }

    [Fact]
    public void FenwickTreeClear_ShouldBumpTheVersionUnconditionally_BecauseEmptinessCostsAScan()
    {
        // Same reasoning as BitSet, and the two agree with each other. FenwickTree's own no-op exception is a
        // zero-delta Add, which is covered in EnumeratorInvalidationAndClearCoverageTests.
        var tree = new FenwickTree<int>(8);

        IEnumerator overAllZeroes = tree.GetEnumerator();
        tree.Clear();
        Assert.Throws<InvalidOperationException>(() => overAllZeroes.MoveNext());

        tree.Add(3, 5);
        IEnumerator overOneValue = tree.GetEnumerator();
        tree.Clear();
        Assert.Throws<InvalidOperationException>(() => overOneValue.MoveNext());
        Assert.Equal(0, tree.PrefixSum(7));
    }

    [Fact]
    public void SegmentTreeClear_ShouldBumpTheVersionUnconditionally_BecauseEmptinessCostsAScan()
    {
        // The third fixed-length type, and it agrees with the other two. It goes further than they do: an
        // assignment that stores the value already there also bumps, because IMonoid<T> carries no equality
        // obligation and the tree will not invent one. That difference is pinned in SegmentTreeTests.
        var tree = new SegmentTree<int, MinMonoid<int>>(8);

        IEnumerator overAllIdentity = tree.GetEnumerator();
        tree.Clear();
        Assert.Throws<InvalidOperationException>(() => overAllIdentity.MoveNext());

        tree[3] = 5;
        IEnumerator overOneValue = tree.GetEnumerator();
        tree.Clear();
        Assert.Throws<InvalidOperationException>(() => overOneValue.MoveNext());
        Assert.Equal(int.MaxValue, tree.Aggregate);
    }
}
