using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Set-algebra coverage for <see cref="CompressedIntSet"/>, driven across the <b>container-pair
/// matrix</b>: every binary operation is run for all nine combinations of the sorted-array, bitmap
/// and run-length forms on the two operands, in both operand orders, against a
/// <see cref="HashSet{T}"/> oracle.
///
/// <para>
/// That matrix is the point. Internally the type does not carry nine hand-written implementations —
/// each operation has a word-parallel path for the dense bitmap&#8596;bitmap case and one
/// cursor-driven path for everything else, which is what keeps a run container from needing its own
/// case in every operator and from being decompressed just because it was read. These tests pin the
/// *observable* contract for all nine pairs regardless, so a future specialization of any pair
/// cannot silently change an answer.
/// </para>
///
/// <para>
/// The chunk-index merge (both operands' chunk lists walked in key order) and the
/// <see cref="IEnumerable{T}"/> fallback — taken whenever the other side is not a
/// <see cref="CompressedIntSet"/> — are covered separately at the bottom.
/// </para>
/// </summary>
public class CompressedIntSetSetAlgebraTests
{
    private const int ArrayToBitmapThreshold = 4096;
    private const int BitmapPayloadBytes = 8192;

    /// <summary>How a test wants a single-chunk operand physically stored.</summary>
    public enum Form
    {
        Array,
        Bitmap,
        Run,
    }

    // Two clustered, unequal-sized operands inside chunk 0. Clustered so the run form is genuinely
    // smaller than the alternatives (otherwise Optimize would decline it), unequal so the
    // "walk the smaller side" branch of the intersect/overlap paths is taken in both directions.
    private static int[] LeftValues => Enumerable.Range(100, 300)
        .Concat(Enumerable.Range(1000, 100))
        .Append(5000)
        .ToArray();

    private static int[] RightValues => Enumerable.Range(300, 250)
        .Concat(Enumerable.Range(1050, 50))
        .Append(7000)
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

    private static CompressedIntSet Build(Form form, IEnumerable<int> values)
    {
        var set = new CompressedIntSet(values);

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
                var filler = new List<int>();
                for (int v = 0; filler.Count <= ArrayToBitmapThreshold; v++)
                {
                    if (set.TryAdd(v))
                        filler.Add(v);
                }

                foreach (int v in filler)
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
        Action<CompressedIntSet, CompressedIntSet> apply,
        Action<HashSet<int>, HashSet<int>> applyToOracle)
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
        Action<CompressedIntSet, CompressedIntSet, HashSet<int>, HashSet<int>> body)
    {
        body(Build(left, LeftValues), Build(right, RightValues), new HashSet<int>(LeftValues), new HashSet<int>(RightValues));
        body(Build(right, RightValues), Build(left, LeftValues), new HashSet<int>(RightValues), new HashSet<int>(LeftValues));
    }

    // ---- results that outgrow the array form ---------------------------------------------

    [Fact]
    public void ExceptWith_ShouldProduceABitmapResult_WhenTheRemainderIsStillDense()
    {
        // A run-encoded left operand against a tiny array right operand: neither side is a bitmap,
        // so the cursor path builds the result — and the result is far too big for an array.
        var set = new CompressedIntSet();
        set.AddRange(0, 9999);
        var other = new CompressedIntSet(new[] { 1, 2, 3 });

        set.ExceptWith(other);

        Assert.Equal(9997, set.Count);
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);
        Assert.False(set.Contains(1));
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(4));

        // A second large-result operation on the same instance reuses the scratch bitmap.
        set.ExceptWith(new CompressedIntSet(new[] { 4, 5 }));
        Assert.Equal(9995, set.Count);
        Assert.False(set.Contains(4));
        Assert.True(set.Contains(6));
    }

    [Fact]
    public void ExceptWith_ShouldProduceABitmapResult_WhenTheLeftOperandIsAlreadyABitmap()
    {
        var set = new CompressedIntSet(Enumerable.Range(0, 5000).Select(i => i * 2));
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);

        set.ExceptWith(new CompressedIntSet(new[] { 0, 2, 4 }));

        Assert.Equal(4997, set.Count);
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);
        Assert.False(set.Contains(0));
        Assert.True(set.Contains(6));
    }

    [Fact]
    public void IntersectWith_ShouldDemoteTheBitmapToAnArray_WhenTheResultBecomesSparse()
    {
        var set = new CompressedIntSet(Enumerable.Range(0, 5000).Select(i => i * 2));
        var other = new CompressedIntSet(Enumerable.Range(0, 5000).Select(i => i * 2));
        other.IntersectWith(new CompressedIntSet(new[] { 0, 2, 4, 6 }));

        set.IntersectWith(other);

        Assert.Equal(new[] { 0, 2, 4, 6 }, set);
        Assert.True(set.MemoryUsageInBytes < BitmapPayloadBytes);
    }

    [Fact]
    public void UnionWith_ShouldMergeTwoBitmapsWordwise_WhenBothChunksAreDense()
    {
        var set = new CompressedIntSet(Enumerable.Range(0, 5000).Select(i => i * 2));
        var other = new CompressedIntSet(Enumerable.Range(0, 5000).Select(i => (i * 2) + 1));

        set.UnionWith(other);

        Assert.Equal(10_000, set.Count);
        Assert.Equal(Enumerable.Range(0, 10_000), set);
    }

    [Fact]
    public void SymmetricExceptWith_ShouldXorTwoBitmapsWordwise_WhenBothChunksAreDense()
    {
        var set = new CompressedIntSet(Enumerable.Range(0, 6000));
        var other = new CompressedIntSet(Enumerable.Range(3000, 6000));

        set.SymmetricExceptWith(other);

        Assert.Equal(Enumerable.Range(0, 3000).Concat(Enumerable.Range(6000, 3000)), set);
    }

    // ---- the chunk-index merge -----------------------------------------------------------

    [Fact]
    public void UnionWith_ShouldAdoptChunksTheLeftSideDoesNotHave()
    {
        var set = new CompressedIntSet(new[] { 1, 300_000 });
        var other = new CompressedIntSet(new[] { -500_000, 2, 900_000 });
        other.TryAdd(-500_001);

        set.UnionWith(other);

        Assert.Equal(new[] { -500_001, -500_000, 1, 2, 300_000, 900_000 }, set);

        // The adopted chunks must be copies: mutating the source afterwards cannot reach into the
        // union's result.
        other.Remove(900_000);
        Assert.True(set.Contains(900_000));
    }

    [Fact]
    public void UnionWith_ShouldCopyABitmapChunk_WhenTheLeftSideHasNothingThere()
    {
        var set = new CompressedIntSet(new[] { 1 });
        var other = new CompressedIntSet(Enumerable.Range(1_000_000, 5000).Select(i => i * 2));

        set.UnionWith(other);

        Assert.Equal(5001, set.Count);
        Assert.True(set.Contains(2_000_000));

        other.Clear();
        Assert.Equal(5001, set.Count);
    }

    [Fact]
    public void ExceptWith_ShouldDropAChunkEmptiedByTheOperation()
    {
        var set = new CompressedIntSet(new[] { 1, 2, 500_000, 900_000 });
        long before = set.MemoryUsageInBytes;

        // 300_000 and 12_345_678 sit in chunks the left side does not have — one before a surviving
        // chunk and one after — so the merge has to skip forward on the right without losing them.
        set.ExceptWith(new CompressedIntSet(new[] { 300_000, 500_000, 12_345_678 }));

        Assert.Equal(new[] { 1, 2, 900_000 }, set);
        Assert.True(set.MemoryUsageInBytes < before);
    }

    [Fact]
    public void IntersectWith_ShouldKeepOnlyTheSharedChunks()
    {
        // 300_000 sits in a chunk the left side does not have, before a chunk it does — so the merge
        // has to skip forward on the right without consuming a left chunk.
        var set = new CompressedIntSet(new[] { 1, 500_000, 900_000 });
        var other = new CompressedIntSet(new[] { 2, 300_000, 500_000, 1_500_000 });

        set.IntersectWith(other);

        Assert.Equal(new[] { 500_000 }, set);
    }

    [Fact]
    public void SymmetricExceptWith_ShouldDropAChunkTheTwoSidesShareEntirely()
    {
        var set = new CompressedIntSet(new[] { 1, 500_000 });
        var other = new CompressedIntSet(new[] { 500_000, 900_000 });

        set.SymmetricExceptWith(other);

        Assert.Equal(new[] { 1, 900_000 }, set);
    }

    // The four cases below are each one arm of a chunk-index merge that the randomized differential
    // test reaches only by luck. Pinning them deterministically matters twice over: a merge arm that
    // silently stopped running would still look correct on most inputs, and the repo gates on 100%
    // branch coverage, which a property test cannot be relied on to hold up.

    [Fact]
    public void UnionWith_ShouldKeepTrailingChunks_WhenTheRightSideRunsOutFirst()
    {
        var set = new CompressedIntSet(new[] { 1, 900_000 });

        set.UnionWith(new CompressedIntSet(new[] { 2 }));

        Assert.Equal(new[] { 1, 2, 900_000 }, set);
    }

    [Fact]
    public void SymmetricExceptWith_ShouldKeepTrailingChunks_WhenTheRightSideRunsOutFirst()
    {
        var set = new CompressedIntSet(new[] { 1, 900_000 });

        set.SymmetricExceptWith(new CompressedIntSet(new[] { 2 }));

        Assert.Equal(new[] { 1, 2, 900_000 }, set);
    }

    [Fact]
    public void IsSubsetOf_ShouldReturnFalse_WhenAChunkHasMoreElementsThanItsCounterpart()
    {
        // Four elements against five, so the whole-set cardinality check passes and the per-chunk
        // one has to do the work: the left set's first chunk holds three where the right holds two.
        var set = new CompressedIntSet(new[] { 1, 2, 3, 900_000 });
        var other = new CompressedIntSet(new[] { 1, 2, 900_000, 900_001, 900_002 });

        Assert.False(set.IsSubsetOf(other));
        Assert.False(set.SetEquals(other));
    }

    [Fact]
    public void IsSubsetOf_ShouldReturnFalse_WhenTheRightSideHasNoChunkAtAll()
    {
        // Two elements against three, so the whole-set check passes; the left set's second chunk has
        // no counterpart on the right at all, which is a different rejection from the one above.
        var set = new CompressedIntSet(new[] { 1, 900_000 });
        var other = new CompressedIntSet(new[] { 1, 2, 3 });

        Assert.False(set.IsSubsetOf(other));
    }

    [Fact]
    public void Overlaps_ShouldSkipChunksNeitherSideShares_InBothDirections()
    {
        var set = new CompressedIntSet(new[] { 1, 900_000 });

        // Left chunk below the right's: the left index advances.
        Assert.True(set.Overlaps(new CompressedIntSet(new[] { 900_000 })));
        Assert.False(set.Overlaps(new CompressedIntSet(new[] { 900_001 })));

        // Right chunk below the left's: the right index advances.
        var high = new CompressedIntSet(new[] { 900_000 });
        Assert.True(high.Overlaps(new CompressedIntSet(new[] { 1, 900_000 })));
        Assert.False(high.Overlaps(new CompressedIntSet(new[] { 1, 2 })));
    }

    [Fact]
    public void SymmetricExceptWith_ShouldAdoptAChunkFromTheRightSide_MidMerge()
    {
        // The right side's 500_000 chunk sits between the left side's two, so it is adopted while
        // both indices are still live — not by the trailing loop that drains one side at the end.
        var set = new CompressedIntSet(new[] { 1, 900_000 });

        set.SymmetricExceptWith(new CompressedIntSet(new[] { 500_000, 900_000 }));

        Assert.Equal(new[] { 1, 500_000 }, set);
    }

    [Fact]
    public void IntersectCount_ShouldSkipChunksTheOtherSideDoesNotHave()
    {
        var set = new CompressedIntSet(new[] { 1, 500_000, 900_000 });
        var other = new CompressedIntSet(new[] { 2, 500_000, 1_500_000 });

        Assert.Equal(1L, set.IntersectCount(other));
        Assert.Equal(1L, other.IntersectCount(set));
    }

    [Fact]
    public void IntersectCount_ShouldReturnTheCardinality_WhenBothSidesAreTheSameInstance()
    {
        var set = new CompressedIntSet(new[] { 1, 2, 3 });

        Assert.Equal(3L, set.IntersectCount(set));
    }

    [Fact]
    public void IntersectCount_ShouldPopcountWordwise_WhenBothChunksAreBitmaps()
    {
        var set = new CompressedIntSet(Enumerable.Range(0, 6000));
        var other = new CompressedIntSet(Enumerable.Range(3000, 6000));

        Assert.Equal(3000L, set.IntersectCount(other));
    }

    [Fact]
    public void IntersectCount_ShouldThrowArgumentNullException_WhenTheOtherSetIsNull()
    {
        var set = new CompressedIntSet();

        Assert.Throws<ArgumentNullException>(() => set.IntersectCount(null!));
    }

    [Fact]
    public void Overlaps_ShouldCompareWordwise_WhenBothChunksAreBitmaps()
    {
        var dense = new CompressedIntSet(Enumerable.Range(0, 5000).Select(i => i * 2));
        var evens = new CompressedIntSet(Enumerable.Range(0, 5000).Select(i => i * 2));
        var odds = new CompressedIntSet(Enumerable.Range(0, 5000).Select(i => (i * 2) + 1));

        Assert.True(dense.Overlaps(evens));
        Assert.False(dense.Overlaps(odds));
    }

    // ---- self-aliasing --------------------------------------------------------------------

    [Fact]
    public void MutatingOperations_ShouldMatchHashSet_WhenTheOtherSideIsTheSetItself()
    {
        int[] values = { 1, 2, 900_000 };

        var union = new CompressedIntSet(values);
        union.UnionWith(union);
        Assert.Equal(values, union);

        var intersect = new CompressedIntSet(values);
        intersect.IntersectWith(intersect);
        Assert.Equal(values, intersect);

        var except = new CompressedIntSet(values);
        except.ExceptWith(except);
        Assert.Empty(except);

        var symmetric = new CompressedIntSet(values);
        symmetric.SymmetricExceptWith(symmetric);
        Assert.Empty(symmetric);
    }

    [Fact]
    public void Queries_ShouldMatchHashSet_WhenTheOtherSideIsTheSetItself()
    {
        var set = new CompressedIntSet(new[] { 1, 2, 3 });

        Assert.True(set.SetEquals(set));
        Assert.True(set.IsSubsetOf(set));
        Assert.True(set.IsSupersetOf(set));
        Assert.False(set.IsProperSubsetOf(set));
        Assert.False(set.IsProperSupersetOf(set));
        Assert.True(set.Overlaps(set));
    }

    [Fact]
    public void MutatingOperations_ShouldBeNoOps_WhenTheOtherSideIsAnEmptyCompressedIntSet()
    {
        int[] values = { 1, 2, 900_000 };
        var empty = new CompressedIntSet();

        var union = new CompressedIntSet(values);
        union.UnionWith(empty);
        Assert.Equal(values, union);

        var symmetric = new CompressedIntSet(values);
        symmetric.SymmetricExceptWith(empty);
        Assert.Equal(values, symmetric);

        var except = new CompressedIntSet(values);
        except.ExceptWith(empty);
        Assert.Equal(values, except);

        var intersect = new CompressedIntSet(values);
        intersect.IntersectWith(empty);
        Assert.Empty(intersect);
    }

    [Fact]
    public void MutatingOperations_ShouldBeNoOps_WhenTheSetItselfIsEmpty()
    {
        var other = new CompressedIntSet(new[] { 1, 2 });

        var intersect = new CompressedIntSet();
        intersect.IntersectWith(other);
        Assert.Empty(intersect);

        var except = new CompressedIntSet();
        except.ExceptWith(other);
        Assert.Empty(except);

        var symmetric = new CompressedIntSet();
        symmetric.SymmetricExceptWith(other);
        Assert.Equal(new[] { 1, 2 }, symmetric);

        Assert.False(new CompressedIntSet().Overlaps(other));
    }

    // ---- the IEnumerable fallback ----------------------------------------------------------

    [Fact]
    public void MutatingOperations_ShouldMatchHashSet_WhenTheOtherSideIsAPlainSequence()
    {
        int[] values = { 1, 2, 3, 900_000 };
        int[] other = { 2, 3, 4, -7 };

        var union = new CompressedIntSet(values);
        union.UnionWith(other.ToList());
        Assert.Equal(new[] { -7, 1, 2, 3, 4, 900_000 }, union);

        var intersect = new CompressedIntSet(values);
        intersect.IntersectWith(other.ToList());
        Assert.Equal(new[] { 2, 3 }, intersect);

        var except = new CompressedIntSet(values);
        except.ExceptWith(other.ToList());
        Assert.Equal(new[] { 1, 900_000 }, except);

        var symmetric = new CompressedIntSet(values);
        symmetric.SymmetricExceptWith(other.ToList());
        Assert.Equal(new[] { -7, 1, 4, 900_000 }, symmetric);
    }

    [Fact]
    public void Queries_ShouldMatchHashSet_WhenTheOtherSideIsAPlainSequence()
    {
        var set = new CompressedIntSet(new[] { 1, 2, 3 });
        var oracle = new HashSet<int>(new[] { 1, 2, 3 });

        foreach (int[] other in new[]
                 {
                     new[] { 1, 2, 3 },
                     new[] { 1, 2, 3, 4 },
                     new[] { 1, 2 },
                     new[] { 7, 8 },
                     Array.Empty<int>(),
                 })
        {
            List<int> sequence = other.ToList();
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
        var set = new CompressedIntSet();
        set.AddRange(int.MinValue, int.MaxValue);
        Assert.Throws<OverflowException>(() => set.Count);

        var sequence = new List<int> { 5, 7 };

        Assert.True(set.Overlaps(sequence));
        Assert.False(set.SetEquals(sequence));
        Assert.False(set.IsSubsetOf(sequence));
        Assert.False(set.IsProperSubsetOf(sequence));
        Assert.True(set.IsSupersetOf(sequence));
        Assert.True(set.IsProperSupersetOf(sequence));

        // CopyTo cannot succeed here — no int[] is long enough — but it must say so in the order
        // HashSet<int>.CopyTo does, rather than surfacing the Count overflow ahead of the arguments.
        Assert.Throws<ArgumentNullException>(() => set.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.CopyTo(new int[4], -1));
        Assert.Throws<ArgumentException>(() => set.CopyTo(new int[4], 0));

        // And the one mutating fallback that would have had to snapshot the whole set to work.
        set.IntersectWith(sequence);

        Assert.Equal(new[] { 5, 7 }, set);
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void IntersectWith_ShouldNotBumpTheVersion_WhenAPlainSequenceRemovesNothing()
    {
        var set = new CompressedIntSet(new[] { 1, 2, 3 });

        using IEnumerator<int> live = ((IEnumerable<int>)set).GetEnumerator();
        set.IntersectWith(new List<int> { 1, 2, 3, 4 });

        Assert.Equal(new[] { 1, 2, 3 }, set);
        Assert.True(live.MoveNext());
        Assert.Equal(1, live.Current);
    }

    [Fact]
    public void ExceptWith_ShouldBeANoOp_WhenTheSetIsEmptyAndTheOtherSideIsAPlainSequence()
    {
        var set = new CompressedIntSet();

        set.ExceptWith(new List<int> { 1, 2 });

        Assert.Empty(set);
    }

    // ---- argument validation ----------------------------------------------------------------

    [Fact]
    public void SetAlgebra_ShouldThrowArgumentNullException_WhenTheOtherSideIsNull()
    {
        var set = new CompressedIntSet(new[] { 1 });

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
