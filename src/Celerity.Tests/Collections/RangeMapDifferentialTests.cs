using System.Collections;
using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="RangeMap{TKey, TValue, TComparer}"/> against the plainest possible
/// model of the same facts: a dense array holding the value mapped at every key of a small integer domain.
///
/// <para>
/// A range map's whole difficulty is the edges — the straddler split at each end of an assignment, the merge
/// with an equal neighbour on either side, the seam where one range ends exactly as the next begins — and
/// every one of them is invisible to a model that stores ranges, because it would have to make the same
/// decisions to agree. The dense array makes none: an assignment writes a value into every covered cell, and
/// the maximal runs of equal values are read back off it afterwards. So the map's stored ranges have to equal
/// those runs exactly, which checks the split, the merge and the canonical form all at once.
/// </para>
///
/// <para>
/// The no-op contract is checked the same way. An enumerator is taken before every write; if the write left
/// the model unchanged the enumerator must still drain, and if it changed anything the enumerator must throw.
/// That pins both directions — a real change always invalidates, and nothing else ever does.
/// </para>
/// </summary>
public class RangeMapDifferentialTests
{
    // Keys run from Low to domain + High - 1 so writes and queries reach past both ends of the populated span.
    private const int Low = -3;
    private const int High = 3;

    // Operation count, domain width (a narrow one packs many writes onto few keys and forces merges; a wide
    // one grows past a single 31-key B-tree node), value alphabet (small makes equal neighbours common, which
    // is what exercises the merge), and the seed that fills in the rest.
    private static readonly Gen<(int Ops, int Domain, int Values, uint Seed)> GenRun =
        Gen.Select(Gen.Int[0, 250], Gen.Int[1, 400], Gen.Int[1, 6], Gen.UInt);

    [Fact]
    public void Operations_ShouldMatchADenseModel_UnderGeneratedRuns()
    {
        GenRun.Sample(spec => Run(new Random((int)spec.Seed), spec.Ops, spec.Domain, spec.Values), iter: 120);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(2026)]
    public void Operations_ShouldMatchADenseModel_OverLongRuns(int seed)
    {
        // Longer and wider than the generated runs, so the tree reaches several levels and a single write can
        // sweep away hundreds of stored ranges.
        Run(new Random(seed), ops: 1500, domain: 3000, values: 4);
    }

    private static void Run(Random rand, int ops, int domain, int values)
    {
        var map = new RangeMap<int, int>();
        var model = new int?[domain + High - Low];

        for (int op = 0; op < ops; op++)
        {
            int start = rand.Next(Low, domain + High);
            int width = rand.Next(0, 10) == 0 ? rand.Next(0, domain + 1) : rand.Next(0, 12);
            int end = Math.Min(start + width, domain + High);

            int?[] before = (int?[])model.Clone();
            IEnumerator live = map.GetEnumerator();
            int roll = rand.Next(0, 100);

            if (roll < 60)
            {
                int value = rand.Next(0, values);
                map.Set(start, end, value);
                for (int k = start; k < end; k++)
                    model[k - Low] = value;
            }
            else if (roll < 95)
            {
                bool anyMapped = false;
                for (int k = start; k < end; k++)
                {
                    anyMapped |= model[k - Low].HasValue;
                    model[k - Low] = null;
                }

                Assert.Equal(anyMapped, map.Remove(start, end));
            }
            else
            {
                map.Clear();
                Array.Clear(model);
            }

            if (before.AsSpan().SequenceEqual(model))
            {
                // A write that changed nothing must leave the enumerator valid: draining it must not throw.
                while (live.MoveNext())
                {
                }
            }
            else
            {
                Assert.Throws<InvalidOperationException>(() => live.MoveNext());
            }

            AssertMatchesModel(map, model);

            int windowStart = rand.Next(Low, domain + High);
            int windowEnd = Math.Min(windowStart + rand.Next(0, 20), domain + High);
            AssertWindowMatchesModel(map, model, windowStart, windowEnd);
        }
    }

    // The stored ranges must be exactly the maximal runs of equal values in the model, and every point query
    // must agree with the model cell by cell.
    private static void AssertMatchesModel(RangeMap<int, int> map, int?[] model)
    {
        List<Interval<int, int>> runs = Runs(model);
        Interval<int, int>[] stored = map.ToArray();

        Assert.Equal(runs.Count, map.Count);
        Assert.Equal(runs.Count, stored.Length);
        for (int i = 0; i < runs.Count; i++)
        {
            Assert.Equal(runs[i].Start, stored[i].Start);
            Assert.Equal(runs[i].End, stored[i].End);
            Assert.Equal(runs[i].Value, stored[i].Value);
        }

        for (int k = Low - 2; k < Low + model.Length + 2; k++)
        {
            int? expected = k >= Low && k < Low + model.Length ? model[k - Low] : null;
            Assert.Equal(expected.HasValue, map.TryGetValue(k, out int actual));
            Assert.Equal(expected.HasValue, map.ContainsKey(k));
            Assert.Equal(expected.HasValue, map.TryGetRange(k, out Interval<int, int> range));

            if (expected.HasValue)
            {
                Assert.Equal(expected.Value, actual);
                Assert.True(range.Start <= k && k < range.End, $"TryGetRange({k}) returned [{range.Start}, {range.End})");
                Assert.Contains(runs, run => run.Start == range.Start && run.End == range.End && run.Value == range.Value);
            }
        }
    }

    private static void AssertWindowMatchesModel(RangeMap<int, int> map, int?[] model, int start, int end)
    {
        var expected = new List<Interval<int, int>>();
        foreach (Interval<int, int> run in Runs(model))
        {
            if (start < end && run.Start < end && start < run.End)
                expected.Add(run);
        }

        var actual = new List<Interval<int, int>>();
        foreach (Interval<int, int> range in map.EnumerateOverlapping(start, end))
            actual.Add(range);

        Assert.Equal(expected.Count > 0, map.Overlaps(start, end));
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Start, actual[i].Start);
            Assert.Equal(expected[i].End, actual[i].End);
            Assert.Equal(expected[i].Value, actual[i].Value);
        }
    }

    private static List<Interval<int, int>> Runs(int?[] model)
    {
        var runs = new List<Interval<int, int>>();
        int i = 0;
        while (i < model.Length)
        {
            if (!model[i].HasValue)
            {
                i++;
                continue;
            }

            int j = i + 1;
            while (j < model.Length && model[j] == model[i])
                j++;

            runs.Add(new Interval<int, int>(i + Low, j + Low, model[i]!.Value));
            i = j;
        }

        return runs;
    }
}
