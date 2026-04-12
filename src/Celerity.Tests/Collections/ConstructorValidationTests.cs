using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Validates that both dictionaries reject invalid constructor arguments
/// (capacity and load factor) with clear exceptions, preventing subtle
/// runtime bugs like infinite loops or resize-on-every-insert.
/// </summary>
public class ConstructorValidationTests
{
    // ──────────────────────────────────────────────────────────────
    //  IntDictionary — loadFactor validation
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(-1f)]
    public void IntDictionary_ShouldThrow_WhenLoadFactorIsZeroOrNegative(float loadFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IntDictionary<int>(capacity: 16, loadFactor: loadFactor));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void IntDictionary_ShouldThrow_WhenLoadFactorIsOneOrGreater(float loadFactor)
    {
        // loadFactor >= 1.0 would cause an infinite loop in ProbeForInsert
        // once every slot in the table is occupied.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IntDictionary<int>(capacity: 16, loadFactor: loadFactor));
    }

    [Fact]
    public void IntDictionary_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IntDictionary<int>(capacity: -1));
    }

    [Fact]
    public void IntDictionary_ShouldAcceptZeroCapacity()
    {
        // capacity = 0 is allowed; NextPowerOfTwo(0) returns 1.
        var map = new IntDictionary<int>(capacity: 0);
        map[42] = 100;
        Assert.Equal(100, map[42]);
    }

    [Theory]
    [InlineData(0.01f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.99f)]
    public void IntDictionary_ShouldAcceptValidLoadFactor(float loadFactor)
    {
        var map = new IntDictionary<int>(capacity: 16, loadFactor: loadFactor);
        map[1] = 10;
        Assert.Equal(10, map[1]);
    }

    // ──────────────────────────────────────────────────────────────
    //  CelerityDictionary — loadFactor validation
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(-1f)]
    public void CelerityDictionary_ShouldThrow_WhenLoadFactorIsZeroOrNegative(float loadFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CelerityDictionary<int, int, Int32WangNaiveHasher>(capacity: 16, loadFactor: loadFactor));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void CelerityDictionary_ShouldThrow_WhenLoadFactorIsOneOrGreater(float loadFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CelerityDictionary<int, int, Int32WangNaiveHasher>(capacity: 16, loadFactor: loadFactor));
    }

    [Fact]
    public void CelerityDictionary_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CelerityDictionary<int, int, Int32WangNaiveHasher>(capacity: -1));
    }

    [Fact]
    public void CelerityDictionary_ShouldAcceptZeroCapacity()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>(capacity: 0);
        map[42] = 100;
        Assert.Equal(100, map[42]);
    }

    [Theory]
    [InlineData(0.01f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.99f)]
    public void CelerityDictionary_ShouldAcceptValidLoadFactor(float loadFactor)
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>(capacity: 16, loadFactor: loadFactor);
        map[1] = 10;
        Assert.Equal(10, map[1]);
    }

    // ──────────────────────────────────────────────────────────────
    //  Convenience subclass IntDictionary<TValue> inherits validation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void IntDictionary_ConvenienceSubclass_ShouldThrow_WhenLoadFactorIsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IntDictionary<string>(capacity: 16, loadFactor: 1.0f));
    }

    [Fact]
    public void IntDictionary_ConvenienceSubclass_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IntDictionary<string>(capacity: -10));
    }
}
