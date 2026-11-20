using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Set-algebra coverage for <see cref="CompressedLongSet"/>, driven across the <b>container-pair
/// matrix</b>: every binary operation is run for all nine combinations of the sorted-array, bitmap
/// and run-length forms on the two operands, in both operand orders, against a
/// <see cref="HashSet{T}"/> oracle.
///
/// <para>
/// The containers and their set algebra are shared with <see cref="CompressedIntSet"/>, so the matrix
/// here pins that the 64-bit wrapper routes every operation to them correctly — with operands placed
/// past the 32-bit range, so a key truncated to 32 bits would show up as a wrong answer.
/// </para>
///
/// <para>
/// The chunk-index merge (both operands' chunk lists walked in key order, including chunks that
/// differ only in the high 32 bits) and the <see cref="IEnumerable{T}"/> fallback — taken whenever the
/// other side is not a <see cref="CompressedLongSet"/> — are covered separately at the bottom.
/// </para>
/// </summary>
public class CompressedLongSetSetAlgebraTests
{
    private const int ArrayToBitmapThreshold = 4096;
    private const int BitmapPayloadBytes = 8192;

    // A chunk-aligned origin outside the 32-bit range: Base + [0, 65,535] is exactly one chunk.
    private const long Base = 1L << 40;

    /// <summary>How a test wants a single-chunk operand physically stored.</summary>
    public enum Form
    {
        Array,
        Bitmap,
        Run,
    }

    private static long[] At(params int[] offsets) => offsets.Select(o => Base + o).ToArray();

    private static IEnumerable<long> RangeAt(int start, int count) => Enumerable.Range(start, count).Select(o => Base + o);

    // Two clustered, unequal-sized operands inside one chunk. Clustered so the run form is genuinely
    // smaller than the alternatives (otherwise Optimize would decline it), unequal so the
    // "walk the smaller side" branch of the intersect/overlap paths is taken in both directions.
    private static long[] LeftValues => RangeAt(100, 300)
        .Concat(RangeAt(1000, 100))
        .Append(Base + 5000)
        .ToArray();

    private static long[] RightValues => RangeAt(300, 250)
        .Concat(RangeAt(1050, 50))
        .Append(Base + 7000)
        .ToArray();

    public static TheoryData<Form, Form> ContainerPairs()
    {
        var data = new TheoryData<Form, Form>();
        foreach (Form left in Enum.GetValues<Form>())
        {
            foreach (Form right in Enum.GetValues<Form>())
                data.Add(left, right);
        }

        return data;
    }

    private static CompressedLongSet Build(Form form, IEnumerable<long> values)
    {
        var set = new CompressedLongSet(values);

        switch (form)
        {
            case Form.Array:
                Assert.True(set.MemoryUsageInBytes < BitmapPayloadBytes);
                return set;

            case Form.Run:
                long asArray = set.MemoryUsageInBytes;
                set.Optimize();
                Assert.True(set.MemoryUsageInBytes < asArray);
                return set;

            default:
                // Push the chunk past the array→bitmap crossover with filler, then take the filler
                // back out: a removal never demotes, so the chunk keeps the bitmap form while
                // holding exactly the requested values.
                var filler = new List<long>();
                for (long v = Base; filler.Count <= ArrayToBitmapThreshold; v++)
                {
                    if (set.TryAdd(v))
                        filler.Add(v);
                }

                foreach (long v in filler)
                    Assert.True(set.Remove(v));

                Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);
                return set;
        }
    }

    // ---- the container-pair matrix -----------------------------------------------------

    [Theory]
    [MemberData(nameof(ContainerPairs))]
    public void UnionWith_ShouldMatchHashSet_ForEveryContainerPair(Form left, Form right)
        => AssertMutationMatchesOracle(left, right, (s, o) => s.UnionWith(o), (s, o) => s.UnionWith(o));

    [Theory]
    [MemberData(nameof(ContainerPairs))]
    public void IntersectWith_ShouldMatchHashSet_ForEveryContainerPair(Form left, Form right)
        => AssertMutationMatchesOracle(left, right, (s, o) => s.IntersectWith(o), (s, o) => s.IntersectWith(o));

    [Theory]
    [MemberData(nameof(ContainerPairs))]
    public void ExceptWith_ShouldMatchHashSet_ForEveryContainerPair(Form left, Form right)
        => AssertMutationMatchesOracle(left, right, (s, o) => s.ExceptWith(o), (s, o) => s.ExceptWith(o));

    [Theory]
    [MemberData(nameof(ContainerPairs))]
    public void SymmetricExceptWith_ShouldMatchHashSet_ForEveryContainerPair(Form left, Form right)
        => AssertMutationMatchesOracle(left, right, (s, o) => s.SymmetricExceptWith(o), (s, o) => s.SymmetricExceptWith(o));

    [Theory]
    [MemberData(nameof(ContainerPairs))]
    public void Queries_ShouldMatchHashSet_ForEveryContainerPair(Form left, Form right)
    {
        RunBothOrders(left, right, (set, other, expected, expectedOther) =>
        {
            Assert.Equal(expected.Overlaps(expectedOther), set.Overlaps(other));
            Assert.Equal(expected.SetEquals(expectedOther), set.SetEquals(other));
            Assert.Equal(expected.IsSubsetOf(expectedOther), set.IsSubsetOf(other));
            Assert.Equal(expected.IsSupersetOf(expectedOther), set.IsSupersetOf(other));
            Assert.Equal(expected.IsProperSubsetOf(expectedOther), set.IsProperSubsetOf(other));
            Assert.Equal(expected.IsProperSupersetOf(expectedOther), set.IsProperSupersetOf(other));

            long overlap = expected.Count(expectedOther.Contains);
            Assert.Equal(overlap, set.IntersectCount(other));
            Assert.Equal(overlap, other.IntersectCount(set));
        });
    }

    private static void AssertMutationMatchesOracle(
        Form left,
        Form right,
        Action<CompressedLongSet, CompressedLongSet> apply,
        Action<HashSet<long>, HashSet<long>> applyToOracle)
    {
        RunBothOrders(left, right, (set, other, expected, expectedOther) =>
        {
            apply(set, other);
            applyToOracle(expected, expectedOther);

            Assert.Equal(expected.Count, set.Count);
            Assert.Equal(expected.OrderBy(v => v), set);

            // The right-hand operand is read-only to every operation, including its representation:
            // reading a run container must never decompress it.
            Assert.Equal(expectedOther.OrderBy(v => v), other);
        });
    }

    private static void RunBothOrders(
        Form left,
        Form right,
        Action<CompressedLongSet, CompressedLongSet, HashSet<long>, HashSet<long>> body)
    {
        body(Build(left, LeftValues), Build(right, RightValues), new HashSet<long>(LeftValues), new HashSet<long>(RightValues));
        body(Build(right, RightValues), Build(left, LeftValues), new HashSet<long>(RightValues), new HashSet<long>(LeftValues));
    }

    // ---- results that outgrow the array form ---------------------------------------------

    [Fact]
    public void ExceptWith_ShouldProduceABitmapResult_WhenTheRemainderIsStillDense()
    {
        // A run-encoded left operand against a tiny array right operand: neither side is a bitmap,
        // so the cursor path builds the result — and the result is far too big for an array.
        var set = new CompressedLongSet();
        set.AddRange(Base, Base + 9999);
        var other = new CompressedLongSet(At(1, 2, 3));

        set.ExceptWith(other);

        Assert.Equal(9997, set.Count);
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);
        Assert.False(set.Contains(Base + 1));
        Assert.True(set.Contains(Base));
        Assert.True(set.Contains(Base + 4));

        // A second large-result operation on the same instance reuses the scratch bitmap.
        set.ExceptWith(new CompressedLongSet(At(4, 5)));
        Assert.Equal(9995, set.Count);
        Assert.False(set.Contains(Base + 4));
        Assert.True(set.Contains(Base + 6));
    }

    [Fact]
    public void ExceptWith_ShouldProduceABitmapResult_WhenTheLeftOperandIsAlreadyABitmap()
    {
        var set = new CompressedLongSet(Enumerable.Range(0, 5000).Select(i => Base + (i * 2)));
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);

        set.ExceptWith(new CompressedLongSet(At(0, 2, 4)));

        Assert.Equal(4997, set.Count);
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);
        Assert.False(set.Contains(Base));
        Assert.True(set.Contains(Base + 6));
    }

    [Fact]
    public void IntersectWith_ShouldDemoteTheBitmapToAnArray_WhenTheResultBecomesSparse()
    {
        var set = new CompressedLongSet(Enumerable.Range(0, 5000).Select(i => Base + (i * 2)));
        var other = new CompressedLongSet(Enumerable.Range(0, 5000).Select(i => Base + (i * 2)));
        other.IntersectWith(new CompressedLongSet(At(0, 2, 4, 6)));

        set.IntersectWith(other);

        Assert.Equal(At(0, 2, 4, 6), set);
        Assert.True(set.MemoryUsageInBytes < BitmapPayloadBytes);
    }

    [Fact]
    public void UnionWith_ShouldMergeTwoBitmapsWordwise_WhenBothChunksAreDense()
    {
        var set = new CompressedLongSet(Enumerable.Range(0, 5000).Select(i => Base + (i * 2)));
        var other = new CompressedLongSet(Enumerable.Range(0, 5000).Select(i => Base + (i * 2) + 1));

        set.UnionWith(other);

        Assert.Equal(10_000, set.Count);
        Assert.Equal(RangeAt(0, 10_000), set);
    }

    [Fact]
    public void SymmetricExceptWith_ShouldXorTwoBitmapsWordwise_WhenBothChunksAreDense()
    {
        var set = new CompressedLongSet(RangeAt(0, 6000));
        var other = new CompressedLongSet(RangeAt(3000, 6000));

        set.SymmetricExceptWith(other);

        Assert.Equal(RangeAt(0, 3000).Concat(RangeAt(6000, 3000)), set);
    }

    // ---- the chunk-index merge -----------------------------------------------------------

    [Fact]
    public void UnionWith_ShouldAdoptChunksTheLeftSideDoesNotHave()
    {
        var set = new CompressedLongSet(At(1, 300_000));
        var other = new CompressedLongSet(new long[] { -Base - 500_000, Base + 2, Base + 900_000 });
        other.TryAdd(-Base - 500_001);

        set.UnionWith(other);

        Assert.Equal(new long[] { -Base - 500_001, -Base - 500_000, Base + 1, Base + 2, Base + 300_000, Base + 900_000 }, set);

        // The adopted chunks must be copies: mutating the source afterwards cannot reach into the
        // union's result.
        other.Remove(Base + 900_000);
        Assert.True(set.Contains(Base + 900_000));
    }

    [Fact]
    public void UnionWith_ShouldCopyABitmapChunk_WhenTheLeftSideHasNothingThere()
    {
        var set = new CompressedLongSet(At(1));
        var other = new CompressedLongSet(Enumerable.Range(1_000_000, 5000).Select(i => Base + (i * 2)));

        set.UnionWith(other);

        Assert.Equal(5001, set.Count);
        Assert.True(set.Contains(Base + 2_000_000));

        other.Clear();
        Assert.Equal(5001, set.Count);
    }

    [Fact]
    public void ExceptWith_ShouldDropAChunkEmptiedByTheOperation()
    {
        var set = new CompressedLongSet(At(1, 2, 500_000, 900_000));
        long before = set.MemoryUsageInBytes;

        // 300_000 and 12_345_678 sit in chunks the left side does not have — one before a surviving
        // chunk and one after — so the merge has to skip forward on the right without losing them.
        set.ExceptWith(new CompressedLongSet(At(300_000, 500_000, 12_345_678)));

        Assert.Equal(At(1, 2, 900_000), set);
        Assert.True(set.MemoryUsageInBytes < before);
    }

    [Fact]
    public void IntersectWith_ShouldKeepOnlyTheSharedChunks()
    {
        // 300_000 sits in a chunk the left side does not have, before a chunk it does — so the merge
        // has to skip forward on the right without consuming a left chunk.
        var set = new CompressedLongSet(At(1, 500_000, 900_000));
        var other = new CompressedLongSet(At(2, 300_000, 500_000, 1_500_000));

        set.IntersectWith(other);

        Assert.Equal(At(500_000), set);
    }

    [Fact]
    public void IntersectWith_ShouldKeepChunksApart_WhenTheyDifferOnlyInTheHigh32Bits()
    {
        // 5 and 5 + 2^32 share their low 32 bits: the merge must treat them as separate chunks.
        var set = new CompressedLongSet(new long[] { 5, 6, 5 + (1L << 32) });
        var other = new CompressedLongSet(new long[] { 5 + (1L << 32), 6 + (1L << 32) });

        Assert.False(set.SetEquals(other));
        Assert.Equal(1L, set.IntersectCount(other));

        set.IntersectWith(other);

        Assert.Equal(new[] { 5 + (1L << 32) }, set);
    }

    [Fact]
    public void SymmetricExceptWith_ShouldDropAChunkTheTwoSidesShareEntirely()
    {
        var set = new CompressedLongSet(At(1, 500_000));
        var other = new CompressedLongSet(At(500_000, 900_000));

        set.SymmetricExceptWith(other);

        Assert.Equal(At(1, 900_000), set);
    }

    // The four cases below are each one arm of a chunk-index merge that the randomized differential
    // test reaches only by luck. Pinning them deterministically matters twice over: a merge arm that
    // silently stopped running would still look correct on most inputs, and the repo gates on 100%
    // branch coverage, which a property test cannot be relied on to hold up.

    [Fact]
    public void UnionWith_ShouldKeepTrailingChunks_WhenTheRightSideRunsOutFirst()
    {
        var set = new CompressedLongSet(At(1, 900_000));

        set.UnionWith(new CompressedLongSet(At(2)));

        Assert.Equal(At(1, 2, 900_000), set);
    }

    [Fact]
    public void SymmetricExceptWith_ShouldKeepTrailingChunks_WhenTheRightSideRunsOutFirst()
    {
        var set = new CompressedLongSet(At(1, 900_000));

        set.SymmetricExceptWith(new CompressedLongSet(At(2)));

        Assert.Equal(At(1, 2, 900_000), set);
    }

    [Fact]
    public void IsSubsetOf_ShouldReturnFalse_WhenAChunkHasMoreElementsThanItsCounterpart()
    {
        // Four elements against five, so the whole-set cardinality check passes and the per-chunk
        // one has to do the work: the left set's first chunk holds three where the right holds two.
        var set = new CompressedLongSet(At(1, 2, 3, 900_000));
        var other = new CompressedLongSet(At(1, 2, 900_000, 900_001, 900_002));

        Assert.False(set.IsSubsetOf(other));
        Assert.False(set.SetEquals(other));
    }

    [Fact]
    public void IsSubsetOf_ShouldReturnFalse_WhenTheRightSideHasNoChunkAtAll()
    {
        // Two elements against three, so the whole-set check passes; the left set's second chunk has
        // no counterpart on the right at all, which is a different rejection from the one above.
        var set = new CompressedLongSet(At(1, 900_000));
        var other = new CompressedLongSet(At(1, 2, 3));

        Assert.False(set.IsSubsetOf(other));
    }

    [Fact]
    public void Overlaps_ShouldSkipChunksNeitherSideShares_InBothDirections()
    {
        var set = new CompressedLongSet(At(1, 900_000));

        // Left chunk below the right's: the left index advances.
        Assert.True(set.Overlaps(new CompressedLongSet(At(900_000))));
        Assert.False(set.Overlaps(new CompressedLongSet(At(900_001))));

        // Right chunk below the left's: the right index advances.
        var high = new CompressedLongSet(At(900_000));
        Assert.True(high.Overlaps(new CompressedLongSet(At(1, 900_000))));
        Assert.False(high.Overlaps(new CompressedLongSet(At(1, 2))));
    }

    [Fact]
    public void SymmetricExceptWith_ShouldAdoptAChunkFromTheRightSide_MidMerge()
    {
        // The right side's 500_000 chunk sits between the left side's two, so it is adopted while
        // both indices are still live — not by the trailing loop that drains one side at the end.
        var set = new CompressedLongSet(At(1, 900_000));

        set.SymmetricExceptWith(new CompressedLongSet(At(500_000, 900_000)));

        Assert.Equal(At(1, 500_000), set);
    }

    [Fact]
    public void IntersectCount_ShouldSkipChunksTheOtherSideDoesNotHave()
    {
        var set = new CompressedLongSet(At(1, 500_000, 900_000));
        var other = new CompressedLongSet(At(2, 500_000, 1_500_000));

        Assert.Equal(1L, set.IntersectCount(other));
        Assert.Equal(1L, other.IntersectCount(set));
    }

    [Fact]
    public void IntersectCount_ShouldReturnTheCardinality_WhenBothSidesAreTheSameInstance()
    {
        var set = new CompressedLongSet(At(1, 2, 3));

        Assert.Equal(3L, set.IntersectCount(set));
    }

    [Fact]
    public void IntersectCount_ShouldPopcountWordwise_WhenBothChunksAreBitmaps()
    {
        var set = new CompressedLongSet(RangeAt(0, 6000));
        var other = new CompressedLongSet(RangeAt(3000, 6000));

        Assert.Equal(3000L, set.IntersectCount(other));
    }

    [Fact]
    public void IntersectCount_ShouldThrowArgumentNullException_WhenTheOtherSetIsNull()
    {
        var set = new CompressedLongSet();

        Assert.Throws<ArgumentNullException>(() => set.IntersectCount(null!));
    }

    [Fact]
    public void Overlaps_ShouldCompareWordwise_WhenBothChunksAreBitmaps()
    {
        var dense = new CompressedLongSet(Enumerable.Range(0, 5000).Select(i => Base + (i * 2)));
        var evens = new CompressedLongSet(Enumerable.Range(0, 5000).Select(i => Base + (i * 2)));
        var odds = new CompressedLongSet(Enumerable.Range(0, 5000).Select(i => Base + (i * 2) + 1));

        Assert.True(dense.Overlaps(evens));
        Assert.False(dense.Overlaps(odds));
    }

    // ---- self-aliasing --------------------------------------------------------------------

    [Fact]
    public void MutatingOperations_ShouldMatchHashSet_WhenTheOtherSideIsTheSetItself()
    {
        long[] values = At(1, 2, 900_000);

        var union = new CompressedLongSet(values);
        union.UnionWith(union);
        Assert.Equal(values, union);

        var intersect = new CompressedLongSet(values);
        intersect.IntersectWith(intersect);
        Assert.Equal(values, intersect);

        var except = new CompressedLongSet(values);
        except.ExceptWith(except);
        Assert.Empty(except);

        var symmetric = new CompressedLongSet(values);
        symmetric.SymmetricExceptWith(symmetric);
        Assert.Empty(symmetric);
    }

    [Fact]
    public void Queries_ShouldMatchHashSet_WhenTheOtherSideIsTheSetItself()
    {
        var set = new CompressedLongSet(At(1, 2, 3));

        Assert.True(set.SetEquals(set));
        Assert.True(set.IsSubsetOf(set));
        Assert.True(set.IsSupersetOf(set));
        Assert.False(set.IsProperSubsetOf(set));
        Assert.False(set.IsProperSupersetOf(set));
        Assert.True(set.Overlaps(set));
    }

    [Fact]
    public void MutatingOperations_ShouldBeNoOps_WhenTheOtherSideIsAnEmptyCompressedLongSet()
    {
        long[] values = At(1, 2, 900_000);
        var empty = new CompressedLongSet();

        var union = new CompressedLongSet(values);
        union.UnionWith(empty);
        Assert.Equal(values, union);

        var symmetric = new CompressedLongSet(values);
        symmetric.SymmetricExceptWith(empty);
        Assert.Equal(values, symmetric);

        var except = new CompressedLongSet(values);
        except.ExceptWith(empty);
        Assert.Equal(values, except);

        var intersect = new CompressedLongSet(values);
        intersect.IntersectWith(empty);
        Assert.Empty(intersect);
    }

    [Fact]
    public void MutatingOperations_ShouldBeNoOps_WhenTheSetItselfIsEmpty()
    {
        var other = new CompressedLongSet(At(1, 2));

        var intersect = new CompressedLongSet();
        intersect.IntersectWith(other);
        Assert.Empty(intersect);

        var except = new CompressedLongSet();
        except.ExceptWith(other);
        Assert.Empty(except);

        var symmetric = new CompressedLongSet();
        symmetric.SymmetricExceptWith(other);
        Assert.Equal(At(1, 2), symmetric);

        Assert.False(new CompressedLongSet().Overlaps(other));
    }

    [Fact]
    public void MutatingOperations_ShouldLeaveALiveEnumeratorValid_OnlyWhenNothingChanged()
    {
        // Each chunk-wise operation bumps the version exactly when its result differs; pin both arms
        // for every one so a stale or spurious bump cannot hide behind the matrix's other asserts.
        long[] values = At(1, 2, 900_000);

        AssertVersionArm(values, s => s.UnionWith(new CompressedLongSet(At(1))), expectInvalidated: false);
        AssertVersionArm(values, s => s.UnionWith(new CompressedLongSet(At(3))), expectInvalidated: true);
        AssertVersionArm(values, s => s.IntersectWith(new CompressedLongSet(At(1, 2, 900_000, 7))), expectInvalidated: false);
        AssertVersionArm(values, s => s.IntersectWith(new CompressedLongSet(At(1))), expectInvalidated: true);
        AssertVersionArm(values, s => s.ExceptWith(new CompressedLongSet(At(7))), expectInvalidated: false);
        AssertVersionArm(values, s => s.ExceptWith(new CompressedLongSet(At(1))), expectInvalidated: true);
        AssertVersionArm(values, s => s.SymmetricExceptWith(new CompressedLongSet()), expectInvalidated: false);
        AssertVersionArm(values, s => s.SymmetricExceptWith(new CompressedLongSet(At(7))), expectInvalidated: true);
    }

    private static void AssertVersionArm(long[] values, Action<CompressedLongSet> mutate, bool expectInvalidated)
    {
        var set = new CompressedLongSet(values);
        using IEnumerator<long> live = ((IEnumerable<long>)set).GetEnumerator();

        mutate(set);

        if (expectInvalidated)
        {
            Assert.Throws<InvalidOperationException>(() => live.MoveNext());
        }
        else
        {
            Assert.True(live.MoveNext());
            Assert.Equal(values[0], live.Current);
        }
    }

    // ---- the IEnumerable fallback ----------------------------------------------------------

    [Fact]
    public void MutatingOperations_ShouldMatchHashSet_WhenTheOtherSideIsAPlainSequence()
    {
        long[] values = At(1, 2, 3, 900_000);
        long[] other = { Base + 2, Base + 3, Base + 4, -Base - 7 };

        var union = new CompressedLongSet(values);
        union.UnionWith(other.ToList());
        Assert.Equal(new[] { -Base - 7, Base + 1, Base + 2, Base + 3, Base + 4, Base + 900_000 }, union);

        var intersect = new CompressedLongSet(values);
        intersect.IntersectWith(other.ToList());
        Assert.Equal(At(2, 3), intersect);

        var except = new CompressedLongSet(values);
        except.ExceptWith(other.ToList());
        Assert.Equal(At(1, 900_000), except);

        var symmetric = new CompressedLongSet(values);
        symmetric.SymmetricExceptWith(other.ToList());
        Assert.Equal(new[] { -Base - 7, Base + 1, Base + 4, Base + 900_000 }, symmetric);
    }

    [Fact]
    public void Queries_ShouldMatchHashSet_WhenTheOtherSideIsAPlainSequence()
    {
        var set = new CompressedLongSet(At(1, 2, 3));
        var oracle = new HashSet<long>(At(1, 2, 3));

        foreach (long[] other in new[]
                 {
                     At(1, 2, 3),
                     At(1, 2, 3, 4),
                     At(1, 2),
                     At(7, 8),
                     new long[] { 1, 2, 3 }, // same low bits, different chunk
                     Array.Empty<long>(),
                 })
        {
            List<long> sequence = other.ToList();
            Assert.Equal(oracle.SetEquals(sequence), set.SetEquals(sequence));
            Assert.Equal(oracle.IsSubsetOf(sequence), set.IsSubsetOf(sequence));
            Assert.Equal(oracle.IsSupersetOf(sequence), set.IsSupersetOf(sequence));
            Assert.Equal(oracle.IsProperSubsetOf(sequence), set.IsProperSubsetOf(sequence));
            Assert.Equal(oracle.IsProperSupersetOf(sequence), set.IsProperSupersetOf(sequence));
            Assert.Equal(oracle.Overlaps(sequence), set.Overlaps(sequence));
        }
    }

    [Fact]
    public void SetAlgebra_ShouldStillAnswer_WhenTheSetHoldsMoreElementsThanAnInt32()
    {
        // Count throws past int.MaxValue by design, so any fallback routed through the shared
        // SetOperations helper — which compares ICollection<T>.Count — would throw here instead of
        // answering. Every one of these is computable from the long Cardinality, and must be.
        // 2^31 + 6 values above 2^40, one run chunk per 65,536 values (~32k chunks).
        var set = new CompressedLongSet();
        set.AddRange(Base, Base + (1L << 31) + 5);
        Assert.Throws<OverflowException>(() => set.Count);

        var sequence = new List<long> { Base + 5, Base + 7 };

        Assert.True(set.Overlaps(sequence));
        Assert.False(set.SetEquals(sequence));
        Assert.False(set.IsSubsetOf(sequence));
        Assert.False(set.IsProperSubsetOf(sequence));
        Assert.True(set.IsSupersetOf(sequence));
        Assert.True(set.IsProperSupersetOf(sequence));

        // CopyTo cannot succeed here — no long[] is long enough — but it must say so in the order
        // HashSet<long>.CopyTo does, rather than surfacing the Count overflow ahead of the arguments.
        Assert.Throws<ArgumentNullException>(() => set.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.CopyTo(new long[4], -1));
        Assert.Throws<ArgumentException>(() => set.CopyTo(new long[4], 0));

        // And the one mutating fallback that would have had to snapshot the whole set to work.
        set.IntersectWith(sequence);

        Assert.Equal(new[] { Base + 5, Base + 7 }, set);
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void IntersectWith_ShouldNotBumpTheVersion_WhenAPlainSequenceRemovesNothing()
    {
        var set = new CompressedLongSet(At(1, 2, 3));

        using IEnumerator<long> live = ((IEnumerable<long>)set).GetEnumerator();
        set.IntersectWith(new List<long> { Base + 1, Base + 2, Base + 3, Base + 4 });

        Assert.Equal(At(1, 2, 3), set);
        Assert.True(live.MoveNext());
        Assert.Equal(Base + 1, live.Current);
    }

    [Fact]
    public void ExceptWith_ShouldBeANoOp_WhenTheSetIsEmptyAndTheOtherSideIsAPlainSequence()
    {
        var set = new CompressedLongSet();

        set.ExceptWith(new List<long> { Base + 1, Base + 2 });

        Assert.Empty(set);
    }

    [Fact]
    public void IsSubsetOf_ShouldReturnTrueWithoutEnumeratingOther_WhenTheSetIsEmpty()
    {
        // The empty set is a subset of everything, so the rest of the set family answers without
        // touching `other`. A lazy or throwing sequence must behave the same way here.
        var set = new CompressedLongSet();

        Assert.True(set.IsSubsetOf(ThrowingSequence()));
        Assert.True(new LongSet().IsSubsetOf(ThrowingSequence()));
    }

    [Fact]
    public void IsProperSupersetOf_ShouldAnswerWithoutEnumeratingOther_WhenOtherIsAnEmptyCollection()
    {
        // An empty ICollection<long> answers from its Count alone, matching the shared family helper.
        Assert.True(new CompressedLongSet(At(1)).IsProperSupersetOf(new EmptyUnenumerableCollection()));
        Assert.False(new CompressedLongSet().IsProperSupersetOf(new EmptyUnenumerableCollection()));

        // A plain (non-ICollection) sequence still takes the materializing path.
        Assert.True(new CompressedLongSet(At(1, 2)).IsProperSupersetOf(Enumerable.Range(1, 1).Select(x => Base + x)));
        Assert.False(new CompressedLongSet(At(1)).IsProperSupersetOf(Enumerable.Range(1, 1).Select(x => Base + x)));
    }

    private static IEnumerable<long> ThrowingSequence()
    {
        throw new InvalidOperationException("other must not be enumerated");
#pragma warning disable CS0162 // Unreachable code: the yield only makes this an iterator.
        yield break;
#pragma warning restore CS0162
    }

    private sealed class EmptyUnenumerableCollection : ICollection<long>
    {
        public int Count => 0;
        public bool IsReadOnly => true;
        public void Add(long item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Contains(long item) => throw new NotSupportedException();
        public void CopyTo(long[] array, int arrayIndex) => throw new NotSupportedException();
        public bool Remove(long item) => throw new NotSupportedException();
        public IEnumerator<long> GetEnumerator() => throw new InvalidOperationException("other must not be enumerated");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // ---- argument validation ----------------------------------------------------------------

    [Fact]
    public void SetAlgebra_ShouldThrowArgumentNullException_WhenTheOtherSideIsNull()
    {
        var set = new CompressedLongSet(At(1));

        Assert.Throws<ArgumentNullException>(() => set.UnionWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.IntersectWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.ExceptWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.SymmetricExceptWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsSubsetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsProperSubsetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsSupersetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsProperSupersetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.Overlaps(null!));
        Assert.Throws<ArgumentNullException>(() => set.SetEquals(null!));
    }
}
