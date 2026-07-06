using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Constructor validation tests for <see cref="CeleritySet{T, THasher}"/>,
/// <see cref="SwissSet{T, THasher}"/>, <see cref="RobinHoodSet{T, THasher}"/>,
/// <see cref="IntSet{THasher}"/>, and
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
}
