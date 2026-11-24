using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Statistical tests that a <see cref="MinHashLshIndex{TKey,T,THasher}"/> returns a pair with about
/// the probability its <see cref="MinHashLshIndex{TKey,T,THasher}.CandidateProbability"/> curve
/// models, for large sets and for sets far smaller than the register count (where densification
/// does the work), and that it finds near-duplicates in a corpus of unrelated sets. These checks
/// cover the chosen corpora, not a probability guarantee for all inputs. Every data set is
/// deterministic, so the measured rates are stable across runs.
/// </summary>
public class MinHashLshIndexAccuracyTests
{
    // `pairs` independent pairs of sets with an exact Jaccard similarity: `shared` elements in both
    // and `distinct` more in each. Returns the fraction of pairs the index returned.
    private static double CandidateRate(int pairs, int shared, int distinct, int bands, int rows)
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(bands, rows);
        var queries = new List<MinHashSketch<int, Int32Murmur3Hasher>>();
        int next = 0;
        for (int pair = 0; pair < pairs; pair++)
        {
            // A fresh, disjoint block of elements per pair, so pairs cannot match each other.
            int baseValue = next;
            next += shared + 2 * distinct;
            var common = Enumerable.Range(baseValue, shared).ToArray();
            var stored = new MinHashSketch<int, Int32Murmur3Hasher>(
                common.Concat(Enumerable.Range(baseValue + shared, distinct)));
            var query = new MinHashSketch<int, Int32Murmur3Hasher>(
                common.Concat(Enumerable.Range(baseValue + shared + distinct, distinct)));
            index.Add(pair, stored);
            queries.Add(query);
        }

        int found = 0;
        var results = new List<int>();
        for (int pair = 0; pair < pairs; pair++)
        {
            results.Clear();
            index.Query(queries[pair], results);
            if (results.Contains(pair))
                found++;
        }

        return (double)found / pairs;
    }

    [Theory]
    [InlineData(1_000, 500, 0.5)]    // J = 1000 / 2000
    [InlineData(1_000, 125, 0.8)]    // J = 1000 / 1250
    [InlineData(600, 700, 0.3)]      // J = 600 / 2000
    [InlineData(18, 1, 0.9)]         // 20-element sets: most registers are densified
    [InlineData(6, 3, 0.5)]          // 12-element sets
    public void CandidateRate_ShouldFollowTheBandingCurve(int shared, int distinct, double jaccard)
    {
        const int Pairs = 400;
        const int Bands = 32;
        const int Rows = 4;
        double expected = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(Bands, Rows).CandidateProbability(jaccard);

        double rate = CandidateRate(Pairs, shared, distinct, Bands, Rows);

        // The binomial standard deviation of a rate over 400 pairs is at most 0.025; the sketches'
        // own estimation noise (and, for small sets, the dependence between densified registers)
        // widens it further.
        Assert.True(Math.Abs(rate - expected) <= 0.12, $"J={jaccard}: rate {rate:F3}, curve {expected:F3}");
    }

    [Fact]
    public void NearDuplicates_ShouldBeFound_AndUnrelatedSetsMostlyNot()
    {
        // 2,000 sets of 200 elements; every 20th is an original followed by four copies with 10% of
        // their elements replaced (J ≈ 0.82). The rest are unrelated.
        var rng = new Random(11);
        var sets = new int[2_000][];
        for (int i = 0; i < sets.Length; i++)
        {
            if (i % 20 is >= 1 and <= 4)
            {
                sets[i] = (int[])sets[i - i % 20].Clone();
                for (int k = 0; k < 20; k++)
                    sets[i][rng.Next(200)] = rng.Next();
            }
            else
            {
                sets[i] = Enumerable.Range(0, 200).Select(_ => rng.Next()).ToArray();
            }
        }

        var sketches = sets.Select(s => new MinHashSketch<int, Int32Murmur3Hasher>(s)).ToArray();
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        for (int i = 0; i < sets.Length; i++)
            index.Add(i, sketches[i]);

        int duplicates = 0;
        int found = 0;
        int strangers = 0;
        var results = new List<int>();
        for (int original = 0; original < sets.Length; original += 20)
        {
            results.Clear();
            index.Query(sketches[original], results);
            for (int copy = original + 1; copy <= original + 4; copy++)
            {
                duplicates++;
                if (results.Contains(copy))
                    found++;
            }

            strangers += results.Count(k => k < original || k > original + 4);
        }

        Assert.True(found >= 0.95 * duplicates, $"found {found} of {duplicates} near-duplicates");
        Assert.True(strangers <= 5, $"{strangers} unrelated sets returned across 100 queries");
    }
}
