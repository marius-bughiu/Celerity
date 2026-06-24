using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Deterministic, seeded differential coverage for the probabilistic types' <c>UnionWith</c>
/// merge — the least-exercised correctness path, which touches the raw bit/counter/register
/// state directly rather than going through <c>Add</c>. Each test drives two independently
/// seeded random streams into two equally-parameterised filters, merges them, and asserts the
/// type's defining invariant survives the merge: a merged filter must report everything either
/// operand held, exactly as a single filter fed both streams would. The nightly
/// <c>Celerity.Fuzz</c> soak runs the same cases at scale; these fixed-seed mirrors run on every
/// CI build so a merge regression is caught before it reaches the soak.
/// </summary>
public class SketchMergeDifferentialTests
{
    private const int MinKey = -8;
    private const int MaxKey = 24;
    private const int Seeds = 200;

    private static int Key(Random rng) => rng.Next(MinKey, MaxKey + 1);
    private static int OpCount(Random rng) => rng.Next(0, 200);

    // Bloom: merge is a bitwise OR, so the no-false-negative guarantee extends to the union and
    // the merged Count is the exact sum of both insertion counters.
    [Fact]
    public void BloomFilter_UnionWith_KeepsEveryElementAndSumsCount()
    {
        for (int seed = 0; seed < Seeds; seed++)
        {
            var rng = new Random(seed);
            var a = new BloomFilter<int, Int32WangNaiveHasher>(256);
            var b = new BloomFilter<int, Int32WangNaiveHasher>(256);
            var inA = new HashSet<int>();
            var inB = new HashSet<int>();

            int opsA = OpCount(rng);
            for (int i = 0; i < opsA; i++) { int k = Key(rng); a.Add(k); inA.Add(k); }
            int opsB = OpCount(rng);
            for (int i = 0; i < opsB; i++) { int k = Key(rng); b.Add(k); inB.Add(k); }

            long expectedCount = a.Count + (long)b.Count;
            a.UnionWith(b);

            foreach (int k in inA)
                Assert.True(a.Contains(k), $"seed {seed}: false negative for {k} (from A) after merge");
            foreach (int k in inB)
                Assert.True(a.Contains(k), $"seed {seed}: false negative for {k} (from B) after merge");
            Assert.Equal(expectedCount, a.Count);
        }
    }

    // Cuckoo: merge re-homes every stored fingerprint. The destination's own elements are never
    // lost even when the merge overflows part-way (it throws); the source's elements are
    // guaranteed present only on a merge that ran to completion.
    [Fact]
    public void CuckooFilter_UnionWith_KeepsDestinationAlwaysAndSourceWhenCompleted()
    {
        for (int seed = 0; seed < Seeds; seed++)
        {
            var rng = new Random(seed);
            var a = new CuckooFilter<int, Int32WangNaiveHasher>(512);
            var b = new CuckooFilter<int, Int32WangNaiveHasher>(512);
            var inA = new HashSet<int>();
            var inB = new HashSet<int>();

            int opsA = OpCount(rng);
            for (int i = 0; i < opsA; i++) { int k = Key(rng); if (a.TryAdd(k)) inA.Add(k); }
            int opsB = OpCount(rng);
            for (int i = 0; i < opsB; i++) { int k = Key(rng); if (b.TryAdd(k)) inB.Add(k); }

            bool completed;
            try { a.UnionWith(b); completed = true; }
            catch (InvalidOperationException) { completed = false; }

            foreach (int k in inA)
                Assert.True(a.Contains(k), $"seed {seed}: false negative for {k} (from A) after merge");
            if (completed)
                foreach (int k in inB)
                    Assert.True(a.Contains(k), $"seed {seed}: false negative for {k} (from B) after merge");
        }
    }

    // HyperLogLog: merge takes the per-register maximum, so the merged estimate sits within the
    // same small linear-counting slack of the exact union cardinality as a single estimator. The
    // tiny key domain (<= 33 distinct values) keeps the union deep in the linear-counting regime;
    // a collision-free hasher keeps the estimate exact apart from rare register collisions.
    [Fact]
    public void HyperLogLog_UnionWith_EstimatesUnionWithinSlack()
    {
        for (int seed = 0; seed < Seeds; seed++)
        {
            var rng = new Random(seed);
            var a = new HyperLogLog<int, Int32Murmur3Hasher>();
            var b = new HyperLogLog<int, Int32Murmur3Hasher>();
            var union = new HashSet<int>();

            int opsA = OpCount(rng);
            for (int i = 0; i < opsA; i++) { int k = Key(rng); a.Add(k); union.Add(k); }
            int opsB = OpCount(rng);
            for (int i = 0; i < opsB; i++) { int k = Key(rng); b.Add(k); union.Add(k); }

            a.UnionWith(b);

            long estimate = a.EstimateCardinality();
            int exact = union.Count;
            Assert.True(estimate >= exact - 3 && estimate <= exact + 1,
                $"seed {seed}: merged cardinality estimate {estimate} not within slack of exact {exact}");
        }
    }

    // Count-Min: merge adds counters elementwise, so the never-underestimate guarantee extends to
    // the combined stream and the total count is exactly the sum of both operands' totals.
    [Fact]
    public void CountMinSketch_UnionWith_NeverUnderestimatesAndSumsTotal()
    {
        for (int seed = 0; seed < Seeds; seed++)
        {
            var rng = new Random(seed);
            var a = new CountMinSketch<int, Int32WangNaiveHasher>();
            var b = new CountMinSketch<int, Int32WangNaiveHasher>();
            var oracle = new Dictionary<int, long>();
            long totalA = 0, totalB = 0;

            int opsA = OpCount(rng);
            for (int i = 0; i < opsA; i++)
            {
                int k = Key(rng);
                long w = rng.Next(1, 10);
                a.Add(k, w);
                oracle[k] = oracle.GetValueOrDefault(k) + w;
                totalA += w;
            }
            int opsB = OpCount(rng);
            for (int i = 0; i < opsB; i++)
            {
                int k = Key(rng);
                long w = rng.Next(1, 10);
                b.Add(k, w);
                oracle[k] = oracle.GetValueOrDefault(k) + w;
                totalB += w;
            }

            a.UnionWith(b);

            foreach (var (k, count) in oracle)
                Assert.True(a.EstimateCount(k) >= count,
                    $"seed {seed}: underestimate for {k} after merge: {a.EstimateCount(k)} < {count}");
            Assert.Equal(totalA + totalB, a.TotalCount);
        }
    }
}
