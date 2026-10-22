using Celerity.Statistics;
using CsCheck;

namespace Celerity.Tests.Statistics;

/// <summary>
/// Differential coverage for <see cref="RollingQuantile{T}"/> against the obvious oracle: a queue of the
/// window in arrival order, copied and sorted after every value, read at rank <c>⌊q · (n − 1)⌋</c>.
/// </summary>
/// <remarks>
/// <para>
/// Values come from a deliberately narrow range so ties are constant: the two heaps meet at the quantile,
/// and a tie at the boundary is where a wrong <c>&lt;</c> / <c>&lt;=</c> choice would put a value on the
/// wrong side. The quantiles include both ends, where one heap is a single entry (<c>0</c>) or empty
/// (<c>1</c>), and the windows include sizes on either side of a full heap level.
/// </para>
/// <para>
/// The seeded sweep pins every branch of the sift and eviction paths on every run, so the coverage gate
/// does not depend on which cases the property happens to draw. The CsCheck property then searches the
/// space beyond it, and shrinks a failure to a minimal stream.
/// </para>
/// </remarks>
public class RollingQuantileDifferentialTests
{
    private static readonly double[] Quantiles = { 0d, 0.1d, 0.25d, 0.5d, 0.75d, 0.9d, 0.99d, 1d };

    public static TheoryData<int, double, int> SweepCases
    {
        get
        {
            var data = new TheoryData<int, double, int>();
            foreach (int window in new[] { 1, 2, 3, 4, 7, 8, 15, 16, 31, 100 })
            {
                foreach (double quantile in Quantiles)
                {
                    data.Add(window, quantile, 6);
                    data.Add(window, quantile, 1_000);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(SweepCases))]
    public void Value_ShouldMatchASortedOracle_AfterEveryAdd(int window, double quantile, int range)
    {
        var random = new Random(HashCode.Combine(window, quantile, range));
        var values = new int[window * 6 + 7];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = random.Next(range);
        }

        AssertMatchesOracle(window, quantile, values);
    }

    [Fact]
    public void Value_ShouldMatchASortedOracle_OverMonotoneStreams()
    {
        // A rising stream always evicts the smallest value and inserts the largest; a falling one the
        // reverse. Both drive the cross-heap trade on every step rather than occasionally.
        int[] rising = Enumerable.Range(0, 200).ToArray();
        int[] falling = rising.Reverse().ToArray();
        foreach (double quantile in Quantiles)
        {
            AssertMatchesOracle(17, quantile, rising);
            AssertMatchesOracle(17, quantile, falling);
        }
    }

    [Fact]
    public void Value_ShouldMatchASortedOracle_ForAnyWindowQuantileAndStream()
    {
        Gen.Select(Gen.Int[1, 40], Gen.Int[0, Quantiles.Length - 1], Gen.Int[0, 12].Array[0, 300])
            .Sample((window, quantileIndex, values) => AssertMatchesOracle(window, Quantiles[quantileIndex], values));
    }

    [Fact]
    public void Value_ShouldMatchASortedOracle_ForAnArbitraryQuantile()
    {
        Gen.Select(Gen.Int[1, 25], Gen.Double[0d, 1d], Gen.Int[0, 50].Array[0, 150])
            .Sample((window, quantile, values) => AssertMatchesOracle(window, quantile, values));
    }

    [Fact]
    public void Clear_ShouldLeaveAWindowThatMatchesAFreshOne()
    {
        Gen.Select(Gen.Int[1, 20], Gen.Int[0, 9].Array[0, 60], Gen.Int[0, 9].Array[0, 60])
            .Sample((window, before, after) =>
            {
                var rolling = new RollingQuantile<int>(window, 0.5d);
                rolling.Add(before);
                rolling.Clear();
                AssertMatchesOracle(rolling, after);
            });
    }

    private static void AssertMatchesOracle(int window, double quantile, int[] values) =>
        AssertMatchesOracle(new RollingQuantile<int>(window, quantile), values);

    private static void AssertMatchesOracle(RollingQuantile<int> rolling, int[] values)
    {
        var oracle = new Queue<int>();
        foreach (int value in values)
        {
            rolling.Add(value);
            oracle.Enqueue(value);
            if (oracle.Count > rolling.WindowSize)
            {
                oracle.Dequeue();
            }

            int[] sorted = oracle.ToArray();
            Array.Sort(sorted);
            int expected = sorted[(int)(rolling.Quantile * (sorted.Length - 1))];

            Assert.Equal(sorted.Length, rolling.Count);
            Assert.Equal(sorted.Length == rolling.WindowSize, rolling.IsFull);
            Assert.Equal(expected, rolling.Value);
        }
    }
}
