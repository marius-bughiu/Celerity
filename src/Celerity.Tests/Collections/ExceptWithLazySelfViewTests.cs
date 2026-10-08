using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Pins the one documented place where the mutable set family's set algebra departs from
/// <see cref="HashSet{T}"/>: <c>ExceptWith</c> over a lazy view of the same set (for example
/// <c>s.ExceptWith(s.Where(...))</c>). Every Celerity set's <c>Remove</c> invalidates live
/// enumerators, so the lazy view fails fast with <see cref="InvalidOperationException"/>, while
/// <see cref="HashSet{T}"/> completes. Materializing the view first works on both.
/// </summary>
public class ExceptWithLazySelfViewTests
{
    private enum Flag { A, B, C, D }

    public static TheoryData<string> SetNames => new()
    {
        nameof(CeleritySet<int, Int32WangNaiveHasher>),
        nameof(SwissSet<int, Int32WangNaiveHasher>),
        nameof(RobinHoodSet<int, Int32WangNaiveHasher>),
        nameof(HashCachingSet<int, Int32WangNaiveHasher>),
        nameof(IntSet),
        nameof(SmallSet<int>),
        nameof(SparseSet),
        nameof(CompressedIntSet),
        nameof(BTreeSet<int>),
        nameof(RankedSet<int>),
        nameof(PooledCeleritySet<int, Int32WangNaiveHasher>),
    };

    private static ISet<int> Create(string name) => name switch
    {
        nameof(CeleritySet<int, Int32WangNaiveHasher>) => new CeleritySet<int, Int32WangNaiveHasher>(),
        nameof(SwissSet<int, Int32WangNaiveHasher>) => new SwissSet<int, Int32WangNaiveHasher>(),
        nameof(RobinHoodSet<int, Int32WangNaiveHasher>) => new RobinHoodSet<int, Int32WangNaiveHasher>(),
        nameof(HashCachingSet<int, Int32WangNaiveHasher>) => new HashCachingSet<int, Int32WangNaiveHasher>(),
        nameof(IntSet) => new IntSet(),
        nameof(SmallSet<int>) => new SmallSet<int>(),
        nameof(SparseSet) => new SparseSet(64),
        nameof(CompressedIntSet) => new CompressedIntSet(),
        nameof(BTreeSet<int>) => new BTreeSet<int>(),
        nameof(RankedSet<int>) => new RankedSet<int>(),
        nameof(PooledCeleritySet<int, Int32WangNaiveHasher>) => new PooledCeleritySet<int, Int32WangNaiveHasher>(),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static ISet<int> Filled(string name)
    {
        ISet<int> set = Create(name);
        for (int i = 0; i < 16; i++)
            set.Add(i);
        return set;
    }

    [Fact]
    public void HashSet_CompletesExceptWith_OverALazyViewOfItself()
    {
        var set = new HashSet<int>(Enumerable.Range(0, 16));

        set.ExceptWith(set.Where(x => x < 8));

        Assert.Equal(Enumerable.Range(8, 8), set.Order());
    }

    [Theory]
    [MemberData(nameof(SetNames))]
    public void ExceptWith_ShouldFailFast_OverALazyViewOfTheSameSet(string name)
    {
        ISet<int> set = Filled(name);

        Assert.Throws<InvalidOperationException>(() => set.ExceptWith(set.Where(x => x < 8)));
    }

    [Theory]
    [MemberData(nameof(SetNames))]
    public void ExceptWith_ShouldMatchHashSet_WhenTheSelfViewIsMaterializedFirst(string name)
    {
        ISet<int> set = Filled(name);

        set.ExceptWith(set.Where(x => x < 8).ToList());

        Assert.Equal(Enumerable.Range(8, 8), set.Order());
    }

    [Fact]
    public void LongSet_ExceptWith_ShouldFailFast_OverALazyViewOfItself()
    {
        var set = new LongSet();
        for (long i = 0; i < 16; i++)
            set.Add(i);

        Assert.Throws<InvalidOperationException>(() => set.ExceptWith(set.Where(x => x < 8)));

        set.ExceptWith(set.Where(x => x < 8).ToList());
        Assert.Equal(Enumerable.Range(8, 8).Select(x => (long)x), set.Order());
    }

    [Fact]
    public void EnumSet_ExceptWith_ShouldFailFast_OverALazyViewOfItself()
    {
        var set = new EnumSet<Flag>(new[] { Flag.A, Flag.B, Flag.C, Flag.D });

        Assert.Throws<InvalidOperationException>(() => set.ExceptWith(set.Where(x => x < Flag.C)));

        set.ExceptWith(set.Where(x => x < Flag.C).ToList());
        Assert.Equal(new[] { Flag.C, Flag.D }, set.Order());
    }
}
