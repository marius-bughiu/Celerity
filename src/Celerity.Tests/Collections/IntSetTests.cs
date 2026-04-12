using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class IntSetTests
{
    [Fact]
    public void TryAdd_ShouldAddAndContain()
    {
        var set = new IntSet();
        Assert.True(set.TryAdd(10));
        Assert.True(set.Contains(10));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void TryAdd_ShouldReturnFalse_WhenDuplicate()
    {
        var set = new IntSet();
        Assert.True(set.TryAdd(10));
        Assert.False(set.TryAdd(10));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Add_ShouldThrow_WhenDuplicate()
    {
        var set = new IntSet();
        set.Add(10);
        Assert.Throws<ArgumentException>(() => set.Add(10));
    }

    [Fact]
    public void Contains_ShouldReturnFalse_WhenNotPresent()
    {
        var set = new IntSet();
        Assert.False(set.Contains(99));
    }

    [Fact]
    public void Remove_ShouldDeleteElement()
    {
        var set = new IntSet();
        set.Add(7);

        Assert.True(set.Remove(7));
        Assert.False(set.Contains(7));
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenNotPresent()
    {
        var set = new IntSet();
        Assert.False(set.Remove(7));
    }

    [Fact]
    public void Set_ShouldResize_WhenThresholdExceeded()
    {
        var set = new IntSet(4);
        set.Add(1);
        set.Add(2);
        set.Add(3);
        set.Add(4); // Triggers resize

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(2));
        Assert.True(set.Contains(3));
        Assert.True(set.Contains(4));
    }

    // Regression: zero collides with EMPTY_SLOT sentinel.
    [Fact]
    public void TryAdd_ShouldHandleZero()
    {
        var set = new IntSet();
        Assert.True(set.TryAdd(0));
        Assert.True(set.Contains(0));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void TryAdd_Zero_ShouldReturnFalse_WhenDuplicate()
    {
        var set = new IntSet();
        Assert.True(set.TryAdd(0));
        Assert.False(set.TryAdd(0));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Remove_ShouldHandleZero()
    {
        var set = new IntSet();
        set.Add(0);
        set.Add(1);

        Assert.True(set.Remove(0));
        Assert.False(set.Contains(0));
        Assert.True(set.Contains(1));
        Assert.Equal(1, set.Count);
        Assert.False(set.Remove(0));
    }

    [Fact]
    public void Zero_ShouldSurviveResize()
    {
        var set = new IntSet(4);
        set.Add(0);
        set.Add(1);
        set.Add(2);
        set.Add(3);
        set.Add(4); // Triggers resize while zero entry is live.

        Assert.Equal(5, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(4));
    }

    [Fact]
    public void Clear_ShouldRemoveAllElements()
    {
        var set = new IntSet();
        for (int i = 0; i < 32; i++)
            set.Add(i);

        set.Clear();

        Assert.Equal(0, set.Count);
        for (int i = 0; i < 32; i++)
            Assert.False(set.Contains(i));

        // Reusable after clear.
        set.Add(0);
        set.Add(5);
        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(5));
    }

    [Fact]
    public void RemoveThenReinsert_ManyElements()
    {
        var set = new IntSet(8);
        for (int i = 1; i <= 100; i++)
            set.Add(i);

        for (int i = 1; i <= 100; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(50, set.Count);

        for (int i = 1; i <= 100; i += 2)
            set.Add(i);

        Assert.Equal(100, set.Count);
        for (int i = 1; i <= 100; i++)
            Assert.True(set.Contains(i));
    }

    // Constructor validation (parity with ConstructorValidationTests for dictionaries)
    [Fact]
    public void Constructor_ShouldThrow_WhenLoadFactorTooLow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntSet(16, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntSet(16, -0.5f));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLoadFactorTooHigh()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntSet(16, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntSet(16, 1.5f));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCapacityNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntSet(-1));
    }
}
