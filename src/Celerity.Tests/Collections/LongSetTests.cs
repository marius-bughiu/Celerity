using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class LongSetTests
{
    [Fact]
    public void TryAdd_ShouldAddAndContain()
    {
        var set = new LongSet();
        Assert.True(set.TryAdd(10L));
        Assert.True(set.Contains(10L));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void TryAdd_ShouldReturnFalse_WhenDuplicate()
    {
        var set = new LongSet();
        Assert.True(set.TryAdd(10L));
        Assert.False(set.TryAdd(10L));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Add_ShouldThrow_WhenDuplicate()
    {
        var set = new LongSet();
        set.Add(10L);
        Assert.Throws<ArgumentException>(() => set.Add(10L));
    }

    [Fact]
    public void Contains_ShouldReturnFalse_WhenNotPresent()
    {
        var set = new LongSet();
        Assert.False(set.Contains(99L));
    }

    [Fact]
    public void Remove_ShouldDeleteElement()
    {
        var set = new LongSet();
        set.Add(7L);

        Assert.True(set.Remove(7L));
        Assert.False(set.Contains(7L));
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenNotPresent()
    {
        var set = new LongSet();
        Assert.False(set.Remove(7L));
    }

    [Fact]
    public void Set_ShouldResize_WhenThresholdExceeded()
    {
        var set = new LongSet(4);
        set.Add(1L);
        set.Add(2L);
        set.Add(3L);
        set.Add(4L); // Triggers resize

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(1L));
        Assert.True(set.Contains(2L));
        Assert.True(set.Contains(3L));
        Assert.True(set.Contains(4L));
    }

    // Regression: zero collides with EMPTY_SLOT sentinel.
    [Fact]
    public void TryAdd_ShouldHandleZero()
    {
        var set = new LongSet();
        Assert.True(set.TryAdd(0L));
        Assert.True(set.Contains(0L));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void TryAdd_Zero_ShouldReturnFalse_WhenDuplicate()
    {
        var set = new LongSet();
        Assert.True(set.TryAdd(0L));
        Assert.False(set.TryAdd(0L));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Remove_ShouldHandleZero()
    {
        var set = new LongSet();
        set.Add(0L);
        set.Add(1L);

        Assert.True(set.Remove(0L));
        Assert.False(set.Contains(0L));
        Assert.True(set.Contains(1L));
        Assert.Equal(1, set.Count);
        Assert.False(set.Remove(0L));
    }

    [Fact]
    public void Zero_ShouldSurviveResize()
    {
        var set = new LongSet(4);
        set.Add(0L);
        set.Add(1L);
        set.Add(2L);
        set.Add(3L);
        set.Add(4L); // Triggers resize while zero entry is live.

        Assert.Equal(5, set.Count);
        Assert.True(set.Contains(0L));
        Assert.True(set.Contains(1L));
        Assert.True(set.Contains(4L));
    }

    [Fact]
    public void Clear_ShouldRemoveAllElements()
    {
        var set = new LongSet();
        for (long i = 0; i < 32; i++)
            set.Add(i);

        set.Clear();

        Assert.Equal(0, set.Count);
        for (long i = 0; i < 32; i++)
            Assert.False(set.Contains(i));

        // Reusable after clear.
        set.Add(0L);
        set.Add(5L);
        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(0L));
        Assert.True(set.Contains(5L));
    }

    [Fact]
    public void RemoveThenReinsert_ManyElements()
    {
        var set = new LongSet(8);
        for (long i = 1; i <= 100; i++)
            set.Add(i);

        for (long i = 1; i <= 100; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(50, set.Count);

        for (long i = 1; i <= 100; i += 2)
            set.Add(i);

        Assert.Equal(100, set.Count);
        for (long i = 1; i <= 100; i++)
            Assert.True(set.Contains(i));
    }

    // Long-specific: extreme key values that exercise the full 64-bit range.
    [Fact]
    public void ExtremeValues_ShouldRoundTrip()
    {
        var set = new LongSet();
        long[] items = {
            long.MaxValue,
            long.MinValue,
            (long)int.MaxValue + 1L,
            (long)int.MinValue - 1L,
            -1L,
        };

        foreach (long item in items)
            set.Add(item);

        Assert.Equal(items.Length, set.Count);
        foreach (long item in items)
            Assert.True(set.Contains(item), $"expected to contain {item}");
    }

    // Long-specific: two values sharing the same lower 32 bits but differing in
    // the upper 32 bits must be kept distinct. Guards against any accidental
    // int-truncation on the probe path.
    [Fact]
    public void UpperBitsOnlyDifference_ShouldKeepValuesDistinct()
    {
        var set = new LongSet();
        long a = 0x0000_0001_0000_0001L;
        long b = 0x0000_0002_0000_0001L;

        set.Add(a);
        set.Add(b);

        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(a));
        Assert.True(set.Contains(b));
    }

    // Smoke test: struct enumerator yields every element (zero-first when
    // present) and the boxed IEnumerable<long> path participates in LINQ.
    [Fact]
    public void Enumerator_ShouldYieldAllElements()
    {
        var set = new LongSet { 0L, 1L, 2L, long.MaxValue };

        var seen = new HashSet<long>();
        foreach (long item in set)
            seen.Add(item);

        Assert.Equal(4, seen.Count);
        Assert.Contains(0L, seen);
        Assert.Contains(1L, seen);
        Assert.Contains(2L, seen);
        Assert.Contains(long.MaxValue, seen);
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenModifiedDuringEnumeration()
    {
        var set = new LongSet { 1L, 2L, 3L };
        var enumerator = set.GetEnumerator();

        set.Add(4L);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    // IEnumerable<long> ctor parity: BCL HashSet<T> semantics — duplicates
    // silently deduped, ICollection<T> count used to size storage, null source
    // throws ArgumentNullException.
    [Fact]
    public void IEnumerableConstructor_ShouldCopyAndDeduplicate()
    {
        var source = new long[] { 1L, 2L, 2L, 3L, 0L, 0L };
        var set = new LongSet(source);

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(0L));
        Assert.True(set.Contains(1L));
        Assert.True(set.Contains(2L));
        Assert.True(set.Contains(3L));
    }

    [Fact]
    public void IEnumerableConstructor_ShouldThrow_WhenSourceNull()
    {
        Assert.Throws<ArgumentNullException>(() => new LongSet(source: null!));
    }

    // Constructor validation (parity with ConstructorValidationTests for dictionaries)
    [Fact]
    public void Constructor_ShouldThrow_WhenLoadFactorTooLow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LongSet(16, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LongSet(16, -0.5f));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLoadFactorTooHigh()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LongSet(16, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LongSet(16, 1.5f));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCapacityNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LongSet(-1));
    }

    // Custom-hasher open-generic smoke test: confirms LongSet<THasher> is usable
    // with a non-default IHashProvider<long>.
    private struct IdentityLongHasher : IHashProvider<long>
    {
        public int Hash(long key) => unchecked((int)key);
    }

    [Fact]
    public void OpenGeneric_ShouldWorkWithCustomHasher()
    {
        var set = new LongSet<IdentityLongHasher>();
        set.Add(1L);
        set.Add(long.MaxValue);
        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(1L));
        Assert.True(set.Contains(long.MaxValue));
    }
}
