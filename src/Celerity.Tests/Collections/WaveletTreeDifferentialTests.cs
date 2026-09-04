using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="WaveletTree"/> against the brute force a caller writes
/// by hand: a quantile is a sort of the window, a range count is a counting loop, a rank is a scan. CsCheck
/// generates random sequences (random length, random alphabet width, random value magnitude) and random
/// windows over each, so any disagreement shrinks to a minimal reproduction with the seed printed.
/// </summary>
public class WaveletTreeDifferentialTests
{
    // Lengths straddle the 64-bit block and 256-bit superblock boundaries each level's rank index pivots on,
    // and the alphabet sweep covers the two level-free degenerate shapes (empty, single symbol) as well as an
    // alphabet wide enough to need several levels — including the exact powers of two where ceil(log2) turns.
    private static readonly Gen<(int Length, int Alphabet, int Origin, uint Seed)> GenSequence =
        Gen.Select(Gen.Int[0, 600], Gen.Int[1, 40], Gen.Int[-1000, 1000], Gen.UInt);

    [Fact]
    public void EveryQuery_ShouldMatchTheBruteForceOracle()
    {
        GenSequence.Sample(spec =>
        {
            int[] values = BuildSequence(spec.Length, spec.Alphabet, spec.Origin, spec.Seed);
            var tree = new WaveletTree(values);
            var rand = new Random(unchecked((int)spec.Seed) ^ 0x5f3759df);

            Assert.Equal(values.Length, tree.Length);
            Assert.Equal(values.Distinct().Count(), tree.AlphabetSize);
            Assert.Equal(values.Distinct().OrderBy(v => v), tree.Symbols.ToArray());

            for (int i = 0; i < values.Length; i++)
                Assert.Equal(values[i], tree[i]);

            // Rank against the counting loop, at every position and for every symbol plus one that is absent.
            foreach (int value in tree.Symbols.ToArray().Append(int.MaxValue))
            {
                for (int i = 0; i <= values.Length; i++)
                    Assert.Equal(CountBelow(values, i, value), tree.Rank(i, value));
            }

            for (int trial = 0; trial < 20; trial++)
            {
                (int start, int length) = RandomWindow(rand, values.Length);
                int[] window = values.Skip(start).Take(length).OrderBy(v => v).ToArray();

                for (int k = 0; k < window.Length; k++)
                    Assert.Equal(window[k], tree.Quantile(start, length, k));

                int lo = spec.Origin + rand.Next(-2, spec.Alphabet + 2);
                int hi = lo + rand.Next(-1, spec.Alphabet + 2);
                Assert.Equal(window.Count(v => v >= lo && v <= hi), tree.RangeCount(start, length, lo, hi));

                foreach (int value in tree.Symbols.ToArray())
                    Assert.Equal(window.Count(v => v == value), tree.RangeRank(start, length, value));
            }
        }, iter: 200);
    }

    [Fact]
    public void Select_ShouldLocateEveryOccurrence_AndRefuseTheOneAfter()
    {
        GenSequence.Sample(spec =>
        {
            int[] values = BuildSequence(spec.Length, spec.Alphabet, spec.Origin, spec.Seed);
            var tree = new WaveletTree(values);

            foreach (int value in tree.Symbols.ToArray())
            {
                int[] expected = Positions(values, value);
                for (int k = 0; k < expected.Length; k++)
                    Assert.Equal(expected[k], tree.Select(k, value));

                Assert.False(tree.TrySelect(expected.Length, value, out int missing));
                Assert.Equal(-1, missing);
            }

            Assert.False(tree.TrySelect(0, int.MinValue, out _));
        }, iter: 200);
    }

    [Fact]
    public void Enumeration_ShouldReplayTheSequence()
    {
        GenSequence.Sample(spec =>
        {
            int[] values = BuildSequence(spec.Length, spec.Alphabet, spec.Origin, spec.Seed);
            var tree = new WaveletTree(values);

            Assert.Equal(values, tree.ToArray());
        }, iter: 200);
    }

    // A sequence of `length` values drawn from an alphabet of at most `alphabet` consecutive codes starting at
    // `origin`. Drawing with repetition is the point: duplicates are what a quantile has to count and what a
    // set-shaped structure could not hold.
    private static int[] BuildSequence(int length, int alphabet, int origin, uint seed)
    {
        var rand = new Random(unchecked((int)seed));
        int[] values = new int[length];
        for (int i = 0; i < length; i++)
            values[i] = origin + rand.Next(alphabet);

        return values;
    }

    private static (int Start, int Length) RandomWindow(Random rand, int total)
    {
        if (total == 0)
            return (0, 0);

        int start = rand.Next(total + 1);
        return (start, rand.Next(total - start + 1));
    }

    private static int CountBelow(int[] values, int index, int value)
    {
        int count = 0;
        for (int i = 0; i < index; i++)
        {
            if (values[i] == value)
                count++;
        }

        return count;
    }

    private static int[] Positions(int[] values, int value)
    {
        var positions = new List<int>();
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == value)
                positions.Add(i);
        }

        return [.. positions];
    }
}
