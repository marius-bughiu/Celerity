using System.Collections;
using System.Numerics;
using Celerity.Primitives;
using Celerity.Statistics;

namespace Celerity.Tests.Statistics;

/// <summary>
/// Behavioural tests for <see cref="WeightedReservoirSampler{T, TRng}"/>: the fill phase, the
/// A-ExpJ jump phase, the weighted distribution the type exists to provide (checked against exact
/// successive-sampling probabilities), the merge, zero weights, and the degenerate end of the jump
/// arithmetic that a real generator will not reach on its own.
/// </summary>
public class WeightedReservoirSamplerTests
{
    /// <summary>
    /// A generator that cycles a fixed script of 64-bit words, to pin the jump and the keys at the
    /// extremes of the draw grid.
    /// </summary>
    private struct ScriptedRandom : IRandomSource
    {
        private readonly ulong[] _script;
        private int _position;

        public ScriptedRandom(ulong[] script)
        {
            _script = script;
            _position = 0;
        }

        public ulong NextUInt64()
        {
            ulong value = _script[_position % _script.Length];
            _position++;
            return value;
        }
    }

    [Fact]
    public void Constructor_ShouldStartEmpty()
    {
        var sampler = new WeightedReservoirSampler<int>(capacity: 4, seed: 1UL);

        Assert.Equal(4, sampler.Capacity);
        Assert.Equal(0, sampler.Count);
        Assert.Equal(0, sampler.TotalSeen);
        Assert.Equal(0d, sampler.TotalWeight);
        Assert.False(sampler.IsFull);
        Assert.True(sampler.Sample.IsEmpty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_ShouldThrow_WhenTheCapacityIsNotPositive(int capacity)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new WeightedReservoirSampler<int>(capacity, seed: 1UL));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void Add_ShouldRetainEveryPositiveWeightItem_UntilTheReservoirIsFull()
    {
        var sampler = new WeightedReservoirSampler<int>(capacity: 5, seed: 7UL);

        for (int i = 0; i < 5; i++)
        {
            Assert.True(sampler.Add(i, i + 1d));
        }

        Assert.True(sampler.IsFull);
        Assert.Equal([0, 1, 2, 3, 4], sampler.Sample.ToArray().OrderBy(x => x));
        Assert.Equal(15d, sampler.TotalWeight);
        Assert.Equal(5, sampler.TotalSeen);
    }

    [Fact]
    public void Add_ShouldKeepTheSampleSizeFixed_AndReportRetainedAndDiscardedItems()
    {
        var sampler = new WeightedReservoirSampler<int>(capacity: 10, seed: 3UL);
        int retained = 0;
        int discarded = 0;

        for (int i = 0; i < 10_000; i++)
        {
            int before = sampler.Sample.ToArray().Count(x => x == i);
            Assert.Equal(0, before);

            if (sampler.Add(i, 1d + (i % 7)))
            {
                retained++;
                Assert.Contains(i, sampler.Sample.ToArray());
            }
            else
            {
                discarded++;
                Assert.DoesNotContain(i, sampler.Sample.ToArray());
            }

            Assert.Equal(Math.Min(i + 1, 10), sampler.Count);
        }

        Assert.True(retained > 10, "Some items after the fill must have been retained.");
        Assert.True(discarded > 9_000, "Most items of a long stream must be discarded.");
        Assert.Equal(10, sampler.Sample.ToArray().Distinct().Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Add_ShouldCountAZeroWeight_AndNeverRetainIt(bool negativeZero)
    {
        double zero = negativeZero ? -0d : 0d;
        var sampler = new WeightedReservoirSampler<int>(capacity: 3, seed: 5UL);

        Assert.False(sampler.Add(-1, zero));
        Assert.Equal(1, sampler.TotalSeen);
        Assert.Equal(0, sampler.Count);
        Assert.Equal(0d, sampler.TotalWeight);

        for (int i = 0; i < 1_000; i++)
        {
            sampler.Add(i, 1d);
            Assert.False(sampler.Add(-1, zero));
        }

        Assert.DoesNotContain(-1, sampler.Sample.ToArray());
        Assert.Equal(2_001, sampler.TotalSeen);
        Assert.Equal(1_000d, sampler.TotalWeight);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(-double.Epsilon)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Add_ShouldThrow_AndLeaveTheSamplerUnchanged_WhenTheWeightIsInvalid(double weight)
    {
        var sampler = new WeightedReservoirSampler<int>(capacity: 2, seed: 5UL);
        sampler.Add(1, 1d);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => sampler.Add(2, weight));

        Assert.Equal("weight", ex.ParamName);
        Assert.Equal(1, sampler.TotalSeen);
        Assert.Equal(1d, sampler.TotalWeight);
        Assert.Equal([1], sampler.Sample.ToArray());
    }

    [Fact]
    public void Add_ShouldAcceptTheExtremesOfTheDoubleRange()
    {
        var sampler = new WeightedReservoirSampler<int>(capacity: 4, seed: 11UL);
        double[] weights = [double.Epsilon, 1e-310, 1e-300, 1d, 1e300, double.MaxValue];

        for (int round = 0; round < 50; round++)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                sampler.Add(i, weights[i]);
            }
        }

        Assert.Equal(4, sampler.Count);
        Assert.Equal(300, sampler.TotalSeen);
        Assert.Equal(double.PositiveInfinity, sampler.TotalWeight);

        // The copies of the two heaviest items outweigh the rest by hundreds of orders of magnitude.
        Assert.All(sampler.Sample.ToArray(), item => Assert.True(item >= 4));
    }

    [Fact]
    public void Add_ShouldMatchExactSuccessiveSamplingProbabilities()
    {
        // k = 3 of 8 items: three fill the reservoir and five go through the jump phase.
        double[] weights = [1d, 2d, 3d, 4d, 5d, 6d, 7d, 0.5d];
        double[] exact = ExactInclusionProbabilities(weights, k: 3);
        const int Trials = 40_000;
        var counts = new int[weights.Length];

        for (int t = 0; t < Trials; t++)
        {
            var sampler = new WeightedReservoirSampler<int>(3, (ulong)t * 0x9E3779B97F4A7C15UL);
            for (int i = 0; i < weights.Length; i++)
            {
                sampler.Add(i, weights[i]);
            }

            foreach (int item in sampler.Sample)
            {
                counts[item]++;
            }
        }

        AssertFrequenciesMatch(exact, counts, Trials);
    }

    [Fact]
    public void Add_ShouldPickInProportionToWeight_AtCapacityOne_OverALongStream()
    {
        // At k = 1 the retained item is the first successive draw: P(i) = w_i / W exactly. A stream
        // of 64 items spends almost all of its time in the jump phase.
        double[] weights = Enumerable.Range(0, 64).Select(i => 1d + (i % 9) * (i % 4)).ToArray();
        double total = weights.Sum();
        double[] exact = weights.Select(w => w / total).ToArray();
        const int Trials = 60_000;
        var counts = new int[weights.Length];
        var items = Enumerable.Range(0, weights.Length).ToArray();

        for (int t = 0; t < Trials; t++)
        {
            var sampler = new WeightedReservoirSampler<int, Xoshiro256StarStar>(1, new Xoshiro256StarStar((ulong)t + 1UL));
            sampler.Add(items, weights);
            counts[sampler[0]]++;
        }

        AssertFrequenciesMatch(exact, counts, Trials);
    }

    [Fact]
    public void Merge_ShouldMatchExactSuccessiveSamplingProbabilities_OverTheUnion()
    {
        double[] weights = [1d, 2d, 3d, 4d, 5d, 6d, 7d, 0.5d];
        double[] exact = ExactInclusionProbabilities(weights, k: 3);
        const int Trials = 40_000;
        var counts = new int[weights.Length];

        for (int t = 0; t < Trials; t++)
        {
            var left = new WeightedReservoirSampler<int>(3, (2UL * (ulong)t) + 1UL);
            var right = new WeightedReservoirSampler<int>(3, (2UL * (ulong)t) + 2UL);

            // An uneven split: one shard fills and jumps, the other never fills.
            for (int i = 0; i < weights.Length; i++)
            {
                (i is 2 or 5 ? right : left).Add(i, weights[i]);
            }

            left.Merge(right);
            foreach (int item in left.Sample)
            {
                counts[item]++;
            }
        }

        AssertFrequenciesMatch(exact, counts, Trials);
    }

    [Fact]
    public void Merge_ShouldKeepSamplingCorrectly_AfterTheMerge()
    {
        // A merged sampler goes on to take more of the stream; the redrawn jump must keep the
        // whole thing exact.
        double[] weights = [3d, 1d, 4d, 1d, 5d, 9d, 2d, 6d];
        double[] exact = ExactInclusionProbabilities(weights, k: 2);
        const int Trials = 40_000;
        var counts = new int[weights.Length];

        for (int t = 0; t < Trials; t++)
        {
            var left = new WeightedReservoirSampler<int>(2, (3UL * (ulong)t) + 1UL);
            var right = new WeightedReservoirSampler<int>(2, (3UL * (ulong)t) + 2UL);
            for (int i = 0; i < 3; i++)
            {
                left.Add(i, weights[i]);
            }

            for (int i = 3; i < 6; i++)
            {
                right.Add(i, weights[i]);
            }

            left.Merge(right);
            for (int i = 6; i < weights.Length; i++)
            {
                left.Add(i, weights[i]);
            }

            foreach (int item in left.Sample)
            {
                counts[item]++;
            }
        }

        AssertFrequenciesMatch(exact, counts, Trials);
    }

    [Fact]
    public void Merge_ShouldSumTheCounters_AndLeaveTheOtherSamplerUnchanged()
    {
        var left = new WeightedReservoirSampler<string>(4, 1UL);
        var right = new WeightedReservoirSampler<string>(4, 2UL);
        left.Add("a", 1d);
        left.Add("zero", 0d);
        right.Add("b", 2d);
        right.Add("c", 3d);

        left.Merge(right);

        Assert.Equal(["a", "b", "c"], left.Sample.ToArray().Order());
        Assert.Equal(4, left.TotalSeen);
        Assert.Equal(6d, left.TotalWeight);
        Assert.False(left.IsFull);
        Assert.Equal(["b", "c"], right.Sample.ToArray().Order());
        Assert.Equal(2, right.TotalSeen);
    }

    [Fact]
    public void Merge_ShouldTakeTheOtherSamplersItems_WhenThisOneIsEmpty_AndBeANoOpWhenTheOtherIsEmpty()
    {
        var empty = new WeightedReservoirSampler<int>(3, 1UL);
        var full = new WeightedReservoirSampler<int>(3, 2UL);
        for (int i = 0; i < 100; i++)
        {
            full.Add(i, 1d + i);
        }

        int[] before = full.Sample.ToArray();
        full.Merge(new WeightedReservoirSampler<int>(3, 3UL));
        Assert.Equal(before, full.Sample.ToArray());
        Assert.Equal(100, full.TotalSeen);

        empty.Merge(full);
        Assert.Equal(before.Order(), empty.Sample.ToArray().Order());
        Assert.True(empty.IsFull);
        Assert.Equal(100, empty.TotalSeen);
    }

    [Fact]
    public void Merge_ShouldThrow_ForNull_ForItself_AndForADifferentCapacity()
    {
        var sampler = new WeightedReservoirSampler<int>(3, 1UL);
        sampler.Add(1, 1d);

        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => sampler.Merge(null!)).ParamName);
        Assert.Equal("other", Assert.Throws<ArgumentException>(() => sampler.Merge(sampler)).ParamName);
        Assert.Equal(
            "other",
            Assert.Throws<ArgumentException>(() => sampler.Merge(new WeightedReservoirSampler<int>(4, 2UL))).ParamName);
        Assert.Equal(1, sampler.TotalSeen);
    }

    [Fact]
    public void Add_ShouldProduceTheSameSample_ForTheSameSeedAndStream()
    {
        var first = new WeightedReservoirSampler<int>(8, 99UL);
        var second = new WeightedReservoirSampler<int>(8, 99UL);

        for (int i = 0; i < 5_000; i++)
        {
            first.Add(i, (i % 13) + 0.25d);
            second.Add(i, (i % 13) + 0.25d);
        }

        Assert.Equal(first.Sample.ToArray(), second.Sample.ToArray());
    }

    [Fact]
    public void AddSpan_ShouldMatchPerItemAdds_ForTheSameSeed()
    {
        var items = Enumerable.Range(0, 3_000).ToArray();
        var weights = items.Select(i => i % 5 == 0 ? 0d : (i % 11) + 0.5d).ToArray();
        var perItem = new WeightedReservoirSampler<int>(16, 4UL);
        var batched = new WeightedReservoirSampler<int>(16, 4UL);

        for (int i = 0; i < items.Length; i++)
        {
            perItem.Add(items[i], weights[i]);
        }

        // Three batches: one inside the fill phase, one across its end, one wholly past it.
        batched.Add(items.AsSpan(0, 10), weights.AsSpan(0, 10));
        batched.Add(items.AsSpan(10, 30), weights.AsSpan(10, 30));
        batched.Add(items.AsSpan(40), weights.AsSpan(40));

        Assert.Equal(perItem.Sample.ToArray(), batched.Sample.ToArray());
        Assert.Equal(perItem.TotalSeen, batched.TotalSeen);
        Assert.Equal(perItem.TotalWeight, batched.TotalWeight);
    }

    [Fact]
    public void AddSpan_ShouldThrow_WhenTheLengthsDiffer()
    {
        var sampler = new WeightedReservoirSampler<int>(2, 1UL);

        var ex = Assert.Throws<ArgumentException>(() => sampler.Add([1, 2], [1d]));

        Assert.Equal("weights", ex.ParamName);
        Assert.Equal(0, sampler.TotalSeen);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AddSpan_ShouldThrow_BeforeOfferingAnything_WhenAnyWeightIsInvalid(double bad)
    {
        var sampler = new WeightedReservoirSampler<int>(2, 1UL);

        var ex = Assert.Throws<ArgumentException>(() => sampler.Add([1, 2, 3], [1d, 2d, bad]));

        Assert.Equal("weights", ex.ParamName);
        Assert.Equal(0, sampler.TotalSeen);
        Assert.Equal(0, sampler.Count);
    }

    [Fact]
    public void AddSpan_ShouldBeANoOp_ForEmptySpans()
    {
        var sampler = new WeightedReservoirSampler<int>(2, 1UL);
        sampler.Add(1, 1d);
        IEnumerator<int> enumerator = sampler.GetEnumerator();

        sampler.Add(ReadOnlySpan<int>.Empty, ReadOnlySpan<double>.Empty);

        Assert.Equal(1, sampler.TotalSeen);
        Assert.True(enumerator.MoveNext());
    }

    [Fact]
    public void Add_ShouldNeverRetainAZeroWeight_EvenWhenTheJumpHasRunDownToZero()
    {
        // Word 0 is the smallest draw (2^-53) and gives the filling item a key so large that the
        // jump drawn from word MaxValue (1 − 2^-53) underflows to exactly zero. A zero-weight item
        // offered then subtracts nothing and leaves the countdown at zero, so only the weight
        // check keeps it out.
        var rng = new ScriptedRandom([0UL, ulong.MaxValue]);
        var sampler = new WeightedReservoirSampler<int, ScriptedRandom>(1, rng);
        sampler.Add(1, double.Epsilon);

        sampler.Add([2, 3], [0d, 0d]);
        Assert.False(sampler.Add(4, 0d));
        Assert.Equal([1], sampler.Sample.ToArray());

        sampler.Add([5, 6], [0d, double.Epsilon]);
        Assert.Equal([6], sampler.Sample.ToArray());
        Assert.Equal(6, sampler.TotalSeen);
    }

    [Fact]
    public void Add_ShouldReplaceOnTheNextPositiveWeight_WhenTheJumpHasRunDownToZero()
    {
        var rng = new ScriptedRandom([0UL, ulong.MaxValue]);
        var sampler = new WeightedReservoirSampler<int, ScriptedRandom>(1, rng);
        sampler.Add(1, double.Epsilon);

        Assert.True(sampler.Add(2, double.Epsilon));
        Assert.Equal([2], sampler.Sample.ToArray());
    }

    [Fact]
    public void Indexer_ShouldReturnTheRetainedItems()
    {
        var sampler = new WeightedReservoirSampler<string>(3, 1UL);
        sampler.Add("x", 1d);
        sampler.Add("y", 2d);

        Assert.Equal(sampler.Sample.ToArray(), new[] { sampler[0], sampler[1] });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(int.MinValue)]
    public void Indexer_ShouldThrow_WhenTheIndexIsOutsideTheRetainedSample(int index)
    {
        var sampler = new WeightedReservoirSampler<int>(3, 1UL);
        sampler.Add(1, 1d);
        sampler.Add(2, 1d);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => sampler[index]);
        Assert.Equal("index", ex.ParamName);
    }

    [Fact]
    public void Clear_ShouldDiscardTheSample_TheStreamPosition_AndTheWeightTotal()
    {
        var sampler = new WeightedReservoirSampler<string>(2, 1UL);
        for (int i = 0; i < 50; i++)
        {
            sampler.Add(i.ToString(), 1d);
        }

        sampler.Clear();

        Assert.Equal(0, sampler.Count);
        Assert.Equal(0, sampler.TotalSeen);
        Assert.Equal(0d, sampler.TotalWeight);
        Assert.False(sampler.IsFull);

        sampler.Add("again", 1d);
        sampler.Add("and again", 1d);
        Assert.Equal(["again", "and again"], sampler.Sample.ToArray().Order());
    }

    [Fact]
    public void GetEnumerator_ShouldYieldExactlyTheRetainedItems()
    {
        var sampler = new WeightedReservoirSampler<int>(4, 2UL);
        for (int i = 0; i < 100; i++)
        {
            sampler.Add(i, 1d);
        }

        var walked = new List<int>();
        foreach (int item in sampler)
        {
            walked.Add(item);
        }

        Assert.Equal(sampler.Sample.ToArray(), walked);

        var nonGeneric = new List<object?>();
        IEnumerator legacy = ((IEnumerable)sampler).GetEnumerator();
        while (legacy.MoveNext())
        {
            nonGeneric.Add(legacy.Current);
        }

        Assert.Equal(walked.Cast<object?>(), nonGeneric);
    }

    [Fact]
    public void GetEnumerator_ShouldThrow_WhenModifiedMidWalk_ByAnAddThatDiscards_AMerge_OrAClear()
    {
        var sampler = new WeightedReservoirSampler<int>(2, 3UL);
        sampler.Add(1, 1d);
        sampler.Add(2, 1d);

        IEnumerator<int> discarded = sampler.GetEnumerator();
        Assert.True(discarded.MoveNext());
        Assert.False(sampler.Add(3, 0d));
        Assert.Throws<InvalidOperationException>(() => discarded.MoveNext());

        IEnumerator<int> merged = sampler.GetEnumerator();
        sampler.Merge(new WeightedReservoirSampler<int>(2, 4UL));
        Assert.Throws<InvalidOperationException>(() => merged.MoveNext());

        IEnumerator<int> batched = sampler.GetEnumerator();
        sampler.Add([9], [1d]);
        Assert.Throws<InvalidOperationException>(() => batched.MoveNext());

        IEnumerator<int> cleared = sampler.GetEnumerator();
        sampler.Clear();
        Assert.Throws<InvalidOperationException>(() => cleared.MoveNext());
    }

    [Fact]
    public void GetEnumerator_ShouldThrow_WhenModifiedAfterTheWalkHasFinished()
    {
        var sampler = new WeightedReservoirSampler<int>(2, 3UL);
        sampler.Add(1, 1d);

        IEnumerator<int> enumerator = sampler.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());

        sampler.Add(2, 1d);
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Enumerator_ShouldExposeTheCurrentItem_AndDefaultOutsideTheWalk()
    {
        var sampler = new WeightedReservoirSampler<string>(1, 3UL);
        sampler.Add("only", 1d);

        using IEnumerator<string> enumerator = sampler.GetEnumerator();
        Assert.Null(enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("only", enumerator.Current);
        Assert.Equal("only", ((IEnumerator)enumerator).Current);
        Assert.False(enumerator.MoveNext());
        Assert.Null(enumerator.Current);
    }

    [Fact]
    public void Enumerator_Reset_ShouldRestartTheWalk_AndThrowOnceTheSamplerWasModified()
    {
        var sampler = new WeightedReservoirSampler<int>(2, 3UL);
        sampler.Add(1, 1d);
        sampler.Add(2, 1d);

        IEnumerator<int> enumerator = sampler.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        enumerator.Reset();
        Assert.Equal(0, enumerator.Current);
        Assert.True(enumerator.MoveNext());

        sampler.Add(3, 1d);
        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    /// <summary>
    /// The exact probability that each item is among the first <paramref name="k"/> successive
    /// weighted draws without replacement: a DP over subsets, where the probability of having
    /// drawn exactly the set <c>S</c> (in any order) sums, over its last member <c>i</c>, the
    /// probability of <c>S \ {i}</c> times <c>w_i / (W − w(S \ {i}))</c>.
    /// </summary>
    internal static double[] ExactInclusionProbabilities(double[] weights, int k)
    {
        int n = weights.Length;
        double total = weights.Sum();
        var drawn = new double[1 << n];
        drawn[0] = 1d;

        for (int set = 1; set < (1 << n); set++)
        {
            if (BitOperations.PopCount((uint)set) > k)
            {
                continue;
            }

            for (int i = 0; i < n; i++)
            {
                if ((set & (1 << i)) == 0)
                {
                    continue;
                }

                int before = set & ~(1 << i);
                double taken = 0d;
                for (int j = 0; j < n; j++)
                {
                    if ((before & (1 << j)) != 0)
                    {
                        taken += weights[j];
                    }
                }

                drawn[set] += drawn[before] * weights[i] / (total - taken);
            }
        }

        var inclusion = new double[n];
        for (int set = 0; set < (1 << n); set++)
        {
            if (BitOperations.PopCount((uint)set) != k)
            {
                continue;
            }

            for (int i = 0; i < n; i++)
            {
                if ((set & (1 << i)) != 0)
                {
                    inclusion[i] += drawn[set];
                }
            }
        }

        return inclusion;
    }

    /// <summary>
    /// Asserts every observed frequency is within five standard errors of its exact probability.
    /// The seeds are fixed, so this is deterministic; five sigma keeps it far from a false alarm
    /// while still catching a bias of a percent or two.
    /// </summary>
    internal static void AssertFrequenciesMatch(double[] exact, int[] counts, int trials)
    {
        for (int i = 0; i < exact.Length; i++)
        {
            double p = exact[i];
            double observed = (double)counts[i] / trials;
            double standardError = Math.Sqrt(p * (1d - p) / trials);
            Assert.True(
                Math.Abs(observed - p) <= 5d * standardError,
                $"Item {i}: observed {observed:F5}, exact {p:F5}, standard error {standardError:F5}.");
        }
    }
}
