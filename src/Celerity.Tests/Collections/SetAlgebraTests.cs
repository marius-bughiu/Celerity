using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Targeted behavioural and conformance tests for the <see cref="ISet{T}"/>
/// set-algebra surface added to the mutable set family. The exhaustive
/// correctness comparison against <see cref="HashSet{T}"/> lives in
/// <see cref="SetAlgebraDifferentialTests"/>; this file pins down argument
/// validation, interface conformance, <c>CopyTo</c>, the out-of-band zero / null
/// element, and enumerator invalidation.
/// </summary>
public class SetAlgebraTests
{
    private static CeleritySet<int, Int32WangNaiveHasher> IntCelerity(params int[] items)
    {
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
        foreach (int i in items)
            set.TryAdd(i);
        return set;
    }

    // ── Interface conformance ─────────────────────────────────────────────────

    [Fact]
    public void CeleritySet_ImplementsISet()
    {
        ISet<int> set = IntCelerity(1, 2, 3);
        Assert.IsAssignableFrom<ICollection<int>>(set);
        Assert.False(set.IsReadOnly);
        Assert.Equal(3, set.Count);
    }

    [Fact]
    public void IntSet_LongSet_SwissSet_RobinHoodSet_HashCachingSet_PooledCeleritySet_SmallSet_ImplementISet()
    {
        Assert.IsAssignableFrom<ISet<int>>(new IntSet());
        Assert.IsAssignableFrom<ISet<long>>(new LongSet());
        Assert.IsAssignableFrom<ISet<int>>(new SwissSet<int, Int32WangNaiveHasher>());
        Assert.IsAssignableFrom<ISet<int>>(new RobinHoodSet<int, Int32WangNaiveHasher>());
        Assert.IsAssignableFrom<ISet<int>>(new HashCachingSet<int, Int32WangNaiveHasher>());
        Assert.IsAssignableFrom<ISet<int>>(new SmallSet<int>());
        using var pooled = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        Assert.IsAssignableFrom<ISet<int>>(pooled);
    }

    [Fact]
    public void ISetAdd_ReturnsTrueThenFalse()
    {
        ISet<int> set = IntCelerity();
        Assert.True(set.Add(7));
        Assert.False(set.Add(7));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void ICollectionAdd_DoesNotThrowOnDuplicate()
    {
        ICollection<int> set = IntCelerity(5);
        // Unlike the public throw-on-duplicate Add(int), the ICollection<T>.Add
        // contract silently ignores an existing element.
        set.Add(5);
        Assert.Single(set);
    }

    [Fact]
    public void PublicAdd_StillThrowsOnDuplicate()
    {
        var set = IntCelerity(5);
        Assert.Throws<ArgumentException>(() => set.Add(5));
    }

    // ── Argument validation ───────────────────────────────────────────────────

    [Fact]
    public void MutatingOps_ThrowOnNullOther()
    {
        var set = IntCelerity(1);
        Assert.Throws<ArgumentNullException>(() => set.UnionWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.IntersectWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.ExceptWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.SymmetricExceptWith(null!));
    }

    [Fact]
    public void QueryOps_ThrowOnNullOther()
    {
        var set = IntCelerity(1);
        Assert.Throws<ArgumentNullException>(() => set.IsSubsetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsProperSubsetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsSupersetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsProperSupersetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.Overlaps(null!));
        Assert.Throws<ArgumentNullException>(() => set.SetEquals(null!));
    }

    // ── Basic behaviour ───────────────────────────────────────────────────────

    [Fact]
    public void UnionWith_AddsMissingElements()
    {
        var set = IntCelerity(1, 2, 3);
        set.UnionWith(new[] { 3, 4, 5 });
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, set.OrderBy(x => x));
    }

    [Fact]
    public void IntersectWith_KeepsOnlyShared()
    {
        var set = IntCelerity(1, 2, 3, 4);
        set.IntersectWith(new[] { 2, 4, 6 });
        Assert.Equal(new[] { 2, 4 }, set.OrderBy(x => x));
    }

    [Fact]
    public void IntersectWith_EmptyOther_ClearsSet()
    {
        var set = IntCelerity(1, 2, 3);
        set.IntersectWith(Array.Empty<int>());
        Assert.Empty(set);
    }

    [Fact]
    public void ExceptWith_RemovesElements()
    {
        var set = IntCelerity(1, 2, 3, 4);
        set.ExceptWith(new[] { 2, 4 });
        Assert.Equal(new[] { 1, 3 }, set.OrderBy(x => x));
    }

    [Fact]
    public void SymmetricExceptWith_TogglesElements()
    {
        var set = IntCelerity(1, 2, 3);
        set.SymmetricExceptWith(new[] { 2, 3, 4, 4 }); // duplicate 4 must toggle once
        Assert.Equal(new[] { 1, 4 }, set.OrderBy(x => x));
    }

    [Fact]
    public void QueryOps_ReturnExpected()
    {
        var set = IntCelerity(1, 2, 3);
        Assert.True(set.IsSubsetOf(new[] { 1, 2, 3, 4 }));
        Assert.True(set.IsProperSubsetOf(new[] { 1, 2, 3, 4 }));
        Assert.False(set.IsProperSubsetOf(new[] { 1, 2, 3 }));
        Assert.True(set.IsSupersetOf(new[] { 1, 2 }));
        Assert.True(set.IsProperSupersetOf(new[] { 1, 2 }));
        Assert.False(set.IsProperSupersetOf(new[] { 1, 2, 3 }));
        Assert.True(set.Overlaps(new[] { 3, 9 }));
        Assert.False(set.Overlaps(new[] { 7, 8 }));
        Assert.True(set.SetEquals(new[] { 3, 2, 1 }));
        Assert.False(set.SetEquals(new[] { 1, 2 }));
    }

    [Fact]
    public void EmptySet_SubsetSemantics()
    {
        var empty = IntCelerity();
        Assert.True(empty.IsSubsetOf(new[] { 1, 2 }));
        Assert.True(empty.IsSubsetOf(Array.Empty<int>()));
        Assert.True(empty.IsProperSubsetOf(new[] { 1 }));
        Assert.False(empty.IsProperSubsetOf(Array.Empty<int>()));
        Assert.True(empty.IsSupersetOf(Array.Empty<int>()));
        Assert.False(empty.IsProperSupersetOf(Array.Empty<int>()));
        Assert.False(empty.Overlaps(new[] { 1 }));
        Assert.True(empty.SetEquals(Array.Empty<int>()));
    }

    // ── Self-aliasing ─────────────────────────────────────────────────────────

    [Fact]
    public void SelfAliasing_MatchesHashSet()
    {
        var set = IntCelerity(1, 2, 3);

        var union = IntCelerity(1, 2, 3);
        union.UnionWith(union);
        Assert.Equal(new[] { 1, 2, 3 }, union.OrderBy(x => x));

        var intersect = IntCelerity(1, 2, 3);
        intersect.IntersectWith(intersect);
        Assert.Equal(new[] { 1, 2, 3 }, intersect.OrderBy(x => x));

        var except = IntCelerity(1, 2, 3);
        except.ExceptWith(except);
        Assert.Empty(except);

        var sym = IntCelerity(1, 2, 3);
        sym.SymmetricExceptWith(sym);
        Assert.Empty(sym);

        Assert.True(set.SetEquals(set));
        Assert.False(set.IsProperSubsetOf(set));
    }

    // ── Out-of-band zero / default element ────────────────────────────────────

    [Fact]
    public void ZeroElement_ParticipatesInSetAlgebra()
    {
        var set = IntCelerity(0, 1, 2);
        Assert.True(set.Contains(0));

        set.SymmetricExceptWith(new[] { 0, 3 }); // removes 0, adds 3
        Assert.Equal(new[] { 1, 2, 3 }, set.OrderBy(x => x));

        set.UnionWith(new[] { 0 });
        Assert.Contains(0, set);

        set.ExceptWith(new[] { 0 });
        Assert.DoesNotContain(0, set);
    }

    [Fact]
    public void NullElement_ParticipatesInSetAlgebra()
    {
        var set = new CeleritySet<string, DefaultHasher<string>>();
        set.TryAdd("a");
        set.TryAdd(null!); // out-of-band default(string) == null
        set.TryAdd("b");

        Assert.True(set.SetEquals(new[] { "b", null!, "a" }));
        Assert.True(set.IsSupersetOf(new string[] { null! }));

        set.ExceptWith(new string[] { null! });
        Assert.Equal(2, set.Count);
        Assert.DoesNotContain(null!, set);

        set.SymmetricExceptWith(new string[] { null! }); // re-adds null
        Assert.Contains(null!, set);
    }

    // ── CopyTo ────────────────────────────────────────────────────────────────

    [Fact]
    public void CopyTo_CopiesAllElements()
    {
        var set = IntCelerity(1, 2, 3);
        var array = new int[5];
        set.CopyTo(array, 1);
        Assert.Equal(0, array[0]);
        Assert.Equal(new[] { 1, 2, 3 }, array.Skip(1).Take(3).OrderBy(x => x));
    }

    [Fact]
    public void CopyTo_ThroughICollection_IncludesZeroElement()
    {
        ICollection<int> set = IntCelerity(0, 5, 9);
        var array = new int[3];
        set.CopyTo(array, 0);
        Assert.Equal(new[] { 0, 5, 9 }, array.OrderBy(x => x));
    }

    [Fact]
    public void CopyTo_Validation()
    {
        var set = IntCelerity(1, 2, 3);
        Assert.Throws<ArgumentNullException>(() => set.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.CopyTo(new int[3], -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.CopyTo(new int[3], 4));
        Assert.Throws<ArgumentException>(() => set.CopyTo(new int[3], 1)); // 3 elements won't fit
    }

    // ── Enumerator invalidation ───────────────────────────────────────────────

    [Fact]
    public void MutatingSetOp_InvalidatesActiveEnumerator()
    {
        var set = IntCelerity(1, 2, 3);
        using var e = set.GetEnumerator();
        e.MoveNext();
        set.UnionWith(new[] { 99 }); // structural change bumps _version
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void NonMutatingSetOp_DoesNotInvalidateEnumerator()
    {
        var set = IntCelerity(1, 2, 3);
        using var e = set.GetEnumerator();
        e.MoveNext();
        _ = set.IsSubsetOf(new[] { 1, 2, 3, 4 }); // query only, no mutation
        _ = set.Overlaps(new[] { 2 });
        // Enumeration continues without throwing.
        while (e.MoveNext()) { }
    }
}
