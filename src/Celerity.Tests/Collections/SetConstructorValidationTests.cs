using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Constructor validation tests for <see cref="CeleritySet{T, THasher}"/>,
/// <see cref="SwissSet{T, THasher}"/>, <see cref="RobinHoodSet{T, THasher}"/>,
/// <see cref="HashCachingSet{T, THasher}"/>, <see cref="IntSet{THasher}"/>, and
/// <see cref="LongSet{THasher}"/>. Mirrors
/// <see cref="ConstructorValidationTests"/> for the dictionary types.
/// </summary>
public class SetConstructorValidationTests
{
    // ---------------------------------------------------------------
    //  CeleritySet
    // ---------------------------------------------------------------

    [Fact]
    public void CeleritySet_ShouldThrow_WhenLoadFactorIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CeleritySet<int, Int32WangNaiveHasher>(16, 0f));
    }

    [Fact]
    public void CeleritySet_ShouldThrow_WhenLoadFactorIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CeleritySet<int, Int32WangNaiveHasher>(16, -0.5f));
    }

    [Fact]
    public void CeleritySet_ShouldThrow_WhenLoadFactorIsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CeleritySet<int, Int32WangNaiveHasher>(16, 1f));
    }

    [Fact]
    public void CeleritySet_ShouldThrow_WhenLoadFactorExceedsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CeleritySet<int, Int32WangNaiveHasher>(16, 1.5f));
    }

    [Fact]
    public void CeleritySet_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CeleritySet<int, Int32WangNaiveHasher>(-1));
    }

    // ---------------------------------------------------------------
    //  SwissSet
    // ---------------------------------------------------------------

    [Fact]
    public void SwissSet_ShouldThrow_WhenLoadFactorIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SwissSet<int, Int32WangNaiveHasher>(16, 0f));
    }

    [Fact]
    public void SwissSet_ShouldThrow_WhenLoadFactorIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SwissSet<int, Int32WangNaiveHasher>(16, -0.5f));
    }

    [Fact]
    public void SwissSet_ShouldThrow_WhenLoadFactorIsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SwissSet<int, Int32WangNaiveHasher>(16, 1f));
    }

    [Fact]
    public void SwissSet_ShouldThrow_WhenLoadFactorExceedsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SwissSet<int, Int32WangNaiveHasher>(16, 1.5f));
    }

    [Fact]
    public void SwissSet_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SwissSet<int, Int32WangNaiveHasher>(-1));
    }

    // ---------------------------------------------------------------
    //  RobinHoodSet
    // ---------------------------------------------------------------

    [Fact]
    public void RobinHoodSet_ShouldThrow_WhenLoadFactorIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RobinHoodSet<int, Int32WangNaiveHasher>(16, 0f));
    }

    [Fact]
    public void RobinHoodSet_ShouldThrow_WhenLoadFactorIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RobinHoodSet<int, Int32WangNaiveHasher>(16, -0.5f));
    }

    [Fact]
    public void RobinHoodSet_ShouldThrow_WhenLoadFactorIsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RobinHoodSet<int, Int32WangNaiveHasher>(16, 1f));
    }

    [Fact]
    public void RobinHoodSet_ShouldThrow_WhenLoadFactorExceedsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RobinHoodSet<int, Int32WangNaiveHasher>(16, 1.5f));
    }

    [Fact]
    public void RobinHoodSet_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RobinHoodSet<int, Int32WangNaiveHasher>(-1));
    }

    // ---------------------------------------------------------------
    //  HashCachingSet
    // ---------------------------------------------------------------

    [Fact]
    public void HashCachingSet_ShouldThrow_WhenLoadFactorIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HashCachingSet<int, Int32WangNaiveHasher>(16, 0f));
    }

    [Fact]
    public void HashCachingSet_ShouldThrow_WhenLoadFactorIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HashCachingSet<int, Int32WangNaiveHasher>(16, -0.5f));
    }

    [Fact]
    public void HashCachingSet_ShouldThrow_WhenLoadFactorIsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HashCachingSet<int, Int32WangNaiveHasher>(16, 1f));
    }

    [Fact]
    public void HashCachingSet_ShouldThrow_WhenLoadFactorExceedsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HashCachingSet<int, Int32WangNaiveHasher>(16, 1.5f));
    }

    [Fact]
    public void HashCachingSet_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HashCachingSet<int, Int32WangNaiveHasher>(-1));
    }

    // ---------------------------------------------------------------
    //  PooledCeleritySet
    // ---------------------------------------------------------------

    [Fact]
    public void PooledCeleritySet_ShouldThrow_WhenLoadFactorIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PooledCeleritySet<int, Int32WangNaiveHasher>(16, 0f));
    }

    [Fact]
    public void PooledCeleritySet_ShouldThrow_WhenLoadFactorIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PooledCeleritySet<int, Int32WangNaiveHasher>(16, -0.5f));
    }

    [Fact]
    public void PooledCeleritySet_ShouldThrow_WhenLoadFactorIsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PooledCeleritySet<int, Int32WangNaiveHasher>(16, 1f));
    }

    [Fact]
    public void PooledCeleritySet_ShouldThrow_WhenLoadFactorExceedsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PooledCeleritySet<int, Int32WangNaiveHasher>(16, 1.5f));
    }

    [Fact]
    public void PooledCeleritySet_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PooledCeleritySet<int, Int32WangNaiveHasher>(-1));
    }

    // ---------------------------------------------------------------
    //  IntSet
    // ---------------------------------------------------------------

    [Fact]
    public void IntSet_ShouldThrow_WhenLoadFactorIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new IntSet(16, 0f));
    }

    [Fact]
    public void IntSet_ShouldThrow_WhenLoadFactorIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new IntSet(16, -0.5f));
    }

    [Fact]
    public void IntSet_ShouldThrow_WhenLoadFactorIsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new IntSet(16, 1f));
    }

    [Fact]
    public void IntSet_ShouldThrow_WhenLoadFactorExceedsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new IntSet(16, 1.5f));
    }

    [Fact]
    public void IntSet_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new IntSet(-1));
    }

    // ---------------------------------------------------------------
    //  LongSet
    // ---------------------------------------------------------------

    [Fact]
    public void LongSet_ShouldThrow_WhenLoadFactorIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LongSet(16, 0f));
    }

    [Fact]
    public void LongSet_ShouldThrow_WhenLoadFactorIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LongSet(16, -0.5f));
    }

    [Fact]
    public void LongSet_ShouldThrow_WhenLoadFactorIsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LongSet(16, 1f));
    }

    [Fact]
    public void LongSet_ShouldThrow_WhenLoadFactorExceedsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LongSet(16, 1.5f));
    }

    [Fact]
    public void LongSet_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LongSet(-1));
    }

    // ---------------------------------------------------------------
    //  SmallSet — flat-array, no hasher and NO loadFactor, so only the
    //  negative-capacity guard applies (the loadFactor rows above genuinely
    //  do not exist for this type). Mirrors SmallDictionary in
    //  ConstructorValidationTests.
    // ---------------------------------------------------------------

    [Fact]
    public void SmallSet_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SmallSet<int>(-1));
    }

    // ---------------------------------------------------------------
    //  Source constructors reject a negative capacity too (issue #460).
    //  Same source shapes and assertion as the dictionary block in
    //  ConstructorValidationTests.
    // ---------------------------------------------------------------

    public static TheoryData<string, int> NegativeCapacityWithSource =>
        ConstructorValidationTests.NegativeCapacityWithSource;

    private static IEnumerable<int> Ints(string shape) =>
        ConstructorValidationTests.SourceOf(shape, i => i);

    private static IEnumerable<long> Longs(string shape) =>
        ConstructorValidationTests.SourceOf(shape, i => (long)i);

    [Theory]
    [MemberData(nameof(NegativeCapacityWithSource))]
    public void CeleritySet_SourceCtor_ShouldThrow_WhenCapacityIsNegative(string shape, int capacity)
    {
        ConstructorValidationTests.AssertRejectsCapacity(capacity, () =>
            new CeleritySet<int, Int32WangNaiveHasher>(Ints(shape), capacity: capacity));
    }

    [Theory]
    [MemberData(nameof(NegativeCapacityWithSource))]
    public void SwissSet_SourceCtor_ShouldThrow_WhenCapacityIsNegative(string shape, int capacity)
    {
        ConstructorValidationTests.AssertRejectsCapacity(capacity, () =>
            new SwissSet<int, Int32WangNaiveHasher>(Ints(shape), capacity: capacity));
    }

    [Theory]
    [MemberData(nameof(NegativeCapacityWithSource))]
    public void RobinHoodSet_SourceCtor_ShouldThrow_WhenCapacityIsNegative(string shape, int capacity)
    {
        ConstructorValidationTests.AssertRejectsCapacity(capacity, () =>
            new RobinHoodSet<int, Int32WangNaiveHasher>(Ints(shape), capacity: capacity));
    }

    [Theory]
    [MemberData(nameof(NegativeCapacityWithSource))]
    public void HashCachingSet_SourceCtor_ShouldThrow_WhenCapacityIsNegative(string shape, int capacity)
    {
        ConstructorValidationTests.AssertRejectsCapacity(capacity, () =>
            new HashCachingSet<int, Int32WangNaiveHasher>(Ints(shape), capacity: capacity));
    }

    [Theory]
    [MemberData(nameof(NegativeCapacityWithSource))]
    public void PooledCeleritySet_SourceCtor_ShouldThrow_WhenCapacityIsNegative(string shape, int capacity)
    {
        ConstructorValidationTests.AssertRejectsCapacity(capacity, () =>
            new PooledCeleritySet<int, Int32WangNaiveHasher>(Ints(shape), capacity: capacity));
    }

    [Theory]
    [MemberData(nameof(NegativeCapacityWithSource))]
    public void IntSet_SourceCtor_ShouldThrow_WhenCapacityIsNegative(string shape, int capacity)
    {
        ConstructorValidationTests.AssertRejectsCapacity(capacity, () =>
            new IntSet<Int32WangNaiveHasher>(Ints(shape), capacity: capacity));
    }

    [Theory]
    [MemberData(nameof(NegativeCapacityWithSource))]
    public void IntSet_ConvenienceSubclass_SourceCtor_ShouldThrow_WhenCapacityIsNegative(string shape, int capacity)
    {
        ConstructorValidationTests.AssertRejectsCapacity(capacity, () =>
            new IntSet(Ints(shape), capacity: capacity));
    }

    [Theory]
    [MemberData(nameof(NegativeCapacityWithSource))]
    public void LongSet_SourceCtor_ShouldThrow_WhenCapacityIsNegative(string shape, int capacity)
    {
        ConstructorValidationTests.AssertRejectsCapacity(capacity, () =>
            new LongSet<Int64WangNaiveHasher>(Longs(shape), capacity: capacity));
    }

    [Theory]
    [MemberData(nameof(NegativeCapacityWithSource))]
    public void LongSet_ConvenienceSubclass_SourceCtor_ShouldThrow_WhenCapacityIsNegative(string shape, int capacity)
    {
        ConstructorValidationTests.AssertRejectsCapacity(capacity, () =>
            new LongSet(Longs(shape), capacity: capacity));
    }

    [Theory]
    [MemberData(nameof(NegativeCapacityWithSource))]
    public void SmallSet_SourceCtor_ShouldThrow_WhenCapacityIsNegative(string shape, int capacity)
    {
        ConstructorValidationTests.AssertRejectsCapacity(capacity, () =>
            new SmallSet<int>(Ints(shape), capacity: capacity));
    }
}
