using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Pins the explicitly-implemented <see cref="ICollection{T}"/> surface of the set family —
/// the members that are invisible on the concrete type and only reachable through an
/// <c>ICollection&lt;T&gt;</c> reference.
///
/// <para>
/// The interesting member is <c>ICollection&lt;T&gt;.Add</c>. Celerity's sets deliberately
/// diverge from the BCL on the public <c>Add(T)</c>: it <i>throws</i>
/// <see cref="ArgumentException"/> when the element is already present, so that a caller who
/// believes an insert is new finds out immediately instead of silently losing it. That choice is
/// safe for the public surface, but it would be a contract violation on the interface —
/// <c>ICollection&lt;T&gt;.Add</c> is documented as the set-semantics "add if absent" operation,
/// and every framework helper that fills a collection through the interface
/// (<c>CollectionExtensions</c>, object/collection initializers on an interface-typed local,
/// serializers, LINQ-adjacent copy loops) relies on a duplicate being a silent no-op. A set that
/// threw from there would blow up mid-copy on any source containing a repeat.
/// </para>
///
/// <para>
/// So each type routes the explicit implementation to the non-throwing <c>TryAdd</c>, and that is
/// exactly what these tests assert: through an <c>ICollection&lt;T&gt;</c> reference, adding the
/// same element twice must complete without throwing and leave <c>Count</c> at one, while the
/// public <c>Add</c> on the very same instance still throws for the duplicate. The two entry
/// points are only distinguishable this way — a test that went through the concrete type would
/// never reach the explicit member at all.
/// </para>
///
/// <para>
/// <c>ICollection&lt;T&gt;.IsReadOnly</c> is pinned as <c>false</c> for every type in the family.
/// It is a one-line property, but callers branch on it before attempting a mutation (and the BCL's
/// own read-only wrappers key off it), so a type that reported <c>true</c> would be quietly skipped
/// by generic copy/merge code rather than failing loudly.
/// </para>
///
/// Coverage is per concrete type rather than table-driven because the sets do not share a base
/// class and several have distinct shapes — <see cref="SparseSet"/> is bounded by a universe,
/// <see cref="PooledCeleritySet{T, THasher}"/> is <see cref="IDisposable"/>, and
/// <see cref="SmallSet{T}"/> is unhashed — so each needs its own construction.
/// </summary>
public class SetExplicitICollectionMemberTests
{
    // ---------------------------------------------------------------- CeleritySet-family (T, THasher)

    [Fact]
    public void ICollectionAdd_ShouldNotThrowAndKeepCountAtOne_WhenHashCachingSetAlreadyContainsItem()
    {
        var set = new HashCachingSet<int, Int32WangNaiveHasher>();
        ICollection<int> collection = set;

        collection.Add(42);
        collection.Add(42);

        Assert.Equal(1, collection.Count);
        Assert.Contains(42, set);

        // The public entry point keeps the throw-on-duplicate contract the interface must not have.
        var ex = Assert.Throws<ArgumentException>(() => set.Add(42));
        Assert.Equal("item", ex.ParamName);
    }

    [Fact]
    public void ICollectionIsReadOnly_ShouldBeFalse_WhenSetIsHashCachingSet()
    {
        ICollection<int> collection = new HashCachingSet<int, Int32WangNaiveHasher>();

        Assert.False(collection.IsReadOnly);
    }

    [Fact]
    public void ICollectionAdd_ShouldNotThrowAndKeepCountAtOne_WhenRobinHoodSetAlreadyContainsItem()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
        ICollection<int> collection = set;

        collection.Add(42);
        collection.Add(42);

        Assert.Equal(1, collection.Count);
        Assert.Contains(42, set);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(42));
        Assert.Equal("item", ex.ParamName);
    }

    [Fact]
    public void ICollectionIsReadOnly_ShouldBeFalse_WhenSetIsRobinHoodSet()
    {
        ICollection<int> collection = new RobinHoodSet<int, Int32WangNaiveHasher>();

        Assert.False(collection.IsReadOnly);
    }

    [Fact]
    public void ICollectionAdd_ShouldNotThrowAndKeepCountAtOne_WhenSwissSetAlreadyContainsItem()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();
        ICollection<int> collection = set;

        collection.Add(42);
        collection.Add(42);

        Assert.Equal(1, collection.Count);
        Assert.Contains(42, set);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(42));
        Assert.Equal("item", ex.ParamName);
    }

    [Fact]
    public void ICollectionIsReadOnly_ShouldBeFalse_WhenSetIsSwissSet()
    {
        ICollection<int> collection = new SwissSet<int, Int32WangNaiveHasher>();

        Assert.False(collection.IsReadOnly);
    }

    [Fact]
    public void ICollectionAdd_ShouldNotThrowAndKeepCountAtOne_WhenPooledCeleritySetAlreadyContainsItem()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        ICollection<int> collection = set;

        collection.Add(42);
        collection.Add(42);

        Assert.Equal(1, collection.Count);
        Assert.Contains(42, set);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(42));
        Assert.Equal("item", ex.ParamName);
    }

    [Fact]
    public void ICollectionIsReadOnly_ShouldBeFalse_WhenSetIsPooledCeleritySet()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        ICollection<int> collection = set;

        Assert.False(collection.IsReadOnly);
    }

    // ---------------------------------------------------------------- primitive-specialized sets

    [Fact]
    public void ICollectionAdd_ShouldNotThrowAndKeepCountAtOne_WhenIntSetAlreadyContainsItem()
    {
        var set = new IntSet<Int32WangNaiveHasher>();
        ICollection<int> collection = set;

        collection.Add(42);
        collection.Add(42);

        Assert.Equal(1, collection.Count);
        Assert.Contains(42, set);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(42));
        Assert.Equal("item", ex.ParamName);
    }

    [Fact]
    public void ICollectionIsReadOnly_ShouldBeFalse_WhenSetIsIntSet()
    {
        ICollection<int> collection = new IntSet<Int32WangNaiveHasher>();

        Assert.False(collection.IsReadOnly);
    }

    [Fact]
    public void ICollectionAdd_ShouldNotThrowAndKeepCountAtOne_WhenLongSetAlreadyContainsItem()
    {
        var set = new LongSet<Int64WangNaiveHasher>();
        ICollection<long> collection = set;

        collection.Add(42L);
        collection.Add(42L);

        Assert.Equal(1, collection.Count);
        Assert.Contains(42L, set);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(42L));
        Assert.Equal("item", ex.ParamName);
    }

    [Fact]
    public void ICollectionIsReadOnly_ShouldBeFalse_WhenSetIsLongSet()
    {
        ICollection<long> collection = new LongSet<Int64WangNaiveHasher>();

        Assert.False(collection.IsReadOnly);
    }

    // ---------------------------------------------------------------- unhashed / bounded sets

    [Fact]
    public void ICollectionAdd_ShouldNotThrowAndKeepCountAtOne_WhenSmallSetAlreadyContainsItem()
    {
        var set = new SmallSet<int>();
        ICollection<int> collection = set;

        collection.Add(42);
        collection.Add(42);

        Assert.Equal(1, collection.Count);
        Assert.Contains(42, set);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(42));
        Assert.Equal("item", ex.ParamName);
    }

    [Fact]
    public void ICollectionIsReadOnly_ShouldBeFalse_WhenSetIsSmallSet()
    {
        ICollection<int> collection = new SmallSet<int>();

        Assert.False(collection.IsReadOnly);
    }

    [Fact]
    public void ICollectionAdd_ShouldNotThrowAndKeepCountAtOne_WhenSparseSetAlreadyContainsItem()
    {
        var set = new SparseSet(universe: 64);
        ICollection<int> collection = set;

        collection.Add(42);
        collection.Add(42);

        Assert.Equal(1, collection.Count);
        Assert.Contains(42, set);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(42));
        Assert.Equal("item", ex.ParamName);
    }

    [Fact]
    public void ICollectionAdd_ShouldThrowArgumentOutOfRange_WhenSparseSetItemIsOutsideUniverse()
    {
        ICollection<int> collection = new SparseSet(universe: 64);

        // The universe bound is a precondition of the type, not a duplicate: unlike a repeat
        // element it must still surface through the interface rather than be swallowed.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => collection.Add(64));
        Assert.Equal("item", ex.ParamName);
    }

    [Fact]
    public void ICollectionIsReadOnly_ShouldBeFalse_WhenSetIsSparseSet()
    {
        ICollection<int> collection = new SparseSet(universe: 8);

        Assert.False(collection.IsReadOnly);
    }
}
