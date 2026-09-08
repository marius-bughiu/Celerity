using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="PersistentVector{T}"/> against a <see cref="List{T}"/> oracle
/// driven through the same operation sequence.
///
/// <para>
/// <b>Persistence is the thing on trial, not the last answer.</b> A vector that path-copies one node too few
/// still agrees with the oracle on the vector you are holding — the damage lands on a vector handed out
/// <i>earlier</i>, whose storage the newer one was supposed to share read-only and instead wrote through. A
/// suite that only checks the current value cannot see that. So every case keeps <b>snapshots</b>: a vector
/// and an independent copy of the oracle taken at intervals through the run, all re-checked element by
/// element after the last operation. A missing clone in <c>PushTail</c>, <c>PopTail</c> or <c>DoAssoc</c>
/// fails there and nowhere else.
/// </para>
///
/// <para>
/// <b>The operation count is a real axis, because the trie changes shape at powers of 32.</b> The tail absorbs
/// the first 32 appends without touching the trie at all; the root gains its first real child at 33; the trie
/// fills at 1024 and the root grows a level at 1057, which is the branch that re-parents the old root and
/// hangs the pushed leaf off a freshly built path. <c>RemoveLast</c> runs the same boundaries backwards and
/// adds one of its own — collapsing a level when the root is left with a single child — and it is the only
/// operation that adopts a trie leaf as the new tail by reference. The generated range spans the first two of
/// those thresholds in both directions; the sweep at the end pins each exact boundary rather than hoping a
/// sample lands on it, and one deterministic case runs past 32,768 so the third level is built and collapsed
/// too.
/// </para>
///
/// <para>
/// The <b>op mix</b> is generated rather than fixed. An append-only run never exercises <c>PopTail</c>, and a
/// run that pops as often as it appends hovers near a single boundary and rarely gets deep; sampling the mix
/// covers both, and the removal weight is what decides how often the level-collapse branch is reached.
/// </para>
/// </summary>
public class PersistentVectorDifferentialTests
{
    // Op count, removal weight (percent of ops that pop), update weight (percent that SetItem), and a seed.
    // The op count spans the 32-element tail boundary and the 1024/1057 root-growth boundary in both
    // directions; the two weights decide which of PushTail / PopTail / DoAssoc the run leans on.
    private static readonly Gen<(int Ops, int RemoveWeight, int UpdateWeight, uint Seed)> GenRuns =
        Gen.Select(Gen.Int[0, 1400], Gen.Int[0, 45], Gen.Int[0, 40], Gen.UInt);

    [Fact]
    public void EveryOperation_ShouldMatchTheListOracle_UnderGeneratedRuns()
    {
        GenRuns.Sample(
            run => AssertAgreesWithOracle(run.Ops, run.RemoveWeight, run.UpdateWeight, run.Seed),
            iter: 200);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(1056)]
    [InlineData(1057)]
    [InlineData(1058)]
    [InlineData(2048)]
    public void AppendThenDrain_ShouldMatchTheListOracle_AtEveryTrieBoundary(int length)
    {
        PersistentVector<int> vector = PersistentVector<int>.Empty;
        var snapshots = new List<(PersistentVector<int> Vector, int[] Oracle)>();

        for (int i = 0; i < length; i++)
        {
            vector = vector.Add(i);
            if (i % 8 == 0 || i >= length - 2)
                snapshots.Add((vector, Enumerable.Range(0, i + 1).ToArray()));
        }

        // Draining takes PopTail through every boundary the appends took PushTail through, in reverse, and is
        // the only path that promotes a trie leaf to the tail.
        for (int i = length - 1; i >= 0; i--)
        {
            vector = vector.RemoveLast();
            Assert.Equal(i, vector.Count);
            if (i % 8 == 0 || i <= 1)
                snapshots.Add((vector, Enumerable.Range(0, i).ToArray()));
        }

        Assert.Same(PersistentVector<int>.Empty, vector);

        foreach ((PersistentVector<int> snapshot, int[] oracle) in snapshots)
            AssertSameSequence(oracle, snapshot);
    }

    [Fact]
    public void AppendThenDrain_ShouldMatchTheListOracle_PastTheThirdTrieLevel()
    {
        // 32,769 elements forces a third level: the trie addresses 32,768 at shift 10, so the root grows again
        // just past it, and the drain collapses that level back. Snapshots are taken sparsely here because the
        // point is the depth, not the density of the checks.
        const int Length = 40_000;

        PersistentVector<int> vector = PersistentVector<int>.Empty;
        var snapshots = new List<(PersistentVector<int> Vector, int Length)>();

        for (int i = 0; i < Length; i++)
        {
            vector = vector.Add(i);
            if (i % 2_000 == 0)
                snapshots.Add((vector, i + 1));
        }

        PersistentVector<int> full = vector;

        for (int i = Length - 1; i >= 0; i--)
        {
            vector = vector.RemoveLast();
            if (i % 2_000 == 0)
                snapshots.Add((vector, i));
        }

        Assert.Same(PersistentVector<int>.Empty, vector);
        AssertSameSequence(Enumerable.Range(0, Length).ToArray(), full);

        foreach ((PersistentVector<int> snapshot, int length) in snapshots)
            AssertSameSequence(Enumerable.Range(0, length).ToArray(), snapshot);
    }

    [Fact]
    public void SetItem_ShouldLeaveEveryEarlierVectorUntouched_AcrossTheWholeIndexRange()
    {
        // Every index of a two-level vector is rewritten in turn, each time against the *original*, so the
        // whole root-to-leaf copy path is exercised at every position and the original is asked to prove it
        // survived all 1,100 of them.
        const int Length = 1_100;

        PersistentVector<int> original = new(Enumerable.Range(0, Length));

        int[] untouched = Enumerable.Range(0, Length).ToArray();

        for (int index = 0; index < Length; index++)
        {
            PersistentVector<int> updated = original.SetItem(index, -index - 1);

            int[] expected = (int[])untouched.Clone();
            expected[index] = -index - 1;

            AssertSameSequence(expected, updated);
            AssertSameSequence(untouched, original);
        }
    }

    private static void AssertAgreesWithOracle(int ops, int removeWeight, int updateWeight, uint seed)
    {
        var rand = new Random(unchecked((int)seed));
        PersistentVector<int> vector = PersistentVector<int>.Empty;
        var oracle = new List<int>();
        var snapshots = new List<(PersistentVector<int> Vector, int[] Oracle)>();

        for (int i = 0; i < ops; i++)
        {
            int roll = rand.Next(0, 100);

            if (roll < removeWeight && oracle.Count > 0)
            {
                vector = vector.RemoveLast();
                oracle.RemoveAt(oracle.Count - 1);
            }
            else if (roll < removeWeight + updateWeight && oracle.Count > 0)
            {
                int index = rand.Next(0, oracle.Count);
                int value = rand.Next();
                vector = vector.SetItem(index, value);
                oracle[index] = value;
            }
            else
            {
                int value = rand.Next();
                vector = vector.Add(value);
                oracle.Add(value);
            }

            Assert.Equal(oracle.Count, vector.Count);
            Assert.Equal(oracle.Count == 0, vector.IsEmpty);

            // Snapshot roughly every 64th operation, so a run spans both sides of a boundary with copies taken
            // on either side of it.
            if (i % 64 == 0)
                snapshots.Add((vector, oracle.ToArray()));
        }

        AssertSameSequence(oracle.ToArray(), vector);

        foreach ((PersistentVector<int> snapshot, int[] expected) in snapshots)
            AssertSameSequence(expected, snapshot);
    }

    // Reconciles every read surface at once: the indexer descends the trie per element, the enumerator walks
    // leaf arrays, and CopyTo bulk-copies them — three different traversals that must agree.
    private static void AssertSameSequence(int[] expected, PersistentVector<int> actual)
    {
        Assert.Equal(expected.Length, actual.Count);

        // Read once per surface into an array and compare the arrays, rather than asserting per element: the
        // suites below take hundreds of thousands of snapshots between them, and a per-element assertion turns
        // that into minutes of xunit bookkeeping for the same evidence.
        var byIndexer = new int[actual.Count];
        for (int i = 0; i < byIndexer.Length; i++)
            byIndexer[i] = actual[i];

        Assert.Equal(expected, byIndexer);
        Assert.Equal(expected, actual.ToArray());
        Assert.Equal(expected, actual.ToList());
    }
}
