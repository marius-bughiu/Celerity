using System.Collections;
using Celerity.Primitives;
using Celerity.Statistics;

namespace Celerity.Tests.Statistics;

/// <summary>
/// Behavioural tests for <see cref="ReservoirSampler{T, TRng}"/> — the fill phase, the
/// Algorithm&#160;L skip phase, the uniformity the whole thing exists to provide, seeded
/// reproducibility, and the two degenerate ends of the skip arithmetic that a real generator
/// will not reach on its own.
/// </summary>
public class ReservoirSamplerTests
{
    /// <summary>
    /// A generator that cycles a fixed script of 64-bit words. It exists to drive the
    /// acceptance weight down far faster than a real generator would, so the collapsed-weight
    /// path is reached in a few hundred items rather than a few hundred million.
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
        var sampler = new ReservoirSampler<int>(capacity: 4, seed: 7UL);

        Assert.Equal(4, sampler.Capacity);
        Assert.Equal(0, sampler.Count);
        Assert.Equal(0, sampler.TotalSeen);
        Assert.False(sampler.IsFull);
        Assert.True(sampler.Sample.IsEmpty);
    }

    [Fact]
    public void Add_ShouldRetainEveryItem_UntilTheReservoirIsFull()
    {
        var sampler = new ReservoirSampler<int>(capacity: 3, seed: 1UL);

        Assert.True(sampler.Add(10));
        Assert.True(sampler.Add(20));
        Assert.False(sampler.IsFull);
        Assert.True(sampler.Add(30));

        Assert.True(sampler.IsFull);
        Assert.Equal(3, sampler.Count);
        Assert.Equal(3, sampler.TotalSeen);
        Assert.Equal(new[] { 10, 20, 30 }, sampler.Sample.ToArray());
    }

    [Fact]
    public void Add_ShouldKeepTheSampleSizeFixed_OnceTheReservoirIsFull()
    {
        var sampler = new ReservoirSampler<int>(capacity: 8, seed: 99UL);

        for (int i = 0; i < 10_000; i++)
        {
            sampler.Add(i);
        }

        Assert.Equal(8, sampler.Count);
        Assert.Equal(8, sampler.Capacity);
        Assert.Equal(10_000, sampler.TotalSeen);
        Assert.All(sampler.Sample.ToArray(), item => Assert.InRange(item, 0, 9_999));
        Assert.Equal(8, sampler.Sample.ToArray().Distinct().Count());
    }

    [Fact]
    public void Add_ShouldReportBothRetainedAndDiscardedItems_OnceTheReservoirIsFull()
    {
        var sampler = new ReservoirSampler<int>(capacity: 4, seed: 12345UL);
        for (int i = 0; i < 4; i++)
        {
            sampler.Add(i);
        }

        int retained = 0;
        int discarded = 0;
        for (int i = 4; i < 2_000; i++)
        {
            if (sampler.Add(i))
            {
                retained++;
            }
            else
            {
                discarded++;
            }
        }

        Assert.True(retained > 0, "Expected at least one replacement over 2,000 items.");
        Assert.True(discarded > retained, "Algorithm L should skip far more items than it keeps.");
    }

    [Fact]
    public void Add_ShouldProduceAUniformSample_OverManyIndependentRuns()
    {
        const int StreamLength = 50;
        const int Capacity = 5;
        const int Trials = 5_000;

        int[] selections = new int[StreamLength];
        for (int trial = 0; trial < Trials; trial++)
        {
            var sampler = new ReservoirSampler<int>(Capacity, (ulong)trial * 0x9E3779B97F4A7C15UL);
            for (int i = 0; i < StreamLength; i++)
            {
                sampler.Add(i);
            }

            foreach (int item in sampler.Sample)
            {
                selections[item]++;
            }
        }

        // Each item should be retained with probability Capacity / StreamLength, so about 500
        // times over 5,000 trials. The bound is ±20%, roughly 4.7 standard deviations.
        int expected = Trials * Capacity / StreamLength;
        for (int i = 0; i < StreamLength; i++)
        {
            Assert.InRange(selections[i], (int)(expected * 0.8), (int)(expected * 1.2));
        }
    }

    [Fact]
    public void Add_ShouldProduceTheSameSample_ForTheSameSeedAndStream()
    {
        static int[] Run(ulong seed)
        {
            var sampler = new ReservoirSampler<int>(capacity: 16, seed: seed);
            for (int i = 0; i < 5_000; i++)
            {
                sampler.Add(i);
            }

            return sampler.Sample.ToArray();
        }

        Assert.Equal(Run(2026UL), Run(2026UL));
        Assert.NotEqual(Run(2026UL), Run(2027UL));
    }

    [Fact]
    public void Add_ShouldAcceptASpan_AndSampleItInOrder()
    {
        int[] stream = [.. Enumerable.Range(0, 1_000)];

        var sampler = new ReservoirSampler<int>(capacity: 10, seed: 5UL);
        sampler.Add(stream);

        var oneAtATime = new ReservoirSampler<int>(capacity: 10, seed: 5UL);
        foreach (int item in stream)
        {
            oneAtATime.Add(item);
        }

        Assert.Equal(oneAtATime.Sample.ToArray(), sampler.Sample.ToArray());
        Assert.Equal(1_000, sampler.TotalSeen);
    }

    [Fact]
    public void Indexer_ShouldReturnTheRetainedItems()
    {
        var sampler = new ReservoirSampler<string>(capacity: 3, seed: 3UL);
        sampler.Add("a");
        sampler.Add("b");

        Assert.Equal("a", sampler[0]);
        Assert.Equal("b", sampler[1]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Indexer_ShouldThrow_WhenTheIndexIsOutsideTheRetainedSample(int index)
    {
        var sampler = new ReservoirSampler<int>(capacity: 5, seed: 3UL);
        sampler.Add(1);
        sampler.Add(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => sampler[index]);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldExactlyTheRetainedItems()
    {
        var sampler = new ReservoirSampler<int>(capacity: 4, seed: 8UL);
        sampler.Add(1);
        sampler.Add(2);
        sampler.Add(3);

        Assert.Equal(new[] { 1, 2, 3 }, sampler.ToArray());

        List<object?> viaNonGeneric = [];
        foreach (object? item in (IEnumerable)sampler)
        {
            viaNonGeneric.Add(item);
        }

        Assert.Equal(new object?[] { 1, 2, 3 }, viaNonGeneric);
    }

    [Fact]
    public void Clear_ShouldDiscardTheSampleAndTheStreamPosition()
    {
        var sampler = new ReservoirSampler<int>(capacity: 3, seed: 11UL);
        for (int i = 0; i < 100; i++)
        {
            sampler.Add(i);
        }

        sampler.Clear();

        Assert.Equal(0, sampler.Count);
        Assert.Equal(0, sampler.TotalSeen);
        Assert.False(sampler.IsFull);

        sampler.Add(-1);
        Assert.Equal(1, sampler.Count);
        Assert.Equal(-1, sampler[0]);
    }

    [Fact]
    public void Add_ShouldFreezeTheReservoir_WhenTheAcceptanceWeightCollapses()
    {
        // Algorithm L's weight is a running product, so over a long enough stream it shrinks
        // past the point where 1 - w differs from 1 and no later item can be accepted. The
        // script drives that in a few hundred items instead of a few hundred million.
        //
        // A replacement draws exactly three times — the slot to overwrite, the weight factor,
        // then the skip — and the fill draws the last two, so a three-word cycle gives each
        // draw a fixed role from the first replacement onward: a weight factor of ~0.75, a skip
        // numerator of ~1 (so every item is a replacement), and a slot selector that a
        // one-element reservoir ignores.
        //
        // 0.75^n crosses 2^-54 at n = 131, so 400 items is comfortably past the collapse. What
        // is being pinned is that the sampler stops rather than dividing log(u) by zero and
        // computing a negative next index — which would replace on every subsequent item.
        const ulong ThreeQuarters = 6_755_399_441_055_744UL << 11;
        var sampler = new ReservoirSampler<int, ScriptedRandom>(
            1,
            new ScriptedRandom([ThreeQuarters, ulong.MaxValue, 0UL]));

        for (int i = 0; i < 400; i++)
        {
            sampler.Add(i);
        }

        Assert.Equal(400, sampler.TotalSeen);
        Assert.Equal(1, sampler.Count);

        int frozenAt = sampler[0];
        for (int i = 400; i < 500; i++)
        {
            Assert.False(sampler.Add(i), $"item {i} was accepted after the weight collapsed");
        }

        Assert.Equal(frozenAt, sampler[0]);
    }

    [Fact]
    public void NextUnitInterval_ShouldNeverProduceZeroOrOne_ForTheExtremeDraws()
    {
        // Both logarithms in the skip arithmetic are undefined at zero, and a weight of exactly
        // one divides by log(0). The draw is the 53-bit grid offset by half a step precisely so
        // neither endpoint is reachable; these are the two draws that would hit them.
        foreach (ulong bits in new[] { 0UL, ulong.MaxValue })
        {
            var sampler = new ReservoirSampler<int, ScriptedRandom>(4, new ScriptedRandom([bits]));

            for (int i = 0; i < 1_000; i++)
            {
                sampler.Add(i);
            }

            Assert.Equal(4, sampler.Count);
            Assert.All(sampler.Sample.ToArray(), item => Assert.InRange(item, 0, 999));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Constructor_ShouldThrow_WhenTheCapacityIsNotPositive(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReservoirSampler<int>(capacity, seed: 1UL));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReservoirSampler<int, Pcg32>(capacity, new Pcg32(1UL)));
    }
}
