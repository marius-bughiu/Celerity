using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Pins the observable behaviour of both halves of the capacity-sizing hint that
/// every <c>IEnumerable&lt;T&gt;</c> set constructor runs before chaining to its
/// primary constructor:
/// <c>(source as ICollection&lt;T&gt;)?.Count ?? 0</c>.
///
/// That single expression has two distinct outcomes. When the caller hands over an
/// array, a <see cref="List{T}"/>, or any other <see cref="ICollection{T}"/>, the
/// set learns the element count up front and pre-sizes its backing store (scaled by
/// <c>1/loadFactor</c> for the hash-table sets) so the bulk fill never rehashes.
/// When the caller hands over a lazily-yielded sequence — an iterator, a LINQ
/// pipeline, anything that cannot report a count without being consumed — the
/// type test fails, the null-coalescing fallback supplies <c>0</c>, and the set
/// falls back to the caller's plain <c>capacity</c> and grows organically as it
/// fills.
///
/// The whole point is that this is a pure optimisation: the two paths must produce
/// sets that are indistinguishable to a caller. These tests therefore build the same
/// logical element sequence twice — once through a counted source and once through a
/// private iterator method — and assert identical <c>Count</c>, identical lookup
/// results, and identical enumerated contents for both. A regression that mis-sizes
/// the table (or, worse, uses the hint as if it were the element count) shows up here
/// as a missing or duplicated element rather than as a silent performance cliff.
///
/// <see cref="SetIEnumerableConstructorTests"/> covers the rest of the source-ctor
/// contract (null rejection, dedupe, out-of-band elements, source independence); this
/// class exists purely to exercise both sides of the counted/uncounted split for
/// every set in the family.
///
/// The uncounted sources here are deliberately produced by <see cref="LazyRange"/>, a
/// C# iterator method. LINQ operators such as <c>Enumerable.Range</c> are not a
/// reliable stand-in: the BCL special-cases several of them to implement
/// <see cref="ICollection{T}"/> so that <c>Count()</c> and <c>ToArray()</c> stay
/// O(1), which would quietly take the *counted* branch instead. A compiler-generated
/// iterator implements <see cref="IEnumerable{T}"/> and nothing else, so the fallback
/// is guaranteed. <see cref="LazyRange_ShouldNotImplementICollection_SoTheFallbackIsGenuinelyTaken"/>
/// guards that premise.
/// </summary>
public class SetSourceCountBranchTests
{
    /// <summary>
    /// The number of elements each source produces. Chosen comfortably above every
    /// set's <c>DEFAULT_CAPACITY</c> so the sizing hint genuinely changes how much
    /// growth happens during the fill, rather than both paths trivially fitting in
    /// the default table.
    /// </summary>
    private const int ElementCount = 40;

    // A compiler-generated iterator: IEnumerable<int> and nothing more. Casting it to
    // ICollection<int> yields null, which is what drives the `?? 0` fallback.
    private static IEnumerable<int> LazyRange(int start, int count)
    {
        for (int i = 0; i < count; i++)
            yield return start + i;
    }

    // A counted source: int[] implements ICollection<int>, so the type test succeeds
    // and Count is read without enumerating.
    private static int[] CountedRange(int start, int count)
    {
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = start + i;

        return items;
    }

    private static void AssertHoldsRange(ISet<int> set, int start, int count)
    {
        Assert.Equal(count, set.Count);

        // Lookup surface: every element must be findable through the set's own
        // Contains, and nothing outside the range may have leaked in.
        for (int i = 0; i < count; i++)
            Assert.Contains(start + i, set);

        Assert.DoesNotContain(start - 1, set);
        Assert.DoesNotContain(start + count, set);

        // Enumeration surface: the stored contents must be exactly the range, with no
        // duplicates and no empty slots yielded. Sets are unordered, so sort first.
        var enumerated = set.ToList();
        enumerated.Sort();
        Assert.Equal(Enumerable.Range(start, count), enumerated);
    }

    // ──────────────────────────────────────────────────────────────
    //  Premise guard
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void LazyRange_ShouldNotImplementICollection_SoTheFallbackIsGenuinelyTaken()
    {
        IEnumerable<int> lazy = LazyRange(1, ElementCount);

        // If this ever becomes non-null the "uncounted" tests below would silently
        // stop exercising the `?? 0` fallback and start duplicating the counted ones.
        Assert.Null(lazy as ICollection<int>);

        // …and the counterpart really is counted, so the two halves differ.
        Assert.NotNull(CountedRange(1, ElementCount) as ICollection<int>);
    }

    // ──────────────────────────────────────────────────────────────
    //  IntSet
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void IntSet_ShouldCopyEveryElement_WhenSourceReportsACount()
    {
        var set = new IntSet(CountedRange(1, ElementCount));

        AssertHoldsRange(set, 1, ElementCount);
    }

    [Fact]
    public void IntSet_ShouldCopyEveryElement_WhenSourceCannotReportACount()
    {
        var set = new IntSet(LazyRange(1, ElementCount));

        AssertHoldsRange(set, 1, ElementCount);
    }

    // ──────────────────────────────────────────────────────────────
    //  CeleritySet
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void CeleritySet_ShouldCopyEveryElement_WhenSourceReportsACount()
    {
        var set = new CeleritySet<int, Int32WangNaiveHasher>(CountedRange(1, ElementCount));

        AssertHoldsRange(set, 1, ElementCount);
    }

    [Fact]
    public void CeleritySet_ShouldCopyEveryElement_WhenSourceCannotReportACount()
    {
        var set = new CeleritySet<int, Int32WangNaiveHasher>(LazyRange(1, ElementCount));

        AssertHoldsRange(set, 1, ElementCount);
    }

    // ──────────────────────────────────────────────────────────────
    //  SwissSet
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void SwissSet_ShouldCopyEveryElement_WhenSourceReportsACount()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>(CountedRange(1, ElementCount));

        AssertHoldsRange(set, 1, ElementCount);
    }

    [Fact]
    public void SwissSet_ShouldCopyEveryElement_WhenSourceCannotReportACount()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>(LazyRange(1, ElementCount));

        AssertHoldsRange(set, 1, ElementCount);
    }

    // ──────────────────────────────────────────────────────────────
    //  RobinHoodSet
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void RobinHoodSet_ShouldCopyEveryElement_WhenSourceReportsACount()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>(CountedRange(1, ElementCount));

        AssertHoldsRange(set, 1, ElementCount);
    }

    [Fact]
    public void RobinHoodSet_ShouldCopyEveryElement_WhenSourceCannotReportACount()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>(LazyRange(1, ElementCount));

        AssertHoldsRange(set, 1, ElementCount);
    }

    // ──────────────────────────────────────────────────────────────
    //  HashCachingSet
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void HashCachingSet_ShouldCopyEveryElement_WhenSourceReportsACount()
    {
        var set = new HashCachingSet<int, Int32WangNaiveHasher>(CountedRange(1, ElementCount));

        AssertHoldsRange(set, 1, ElementCount);
    }

    [Fact]
    public void HashCachingSet_ShouldCopyEveryElement_WhenSourceCannotReportACount()
    {
        var set = new HashCachingSet<int, Int32WangNaiveHasher>(LazyRange(1, ElementCount));

        AssertHoldsRange(set, 1, ElementCount);
    }

    // ──────────────────────────────────────────────────────────────
    //  PooledCeleritySet
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void PooledCeleritySet_ShouldCopyEveryElement_WhenSourceReportsACount()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>(CountedRange(1, ElementCount));

        AssertHoldsRange(set, 1, ElementCount);
    }

    [Fact]
    public void PooledCeleritySet_ShouldCopyEveryElement_WhenSourceCannotReportACount()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>(LazyRange(1, ElementCount));

        AssertHoldsRange(set, 1, ElementCount);
    }

    // ──────────────────────────────────────────────────────────────
    //  SmallSet
    // ──────────────────────────────────────────────────────────────
    //
    //  SmallSet has no load factor — its hint is a plain
    //  Math.Max(capacity, source-count) over a flat, linear-scanned array — but the
    //  counted/uncounted split is the same, and so is the guarantee that it cannot
    //  change what the set holds.

    [Fact]
    public void SmallSet_ShouldCopyEveryElement_WhenSourceReportsACount()
    {
        var set = new SmallSet<int>(CountedRange(1, ElementCount));

        AssertHoldsRange(set, 1, ElementCount);
    }

    [Fact]
    public void SmallSet_ShouldCopyEveryElement_WhenSourceCannotReportACount()
    {
        var set = new SmallSet<int>(LazyRange(1, ElementCount));

        AssertHoldsRange(set, 1, ElementCount);
    }

    [Fact]
    public void SmallSet_ShouldPreferCallerCapacity_WhenSourceCannotReportACount()
    {
        // The uncounted branch feeds 0 into Math.Max, so the caller's capacity wins
        // outright. The set must still grow past it and keep every element.
        var set = new SmallSet<int>(LazyRange(1, ElementCount), capacity: 2);

        AssertHoldsRange(set, 1, ElementCount);
    }

    // ──────────────────────────────────────────────────────────────
    //  Cross-check: the hint must not be observable
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void CountedAndUncountedSources_ShouldProduceEquivalentSets()
    {
        var fromCounted = new CeleritySet<int, Int32WangNaiveHasher>(CountedRange(1, ElementCount));
        var fromLazy = new CeleritySet<int, Int32WangNaiveHasher>(LazyRange(1, ElementCount));

        Assert.Equal(fromCounted.Count, fromLazy.Count);

        var countedItems = fromCounted.ToList();
        countedItems.Sort();
        var lazyItems = fromLazy.ToList();
        lazyItems.Sort();

        Assert.Equal(countedItems, lazyItems);
    }
}
