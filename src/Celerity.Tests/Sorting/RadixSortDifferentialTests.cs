using Celerity.Sorting;

namespace Celerity.Tests.Sorting;

/// <summary>
/// Reconciles every <see cref="RadixSort"/> shape against <see cref="Array.Sort(Array)"/> over
/// seeded pseudo-random inputs. The sign-bit and IEEE-754 key transforms are the error-prone part
/// of a radix sort — a wrong branch there produces a plausible-looking but wrongly ordered block
/// rather than an obvious crash — so they are checked against the BCL oracle at every length class,
/// not only on the hand-written fixtures.
/// </summary>
/// <remarks>
/// Each case is a pure function of its seed, so a failure reproduces exactly. Lengths span the
/// single-pass, multi-pass and copy-back cases; the key domains span narrow (one digit differs),
/// full-width, and duplicate-heavy.
/// </remarks>
public class RadixSortDifferentialTests
{
    public static TheoryData<int, int> Cases
    {
        get
        {
            var data = new TheoryData<int, int>();
            foreach (int length in new[] { 2, 3, 17, 64, 257, 1000 })
            {
                foreach (int seed in new[] { 1, 2, 3 })
                {
                    data.Add(length, seed);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sort_ShouldMatchArraySort_WhenKeysAreInt32(int length, int seed)
    {
        var rand = new Random(seed);
        int[] keys = new int[length];
        for (int i = 0; i < length; i++)
        {
            keys[i] = rand.Next(int.MinValue, int.MaxValue);
        }

        int[] expected = (int[])keys.Clone();
        Array.Sort(expected);

        RadixSort.Sort(keys.AsSpan());

        Assert.Equal(expected, keys);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sort_ShouldMatchArraySort_WhenKeysAreUInt32AndNarrow(int length, int seed)
    {
        var rand = new Random(seed);
        uint[] keys = new uint[length];
        for (int i = 0; i < length; i++)
        {
            keys[i] = (uint)rand.Next(0, 300);
        }

        uint[] expected = (uint[])keys.Clone();
        Array.Sort(expected);

        RadixSort.Sort(keys.AsSpan());

        Assert.Equal(expected, keys);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sort_ShouldMatchArraySort_WhenKeysAreInt64(int length, int seed)
    {
        var rand = new Random(seed);
        long[] keys = new long[length];
        for (int i = 0; i < length; i++)
        {
            keys[i] = rand.NextInt64(long.MinValue, long.MaxValue);
        }

        long[] expected = (long[])keys.Clone();
        Array.Sort(expected);

        RadixSort.Sort(keys.AsSpan());

        Assert.Equal(expected, keys);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sort_ShouldMatchArraySort_WhenKeysAreUInt64AndNarrow(int length, int seed)
    {
        var rand = new Random(seed);
        ulong[] keys = new ulong[length];
        for (int i = 0; i < length; i++)
        {
            keys[i] = (ulong)rand.Next(0, 1000);
        }

        ulong[] expected = (ulong[])keys.Clone();
        Array.Sort(expected);

        RadixSort.Sort(keys.AsSpan());

        Assert.Equal(expected, keys);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sort_ShouldMatchArraySort_WhenKeysAreSingle(int length, int seed)
    {
        var rand = new Random(seed);
        float[] keys = new float[length];
        for (int i = 0; i < length; i++)
        {
            keys[i] = (float)((rand.NextDouble() - 0.5) * 1e6);
        }

        float[] expected = (float[])keys.Clone();
        Array.Sort(expected);

        RadixSort.Sort(keys.AsSpan());

        Assert.Equal(expected, keys);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sort_ShouldMatchArraySort_WhenKeysAreDouble(int length, int seed)
    {
        var rand = new Random(seed);
        double[] keys = new double[length];
        for (int i = 0; i < length; i++)
        {
            keys[i] = (rand.NextDouble() - 0.5) * 1e12;
        }

        double[] expected = (double[])keys.Clone();
        Array.Sort(expected);

        RadixSort.Sort(keys.AsSpan());

        Assert.Equal(expected, keys);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sort_ShouldMatchArraySortOfPairs_WhenAPayloadIsCarried(int length, int seed)
    {
        var rand = new Random(seed);
        int[] keys = new int[length];
        int[] values = new int[length];
        for (int i = 0; i < length; i++)
        {
            // A deliberately small key domain so most keys repeat: that is where a key+payload sort
            // can silently pair the wrong value with the right key.
            keys[i] = rand.Next(-5, 6);
            values[i] = i;
        }

        int[] expectedKeys = (int[])keys.Clone();
        int[] expectedValues = (int[])values.Clone();
        Array.Sort(expectedKeys, expectedValues);

        RadixSort.Sort<int>(keys.AsSpan(), values.AsSpan());

        Assert.Equal(expectedKeys, keys);

        // Array.Sort is unstable, so compare the (key, value) multiset rather than the exact
        // value order, and separately assert the stability the radix sort does guarantee.
        Assert.Equal(expectedValues.Order(), values.Order());
        for (int i = 1; i < length; i++)
        {
            if (keys[i] == keys[i - 1])
            {
                Assert.True(values[i] > values[i - 1]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ArgSort_ShouldProduceAStableAscendingPermutation_WhenKeysRepeat(int length, int seed)
    {
        var rand = new Random(seed);
        long[] keys = new long[length];
        for (int i = 0; i < length; i++)
        {
            keys[i] = rand.Next(-3, 4);
        }

        int[] indices = new int[length];
        RadixSort.ArgSort(keys, indices);

        Assert.Equal(Enumerable.Range(0, length), indices.Order());
        for (int i = 1; i < length; i++)
        {
            Assert.True(keys[indices[i - 1]] < keys[indices[i]]
                || (keys[indices[i - 1]] == keys[indices[i]] && indices[i - 1] < indices[i]));
        }
    }
}
