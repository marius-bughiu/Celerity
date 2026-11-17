using Celerity.Sorting;
using CsCheck;

namespace Celerity.Tests.Sorting;

/// <summary>
/// Reconciles every <see cref="StableSort"/> entry point against a stable LINQ <c>OrderBy</c> over
/// seeded pseudo-random inputs. Elements carry a tag that the comparer ignores, so the oracle checks
/// stability as well as order. The shapes are the ones the run finder and the merge policy treat
/// differently: no order at all, ascending and descending runs of mixed lengths, a sorted body with
/// a disordered tail, and sawtooth and organ-pipe patterns whose runs are all the same length.
/// </summary>
/// <remarks>
/// Each case is a pure function of its seed, so a failure reproduces exactly. Lengths straddle the
/// 16-element minimum run, so they span the single extended run, a short final run, and many runs.
/// The key domain is narrow in most shapes, so ties are the common case rather than the exception.
/// </remarks>
public class StableSortDifferentialTests
{
    public enum Shape
    {
        /// <summary>Keys drawn from a narrow domain, in no order.</summary>
        Random,

        /// <summary>A random permutation: no ties at all.</summary>
        Distinct,

        /// <summary>Sorted, then about one key in fifty swapped with a random partner.</summary>
        NearlySorted,

        /// <summary>Sorted batches of random lengths, some ascending and some descending.</summary>
        MixedRuns,

        /// <summary>A sorted body followed by a tail of random keys about a tenth as long.</summary>
        AppendedTail,

        /// <summary>Repeated ascending ramps of one fixed length.</summary>
        Sawtooth,

        /// <summary>Ascending to the middle, then descending.</summary>
        OrganPipe,
    }

    public static TheoryData<int, int, Shape> Cases
    {
        get
        {
            var data = new TheoryData<int, int, Shape>();
            foreach (int length in new[] { 0, 1, 2, 15, 16, 17, 31, 33, 64, 100, 257, 1_000, 5_000 })
            {
                foreach (int seed in new[] { 1, 2 })
                {
                    foreach (Shape shape in Enum.GetValues<Shape>())
                    {
                        data.Add(length, seed, shape);
                    }
                }
            }

            return data;
        }
    }

    internal static int[] Generate(int length, int seed, Shape shape)
    {
        var rand = new Random(seed * 7919 + length);
        int domain = Math.Max(1, length / 8);
        var keys = new int[length];
        switch (shape)
        {
            case Shape.Random:
                for (int i = 0; i < length; i++)
                {
                    keys[i] = rand.Next(domain);
                }

                break;
            case Shape.Distinct:
                for (int i = 0; i < length; i++)
                {
                    keys[i] = i;
                }

                rand.Shuffle(keys);
                break;
            case Shape.NearlySorted:
                for (int i = 0; i < length; i++)
                {
                    keys[i] = i / 4;
                }

                for (int swaps = length / 50; swaps >= 0 && length > 0; swaps--)
                {
                    int a = rand.Next(length), b = rand.Next(length);
                    (keys[a], keys[b]) = (keys[b], keys[a]);
                }

                break;
            case Shape.MixedRuns:
                for (int start = 0; start < length;)
                {
                    int runLength = Math.Min(length - start, rand.Next(1, 3 * 16));
                    var run = keys.AsSpan(start, runLength);
                    for (int i = 0; i < run.Length; i++)
                    {
                        run[i] = rand.Next(domain);
                    }

                    run.Sort();
                    if (rand.Next(2) == 0)
                    {
                        run.Reverse();
                    }

                    start += runLength;
                }

                break;
            case Shape.AppendedTail:
                int body = length - length / 10;
                for (int i = 0; i < length; i++)
                {
                    keys[i] = i < body ? i / 2 : rand.Next(Math.Max(1, body / 2));
                }

                break;
            case Shape.Sawtooth:
                int tooth = rand.Next(2, 40);
                for (int i = 0; i < length; i++)
                {
                    keys[i] = i % tooth;
                }

                break;
            default:
                for (int i = 0; i < length; i++)
                {
                    keys[i] = Math.Min(i, length - 1 - i);
                }

                break;
        }

        return keys;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sort_ShouldMatchAStableSort(int length, int seed, Shape shape)
    {
        var items = StableSortTests.Tagged(Generate(length, seed, shape));
        var expected = StableSortTests.StableOracle(items);

        StableSort.Sort(items.AsSpan(), default(StableSortTests.ByKey));

        Assert.Equal(expected, items);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void SortWithPayload_ShouldMatchAStableSort(int length, int seed, Shape shape)
    {
        int[] keys = Generate(length, seed, shape);
        int[] values = Enumerable.Range(0, length).ToArray();
        var expected = StableSortTests.StableOracle(StableSortTests.Tagged(keys));

        StableSort.Sort(keys.AsSpan(), values.AsSpan());

        Assert.Equal(expected.Select(e => e.Key), keys);
        Assert.Equal(expected.Select(e => e.Tag), values);
    }

    [Fact]
    public void Sort_ShouldMatchAStableSort_OnGeneratedInput()
    {
        // CsCheck shrinks a failing case to its smallest key list, which for a merge bug is usually a
        // pair of short runs.
        Gen.Int[0, 40].Array[0, 600].Sample(keyValues =>
        {
            var items = StableSortTests.Tagged(keyValues);
            var expected = StableSortTests.StableOracle(items);

            StableSort.Sort(items.AsSpan(), default(StableSortTests.ByKey));

            Assert.Equal(expected, items);
        });
    }
}
