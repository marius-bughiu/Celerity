using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="FenwickTree{T}"/>: the point-update / prefix-sum / range-sum core, the
/// indexer get/set, the three constructors, boundary and validation corners, <see cref="FenwickTree{T}.Clear"/>,
/// and the enumeration surface. The randomized reconciliation against a naive array oracle lives in
/// <see cref="FenwickTreeDifferentialTests"/>.
/// </summary>
public class FenwickTreeTests
{
    [Fact]
    public void Constructor_ShouldStartAllZero_WhenGivenLength()
    {
        var tree = new FenwickTree<int>(8);

        Assert.Equal(8, tree.Count);
        Assert.Equal(0, tree.Total);
        for (int i = 0; i < 8; i++)
            Assert.Equal(0, tree[i]);
        Assert.Equal(0, tree.PrefixSum(8));
    }

    [Fact]
    public void Constructor_ShouldAllowZeroLength()
    {
        var tree = new FenwickTree<int>(0);

        Assert.Equal(0, tree.Count);
        Assert.Equal(0, tree.Total);
        Assert.Equal(0, tree.PrefixSum(0));
        Assert.Empty(tree);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLengthNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new FenwickTree<int>(-1));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLengthExceedsMaxSupported()
    {
        // The 1-based layout needs length + 1 array slots, so anything above Array.MaxLength - 1 must be
        // rejected up front with a clear ArgumentOutOfRangeException rather than overflowing into an
        // OverflowException / OutOfMemoryException from the allocation.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new FenwickTree<int>(int.MaxValue));
        Assert.Equal("length", ex.ParamName);

        var atCeiling = Assert.Throws<ArgumentOutOfRangeException>(() => new FenwickTree<int>(Array.MaxLength));
        Assert.Equal("length", atCeiling.ParamName);
    }

    [Fact]
    public void ArrayConstructor_ShouldSeedLogicalValues()
    {
        long[] values = { 3, 1, 4, 1, 5, 9, 2, 6 };
        var tree = new FenwickTree<long>(values);

        Assert.Equal(8, tree.Count);
        long running = 0;
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(values[i], tree[i]);
            running += values[i];
            Assert.Equal(running, tree.PrefixSum(i + 1));
        }

        Assert.Equal(31, tree.Total);
    }

    [Fact]
    public void ArrayConstructor_ShouldNotAliasSourceArray()
    {
        long[] source = { 1, 2, 3, 4 };
        var tree = new FenwickTree<long>(source);

        tree.Add(0, 100);

        Assert.Equal(1, source[0]); // mutating the tree must not write back into the caller's array
    }

    [Fact]
    public void EnumerableConstructor_ShouldSeedLogicalValues_FromCollection()
    {
        var tree = new FenwickTree<int>(new List<int> { 10, 20, 30, 40 });

        Assert.Equal(4, tree.Count);
        Assert.Equal(new[] { 10, 20, 30, 40 }, tree.ToArray());
        Assert.Equal(100, tree.Total);
        Assert.Equal(50, tree.RangeSum(1, 3)); // elements at index 1 and 2: 20 + 30
    }

    [Fact]
    public void EnumerableConstructor_ShouldSeedLogicalValues_FromLazySequence()
    {
        // A non-ICollection source exercises the List<T> materialization fallback.
        IEnumerable<int> Lazy()
        {
            for (int i = 1; i <= 5; i++)
                yield return i * i;
        }

        var tree = new FenwickTree<int>(Lazy());

        Assert.Equal(5, tree.Count);
        Assert.Equal(new[] { 1, 4, 9, 16, 25 }, tree.ToArray());
        Assert.Equal(55, tree.Total);
    }

    [Fact]
    public void EnumerableConstructor_ShouldHandleEmptySource()
    {
        // Boundary for the counted fast path: CopyTo targets index 1 of a length-1 backing array.
        var fromCollection = new FenwickTree<int>(Array.Empty<int>());
        Assert.Equal(0, fromCollection.Count);
        Assert.Equal(0, fromCollection.Total);
        Assert.Empty(fromCollection);

        var fromLazy = new FenwickTree<int>(Enumerable.Empty<int>());
        Assert.Equal(0, fromLazy.Count);
        Assert.Equal(0, fromLazy.Total);
        Assert.Empty(fromLazy);
    }

    [Fact]
    public void EnumerableConstructor_ShouldSeedLogicalValues_FromNonListCollection()
    {
        // A counted source that is neither T[] nor List<T>, so the ICollection<T>.CopyTo path is what runs.
        var source = new SortedSet<int> { 4, 1, 3 }; // enumerates ascending: 1, 3, 4
        var tree = new FenwickTree<int>(source);

        Assert.Equal(3, tree.Count);
        Assert.Equal(new[] { 1, 3, 4 }, tree.ToArray());
        Assert.Equal(8, tree.Total);
    }

    [Fact]
    public void EnumerableConstructor_ShouldThrow_WhenSourceNull()
    {
        Assert.Throws<ArgumentNullException>(() => new FenwickTree<int>((IEnumerable<int>)null!));
    }

    [Fact]
    public void Add_ShouldAccumulateAtIndex()
    {
        var tree = new FenwickTree<int>(6);

        tree.Add(2, 5);
        tree.Add(2, 3);
        tree.Add(4, 10);

        Assert.Equal(8, tree[2]);
        Assert.Equal(10, tree[4]);
        Assert.Equal(8, tree.PrefixSum(3));
        Assert.Equal(18, tree.PrefixSum(5));
        Assert.Equal(18, tree.Total);
    }

    [Fact]
    public void Add_ShouldSubtract_WhenDeltaNegative()
    {
        var tree = new FenwickTree<int>(new[] { 5, 5, 5, 5 });

        tree.Add(1, -3);

        Assert.Equal(2, tree[1]);
        Assert.Equal(17, tree.Total);
    }

    [Fact]
    public void Add_ShouldThrow_WhenIndexOutOfRange()
    {
        var tree = new FenwickTree<int>(4);

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Add(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Add(4, 1));
    }

    [Fact]
    public void Indexer_Set_ShouldReplaceValue()
    {
        var tree = new FenwickTree<int>(new[] { 1, 2, 3, 4, 5 });

        tree[2] = 30;

        Assert.Equal(30, tree[2]);
        Assert.Equal(1 + 2 + 30 + 4 + 5, tree.Total);
        Assert.Equal(33, tree.PrefixSum(3));
    }

    [Fact]
    public void Indexer_Set_ShouldBeIdempotent_WhenAssignedSameValue()
    {
        var tree = new FenwickTree<int>(new[] { 7, 8, 9 });

        tree[1] = 8;

        Assert.Equal(new[] { 7, 8, 9 }, tree.ToArray());
    }

    [Fact]
    public void Indexer_Set_ShouldNotInvalidateEnumerator_WhenAssignedSameValue()
    {
        // Assigning the value already stored does not change the observable state, so — like every other
        // no-op in the library — it must not bump the version and invalidate active enumerators.
        var tree = new FenwickTree<int>(new[] { 7, 8, 9 });

        var seen = new List<int>();
        foreach (int v in tree)
        {
            tree[1] = 8; // no-op assignment
            seen.Add(v);
        }

        Assert.Equal(new[] { 7, 8, 9 }, seen);
    }

    [Fact]
    public void Add_ShouldNotInvalidateEnumerator_WhenDeltaZero()
    {
        var tree = new FenwickTree<int>(new[] { 1, 2, 3 });

        var seen = new List<int>();
        foreach (int v in tree)
        {
            tree.Add(0, 0); // adding zero changes nothing
            seen.Add(v);
        }

        Assert.Equal(new[] { 1, 2, 3 }, seen);
    }

    [Fact]
    public void Add_ShouldStillInvalidateEnumerator_WhenDeltaNonZero()
    {
        var tree = new FenwickTree<int>(new[] { 1, 2, 3 });

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (int _ in tree)
                tree.Add(0, 1);
        });
    }

    [Fact]
    public void Indexer_Get_ShouldThrow_WhenIndexOutOfRange()
    {
        var tree = new FenwickTree<int>(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = tree[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = tree[3]);
    }

    [Fact]
    public void Indexer_Set_ShouldThrow_WhenIndexOutOfRange()
    {
        var tree = new FenwickTree<int>(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => tree[-1] = 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => tree[3] = 1);
    }

    [Fact]
    public void PrefixSum_ShouldReturnZero_AtStart()
    {
        var tree = new FenwickTree<int>(new[] { 1, 2, 3 });

        Assert.Equal(0, tree.PrefixSum(0));
    }

    [Fact]
    public void PrefixSum_ShouldReturnTotal_AtFullLength()
    {
        var tree = new FenwickTree<int>(new[] { 1, 2, 3 });

        Assert.Equal(6, tree.PrefixSum(3));
        Assert.Equal(tree.Total, tree.PrefixSum(tree.Count));
    }

    [Fact]
    public void PrefixSum_ShouldThrow_WhenOutOfRange()
    {
        var tree = new FenwickTree<int>(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.PrefixSum(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.PrefixSum(4));
    }

    [Fact]
    public void RangeSum_ShouldReturnHalfOpenSum()
    {
        var tree = new FenwickTree<int>(new[] { 2, 4, 6, 8, 10 });

        Assert.Equal(4 + 6 + 8, tree.RangeSum(1, 4));
        Assert.Equal(30, tree.RangeSum(0, 5));
    }

    [Fact]
    public void RangeSum_ShouldReturnZero_WhenEmptyRange()
    {
        var tree = new FenwickTree<int>(new[] { 2, 4, 6 });

        Assert.Equal(0, tree.RangeSum(2, 2));
        Assert.Equal(0, tree.RangeSum(0, 0));
        Assert.Equal(0, tree.RangeSum(3, 3));
    }

    [Fact]
    public void RangeSum_ShouldThrow_WhenInvalid()
    {
        var tree = new FenwickTree<int>(4);

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.RangeSum(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.RangeSum(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.RangeSum(3, 2)); // end < start
    }

    [Fact]
    public void Clear_ShouldResetAllToZero()
    {
        var tree = new FenwickTree<int>(new[] { 1, 2, 3, 4 });

        tree.Clear();

        Assert.Equal(0, tree.Total);
        for (int i = 0; i < 4; i++)
            Assert.Equal(0, tree[i]);
        Assert.Equal(4, tree.Count); // length is fixed; only the values reset
    }

    [Fact]
    public void Clear_ThenReuse_ShouldWork()
    {
        var tree = new FenwickTree<int>(new[] { 1, 2, 3, 4 });

        tree.Clear();
        tree.Add(0, 100);
        tree.Add(3, 5);

        Assert.Equal(105, tree.Total);
        Assert.Equal(100, tree[0]);
        Assert.Equal(5, tree[3]);
    }

    [Fact]
    public void Enumerator_ShouldYieldLogicalValuesInOrder()
    {
        var tree = new FenwickTree<int>(new[] { 3, 1, 4, 1, 5, 9 });

        Assert.Equal(new[] { 3, 1, 4, 1, 5, 9 }, tree.ToArray());
    }

    [Fact]
    public void Enumerator_ShouldReflectUpdates()
    {
        var tree = new FenwickTree<int>(new[] { 1, 1, 1, 1 });
        tree[2] = 9;
        tree.Add(0, 5);

        Assert.Equal(new[] { 6, 1, 9, 1 }, tree.ToArray());
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenMutatedDuringEnumeration()
    {
        var tree = new FenwickTree<int>(new[] { 1, 2, 3 });

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (int _ in tree)
                tree.Add(0, 1);
        });
    }

    [Fact]
    public void Enumerator_QueryDuringEnumeration_ShouldNotInvalidate()
    {
        var tree = new FenwickTree<int>(new[] { 1, 2, 3, 4 });

        int sum = 0;
        foreach (int v in tree)
        {
            _ = tree.PrefixSum(2); // a pure query must not bump the version
            sum += v;
        }

        Assert.Equal(10, sum);
    }

    [Fact]
    public void Enumerator_Reset_ShouldRestart()
    {
        var tree = new FenwickTree<int>(new[] { 5, 6, 7 });
        FenwickTree<int>.Enumerator e = tree.GetEnumerator();

        Assert.True(e.MoveNext());
        Assert.Equal(5, e.Current);
        e.Reset();
        Assert.True(e.MoveNext());
        Assert.Equal(5, e.Current);
    }

    [Fact]
    public void Enumerator_NonGeneric_ShouldYieldValues()
    {
        var tree = new FenwickTree<int>(new[] { 2, 4, 6 });

        var result = new List<object?>();
        IEnumerator e = ((IEnumerable)tree).GetEnumerator();
        while (e.MoveNext())
            result.Add(e.Current);

        Assert.Equal(new object?[] { 2, 4, 6 }, result);
    }

    // Regression for the index-overflow fix. Both Fenwick ascents advance by adding the lowest set bit, so at
    // k == 1 << 30 the next index is 1 << 31 — which overflows a signed int and wraps to int.MinValue, a
    // negative value that still passes the `<= _length` bound and then indexes the array out of range. Lengths
    // that large are permitted (the ceiling is Array.MaxLength - 1), so this is reachable rather than
    // theoretical: the smallest INumber<T> is one byte, making a 2^30-element tree about 1 GiB.
    //
    // Verified to throw IndexOutOfRangeException against the pre-fix code and pass after the widening, so this
    // is the regression guard for that fix. It has to build a real 2^30-element tree: the overflow depends on
    // _length, so no smaller instance can reach the failing step, and an arithmetic-only assertion would just
    // re-evaluate the expression in the test rather than exercise FenwickTree at all.
    //
    // The cost is far lower than the 1 GiB figure suggests — the array is committed but never faulted in
    // beyond the ~30 cells the ascent touches (the runtime zeroes lazily), so it completes in ~17 ms. Where
    // that headroom genuinely is not available (a memory-capped container or runner), MemoryIntensiveFact
    // reports the test skipped rather than running it, so it can never turn the build red on resource grounds.
    [MemoryIntensiveFact(1024)]
    public void Add_ShouldNotOverflowIndex_WhenTreeExceedsTwoToThe30()
    {
        const int length = 1 << 30; // 2^30 one-byte cells + the reserved 1-based slot ≈ 1 GiB
        var tree = new FenwickTree<byte>(length);

        // index + 1 == 1 << 30, so the ascent lands exactly on the overflowing step.
        tree.Add(length - 1, 1);

        Assert.Equal((byte)1, tree[length - 1]);
        Assert.Equal((byte)1, tree.Total);
    }

    [Fact]
    public void FenwickTree_ShouldWorkWithDoubleValues()
    {
        var tree = new FenwickTree<double>(new[] { 1.5, 2.5, 3.0 });

        Assert.Equal(7.0, tree.Total);
        Assert.Equal(4.0, tree.RangeSum(0, 2));
        tree.Add(2, 1.0);
        Assert.Equal(8.0, tree.Total);
    }

    [Fact]
    public void FenwickTree_ShouldWorkWithLongValues_AtScale()
    {
        // Long avoids the int-overflow the summed magnitudes would otherwise risk, and exercises a larger tree.
        const int n = 1000;
        var tree = new FenwickTree<long>(n);
        for (int i = 0; i < n; i++)
            tree.Add(i, i);

        long expected = (long)(n - 1) * n / 2;
        Assert.Equal(expected, tree.Total);
        Assert.Equal(expected, tree.PrefixSum(n));
        Assert.Equal(45, tree.PrefixSum(10)); // 0+1+...+9
    }
}
