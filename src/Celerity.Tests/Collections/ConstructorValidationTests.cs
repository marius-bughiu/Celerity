using System.Linq;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Validates that the dictionaries reject invalid constructor arguments
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
    //  LongDictionary — loadFactor validation
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(-1f)]
    public void LongDictionary_ShouldThrow_WhenLoadFactorIsZeroOrNegative(float loadFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LongDictionary<int>(capacity: 16, loadFactor: loadFactor));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void LongDictionary_ShouldThrow_WhenLoadFactorIsOneOrGreater(float loadFactor)
    {
        // loadFactor >= 1.0 would cause an infinite loop in ProbeForInsert
        // once every slot in the table is occupied.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LongDictionary<int>(capacity: 16, loadFactor: loadFactor));
    }

    [Fact]
    public void LongDictionary_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LongDictionary<int>(capacity: -1));
    }

    [Fact]
    public void LongDictionary_ShouldAcceptZeroCapacity()
    {
        // capacity = 0 is allowed; NextPowerOfTwo(0) returns 1.
        var map = new LongDictionary<int>(capacity: 0);
        map[42L] = 100;
        Assert.Equal(100, map[42L]);
    }

    [Theory]
    [InlineData(0.01f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.99f)]
    public void LongDictionary_ShouldAcceptValidLoadFactor(float loadFactor)
    {
        var map = new LongDictionary<int>(capacity: 16, loadFactor: loadFactor);
        map[1L] = 10;
        Assert.Equal(10, map[1L]);
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
    //  RobinHoodDictionary — loadFactor validation
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(-1f)]
    public void RobinHoodDictionary_ShouldThrow_WhenLoadFactorIsZeroOrNegative(float loadFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RobinHoodDictionary<int, int, Int32WangNaiveHasher>(capacity: 16, loadFactor: loadFactor));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void RobinHoodDictionary_ShouldThrow_WhenLoadFactorIsOneOrGreater(float loadFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RobinHoodDictionary<int, int, Int32WangNaiveHasher>(capacity: 16, loadFactor: loadFactor));
    }

    [Fact]
    public void RobinHoodDictionary_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RobinHoodDictionary<int, int, Int32WangNaiveHasher>(capacity: -1));
    }

    [Fact]
    public void RobinHoodDictionary_ShouldAcceptZeroCapacity()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>(capacity: 0);
        map[42] = 100;
        Assert.Equal(100, map[42]);
    }

    [Theory]
    [InlineData(0.01f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.99f)]
    public void RobinHoodDictionary_ShouldAcceptValidLoadFactor(float loadFactor)
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>(capacity: 16, loadFactor: loadFactor);
        map[1] = 10;
        Assert.Equal(10, map[1]);
    }

    // ──────────────────────────────────────────────────────────────
    //  PooledCelerityDictionary — loadFactor validation (mirror of Celerity)
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(-1f)]
    public void PooledCelerityDictionary_ShouldThrow_WhenLoadFactorIsZeroOrNegative(float loadFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(capacity: 16, loadFactor: loadFactor));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void PooledCelerityDictionary_ShouldThrow_WhenLoadFactorIsOneOrGreater(float loadFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(capacity: 16, loadFactor: loadFactor));
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(capacity: -1));
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldAcceptZeroCapacity()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(capacity: 0);
        map[42] = 100;
        Assert.Equal(100, map[42]);
    }

    [Theory]
    [InlineData(0.01f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.99f)]
    public void PooledCelerityDictionary_ShouldAcceptValidLoadFactor(float loadFactor)
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(capacity: 16, loadFactor: loadFactor);
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

    // ──────────────────────────────────────────────────────────────
    //  Convenience subclass LongDictionary<TValue> inherits validation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void LongDictionary_ConvenienceSubclass_ShouldThrow_WhenLoadFactorIsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LongDictionary<string>(capacity: 16, loadFactor: 1.0f));
    }

    [Fact]
    public void LongDictionary_ConvenienceSubclass_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LongDictionary<string>(capacity: -10));
    }

    // ──────────────────────────────────────────────────────────────
    //  CelerityMultiMap — same capacity / loadFactor validation
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(-1f)]
    public void CelerityMultiMap_ShouldThrow_WhenLoadFactorIsZeroOrNegative(float loadFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CelerityMultiMap<int, int, Int32WangNaiveHasher>(capacity: 16, loadFactor: loadFactor));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void CelerityMultiMap_ShouldThrow_WhenLoadFactorIsOneOrGreater(float loadFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CelerityMultiMap<int, int, Int32WangNaiveHasher>(capacity: 16, loadFactor: loadFactor));
    }

    [Fact]
    public void CelerityMultiMap_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CelerityMultiMap<int, int, Int32WangNaiveHasher>(capacity: -1));
    }

    [Fact]
    public void CelerityMultiMap_ShouldAcceptZeroCapacity()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>(capacity: 0);
        map.Add(42, 100);
        Assert.Equal(new[] { 100 }, map[42].ToArray());
    }

    [Theory]
    [InlineData(0.01f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.99f)]
    public void CelerityMultiMap_ShouldAcceptValidLoadFactor(float loadFactor)
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>(capacity: 16, loadFactor: loadFactor);
        map.Add(1, 10);
        Assert.Equal(new[] { 10 }, map[1].ToArray());
    }

    // ──────────────────────────────────────────────────────────────
    //  SmallDictionary — capacity validation only
    //
    //  SmallDictionary is a flat-array, linear-scan dictionary with no hash
    //  table, so it has no loadFactor parameter: the loadFactor validation rows
    //  above genuinely do not apply. Capacity is still validated (a negative
    //  capacity is rejected; zero is accepted and defers allocation).
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void SmallDictionary_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SmallDictionary<int, int>(capacity: -1));
    }

    [Fact]
    public void SmallDictionary_ShouldAcceptZeroCapacity()
    {
        var map = new SmallDictionary<int, int>(capacity: 0);
        map[42] = 100;
        Assert.Equal(100, map[42]);
    }
}
