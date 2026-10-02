using System;
using System.Collections.Generic;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// A <see cref="float.NaN"/> load factor fails both halves of a <c>loadFactor &lt;= 0f ||
/// loadFactor &gt;= 1f</c> guard, so every hash collection used to accept it and then resize on
/// every insert, doubling its table until it hit the 2^30-slot ceiling (issue #493). Each
/// constructor must reject NaN like any other value outside the open interval (0, 1) — both the
/// capacity constructor and the <see cref="IEnumerable{T}"/> source constructor, which sizes its
/// table from the load factor before delegating.
/// </summary>
public class NaNLoadFactorTests
{
    public static TheoryData<string, Action> CapacityConstructors => new()
    {
        { "IntDictionary", () => _ = new IntDictionary<int>(16, float.NaN) },
        { "LongDictionary", () => _ = new LongDictionary<int>(16, float.NaN) },
        { "CelerityDictionary", () => _ = new CelerityDictionary<int, int, Int32WangNaiveHasher>(16, float.NaN) },
        { "RobinHoodDictionary", () => _ = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>(16, float.NaN) },
        { "SwissDictionary", () => _ = new SwissDictionary<int, int, Int32WangNaiveHasher>(16, float.NaN) },
        { "HashCachingDictionary", () => _ = new HashCachingDictionary<int, int, Int32WangNaiveHasher>(16, float.NaN) },
        { "PooledCelerityDictionary", () => _ = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(16, float.NaN) },
        { "CelerityMultiMap", () => _ = new CelerityMultiMap<int, int, Int32WangNaiveHasher>(16, float.NaN) },
        { "CelerityMultiSet", () => _ = new CelerityMultiSet<int, Int32WangNaiveHasher>(16, float.NaN) },
        { "IntSet", () => _ = new IntSet(16, float.NaN) },
        { "LongSet", () => _ = new LongSet(16, float.NaN) },
        { "CeleritySet", () => _ = new CeleritySet<int, Int32WangNaiveHasher>(16, float.NaN) },
        { "RobinHoodSet", () => _ = new RobinHoodSet<int, Int32WangNaiveHasher>(16, float.NaN) },
        { "SwissSet", () => _ = new SwissSet<int, Int32WangNaiveHasher>(16, float.NaN) },
        { "HashCachingSet", () => _ = new HashCachingSet<int, Int32WangNaiveHasher>(16, float.NaN) },
        { "PooledCeleritySet", () => _ = new PooledCeleritySet<int, Int32WangNaiveHasher>(16, float.NaN) },
        { "StringInternTable", () => _ = new StringInternTable(16, float.NaN) },
    };

    public static TheoryData<string, Action> SourceConstructors => new()
    {
        { "IntDictionary", () => _ = new IntDictionary<int>(Pairs, 16, float.NaN) },
        { "LongDictionary", () => _ = new LongDictionary<int>(LongPairs, 16, float.NaN) },
        { "CelerityDictionary", () => _ = new CelerityDictionary<int, int, Int32WangNaiveHasher>(Pairs, 16, float.NaN) },
        { "RobinHoodDictionary", () => _ = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>(Pairs, 16, float.NaN) },
        { "SwissDictionary", () => _ = new SwissDictionary<int, int, Int32WangNaiveHasher>(Pairs, 16, float.NaN) },
        { "HashCachingDictionary", () => _ = new HashCachingDictionary<int, int, Int32WangNaiveHasher>(Pairs, 16, float.NaN) },
        { "PooledCelerityDictionary", () => _ = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(Pairs, 16, float.NaN) },
        { "CelerityMultiMap", () => _ = new CelerityMultiMap<int, int, Int32WangNaiveHasher>(Pairs, 16, float.NaN) },
        { "CelerityMultiSet", () => _ = new CelerityMultiSet<int, Int32WangNaiveHasher>(Items, 16, float.NaN) },
        { "IntSet", () => _ = new IntSet(Items, 16, float.NaN) },
        { "LongSet", () => _ = new LongSet(LongItems, 16, float.NaN) },
        { "CeleritySet", () => _ = new CeleritySet<int, Int32WangNaiveHasher>(Items, 16, float.NaN) },
        { "RobinHoodSet", () => _ = new RobinHoodSet<int, Int32WangNaiveHasher>(Items, 16, float.NaN) },
        { "SwissSet", () => _ = new SwissSet<int, Int32WangNaiveHasher>(Items, 16, float.NaN) },
        { "HashCachingSet", () => _ = new HashCachingSet<int, Int32WangNaiveHasher>(Items, 16, float.NaN) },
        { "PooledCeleritySet", () => _ = new PooledCeleritySet<int, Int32WangNaiveHasher>(Items, 16, float.NaN) },
    };

    private static readonly int[] Items = { 1, 2, 3 };
    private static readonly long[] LongItems = { 1L, 2L, 3L };
    private static readonly KeyValuePair<int, int>[] Pairs = { new(1, 1), new(2, 2), new(3, 3) };
    private static readonly KeyValuePair<long, int>[] LongPairs = { new(1L, 1), new(2L, 2), new(3L, 3) };

    [Theory]
    [MemberData(nameof(CapacityConstructors))]
    public void CapacityConstructor_RejectsNaNLoadFactor(string collection, Action construct)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(construct);
        Assert.Equal("loadFactor", ex.ParamName);
        Assert.False(string.IsNullOrEmpty(collection));
    }

    [Theory]
    [MemberData(nameof(SourceConstructors))]
    public void SourceConstructor_RejectsNaNLoadFactor(string collection, Action construct)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(construct);
        Assert.Equal("loadFactor", ex.ParamName);
        Assert.False(string.IsNullOrEmpty(collection));
    }
}
