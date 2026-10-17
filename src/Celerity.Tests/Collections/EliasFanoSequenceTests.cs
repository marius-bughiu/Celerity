using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Unit coverage for <see cref="EliasFanoSequence"/>: construction and its validation, the split point
/// <see cref="EliasFanoSequence.LowBitCount"/>, positional reads, and the four successor-shaped queries at
/// the edges a lower bound gets wrong — below the first value, past the last, on a repeat, and between two
/// values that share a high part.
/// </summary>
public class EliasFanoSequenceTests
{
    private static readonly long[] Offsets = [0, 3, 3, 10, 64, 65, 200, 1000, 1000, 1000, 4096];

    [Fact]
    public void Constructor_ShouldAcceptAnEmptySequence()
    {
        var sequence = new EliasFanoSequence([]);

        Assert.Equal(0, sequence.Count);
        Assert.Equal(0, sequence.LowBitCount);
        Assert.Equal(0, sequence.MemoryUsageInBytes);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTheFirstValueIsNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new EliasFanoSequence([-1, 5]));

        Assert.Equal("values", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenAValueDecreases()
    {
        var ex = Assert.Throws<ArgumentException>(() => new EliasFanoSequence([1, 5, 4, 9]));

        Assert.Equal("values", ex.ParamName);
        Assert.Contains("index 2", ex.Message);
    }

    [Fact]
    public void Constructor_ShouldAcceptRepeats()
    {
        var sequence = new EliasFanoSequence([7, 7, 7, 7]);

        Assert.Equal([7L, 7, 7, 7], sequence.ToArray());
    }

    [Fact]
    public void Constructor_ShouldCopyTheInput()
    {
        long[] values = [1, 2, 3];
        var sequence = new EliasFanoSequence(values);

        values[2] = 99;

        Assert.Equal(3, sequence[2]);
    }

    [Theory]
    [InlineData(new long[] { 5 }, 2)]                 // max 5 / n 1 = 5, log2 = 2
    [InlineData(new long[] { 0, 0, 0, 0 }, 0)]        // a quotient of zero needs no low bits
    [InlineData(new long[] { 1, 2, 3 }, 0)]           // 3 / 3 = 1, log2 = 0
    [InlineData(new long[] { 0, 1000 }, 8)]           // 1000 / 2 = 500, log2 = 8
    [InlineData(new long[] { 0, long.MaxValue }, 61)] // (2^63 - 1) / 2 = 2^62 - 1, log2 = 61
    [InlineData(new long[] { long.MaxValue }, 62)]    // the widest split a long can need
    public void LowBitCount_ShouldBeTheFloorOfLog2OfMaxOverCount(long[] values, int expected)
    {
        Assert.Equal(expected, new EliasFanoSequence(values).LowBitCount);
    }

    [Fact]
    public void Indexer_ShouldReadEveryValueBack()
    {
        var sequence = new EliasFanoSequence(Offsets);

        Assert.Equal(Offsets.Length, sequence.Count);
        for (int i = 0; i < Offsets.Length; i++)
            Assert.Equal(Offsets[i], sequence[i]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Indexer_ShouldThrow_WhenTheIndexIsOutOfRange(int index)
    {
        var sequence = new EliasFanoSequence(Offsets);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => sequence[index]);
        Assert.Equal("index", ex.ParamName);
    }

    [Fact]
    public void Indexer_ShouldRoundTripTheExtremesOfTheRange()
    {
        long[] values = [0, 1, long.MaxValue / 2, long.MaxValue - 1, long.MaxValue, long.MaxValue];
        var sequence = new EliasFanoSequence(values);

        Assert.Equal(values, sequence.ToArray());
        for (int i = 0; i < values.Length; i++)
            Assert.Equal(values[i], sequence[i]);
    }

    [Theory]
    [InlineData(long.MinValue, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 1)]     // the first of two repeats
    [InlineData(4, 3)]
    [InlineData(64, 4)]
    [InlineData(66, 6)]
    [InlineData(1000, 7)]  // the first of three repeats
    [InlineData(1001, 10)]
    [InlineData(4096, 10)]
    [InlineData(4097, 11)] // above every value
    [InlineData(long.MaxValue, 11)]
    public void CountLessThan_ShouldBeTheLowerBoundIndex(long value, int expected)
    {
        var sequence = new EliasFanoSequence(Offsets);

        Assert.Equal(expected, sequence.CountLessThan(value));
        Assert.Equal(Offsets.Count(v => v < value), sequence.CountLessThan(value));
    }

    [Fact]
    public void Queries_ShouldReportNothing_OnAnEmptySequence()
    {
        var sequence = new EliasFanoSequence([]);

        Assert.Equal(0, sequence.CountLessThan(0));
        Assert.Equal(0, sequence.CountLessThan(long.MaxValue));
        Assert.False(sequence.Contains(0));
        Assert.Equal(-1, sequence.IndexOf(0));
        Assert.False(sequence.TryGetLowerBound(0, out long bound));
        Assert.Equal(0, bound);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 3)]
    [InlineData(3, 3)]
    [InlineData(11, 64)]
    [InlineData(201, 1000)]
    [InlineData(1000, 1000)]
    [InlineData(1001, 4096)]
    [InlineData(4096, 4096)]
    public void TryGetLowerBound_ShouldReturnTheSmallestValueAtOrAbove(long value, long expected)
    {
        var sequence = new EliasFanoSequence(Offsets);

        Assert.True(sequence.TryGetLowerBound(value, out long bound));
        Assert.Equal(expected, bound);
    }

    [Fact]
    public void TryGetLowerBound_ShouldFail_WhenEveryValueIsSmaller()
    {
        var sequence = new EliasFanoSequence(Offsets);

        Assert.False(sequence.TryGetLowerBound(4097, out long bound));
        Assert.Equal(0, bound);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 1)]
    [InlineData(10, 3)]
    [InlineData(1000, 7)]
    [InlineData(4096, 10)]
    [InlineData(-1, -1)]
    [InlineData(4, -1)]
    [InlineData(999, -1)]
    [InlineData(4097, -1)]
    public void IndexOf_ShouldFindTheFirstOccurrence(long value, int expected)
    {
        var sequence = new EliasFanoSequence(Offsets);

        Assert.Equal(expected, sequence.IndexOf(value));
        Assert.Equal(expected >= 0, sequence.Contains(value));
    }

    [Fact]
    public void Queries_ShouldResolveWithinADenseRun_WhenManyValuesShareAHighPart()
    {
        // 300 repeats of one large value, then one far above: L is wide, so the repeats all land on a single
        // high part and the lower bound has to binary-search a run of 300 low parts.
        long[] values = [.. Enumerable.Repeat(5_000_000_000L, 300), 9_000_000_000L];
        var sequence = new EliasFanoSequence(values);

        Assert.Equal(0, sequence.CountLessThan(5_000_000_000L));
        Assert.Equal(300, sequence.CountLessThan(5_000_000_001L));
        Assert.Equal(0, sequence.IndexOf(5_000_000_000L));
        Assert.Equal(-1, sequence.IndexOf(4_999_999_999L));
        Assert.True(sequence.TryGetLowerBound(5_000_000_001L, out long bound));
        Assert.Equal(9_000_000_000L, bound);
    }

    [Fact]
    public void Queries_ShouldWork_WhenNoLowBitsAreStored()
    {
        // Values below the count give L = 0: every value is its own high part and the low vector is empty.
        long[] values = [0, 0, 1, 2, 2, 2, 5, 9];
        var sequence = new EliasFanoSequence(values);

        Assert.Equal(0, sequence.LowBitCount);
        Assert.Equal(values, sequence.ToArray());
        Assert.Equal(2, sequence.CountLessThan(1));
        Assert.Equal(3, sequence.IndexOf(2));
        Assert.False(sequence.Contains(3));
        Assert.True(sequence.TryGetLowerBound(6, out long bound));
        Assert.Equal(9, bound);
    }

    [Fact]
    public void Indexer_ShouldCrossManySelectSamples_OnALongSequence()
    {
        // Thousands of values with irregular gaps, so the high vector spans many 128-one and 128-zero samples
        // and the low parts straddle word boundaries at several offsets.
        var rand = new Random(1234);
        long[] values = new long[5000];
        long current = 0;
        for (int i = 0; i < values.Length; i++)
        {
            current += rand.Next(0, 1000);
            values[i] = current;
        }

        var sequence = new EliasFanoSequence(values);

        for (int i = 0; i < values.Length; i++)
            Assert.Equal(values[i], sequence[i]);

        for (int probe = 0; probe < 2000; probe++)
        {
            long value = rand.NextInt64(-10, current + 10);
            int expected = Array.FindIndex(values, v => v >= value);
            if (expected < 0)
                expected = values.Length;

            Assert.Equal(expected, sequence.CountLessThan(value));
        }
    }

    [Fact]
    public void MemoryUsageInBytes_ShouldCountEveryArray()
    {
        // 1,000,000 values spaced exactly 100 apart: max 99,999,900 over n = 10^6 is 99, so L = 6. The low
        // vector is 6,000,000 bits (93,750 words). High parts reach 99,999,900 >> 6 = 1,562,498, so the high
        // vector is 10^6 + 1,562,499 bits (40,040 words), and its rank directory one int per four words
        // (10,010). One-samples: ceil(10^6 / 128) = 7,813. Zero-samples: (1,562,498 >> 7) + 1 = 12,208.
        long[] values = new long[1_000_000];
        for (int i = 0; i < values.Length; i++)
            values[i] = i * 100L;

        var sequence = new EliasFanoSequence(values);

        Assert.Equal(6, sequence.LowBitCount);
        Assert.Equal(((93_750L + 40_040 + 7_813 + 12_208) * sizeof(ulong)) + (10_010L * sizeof(int)),
            sequence.MemoryUsageInBytes);
        Assert.True(sequence.MemoryUsageInBytes * 5 < values.Length * (long)sizeof(long));
    }

    [Fact]
    public void Indexer_ShouldReadPastALongGapOfZeros()
    {
        // A million zeros and then long.MaxValue: L = 43 and the last value's high part is 2^20 - 1, so its one
        // sits past about a million zeros. The one-sample bracketing it spans that whole gap, which is the case
        // the rank directory's binary search exists for — a scan from the sample would cross 16,384 words.
        long[] values = new long[1_000_000];
        values[^1] = long.MaxValue;
        var sequence = new EliasFanoSequence(values);

        Assert.Equal(43, sequence.LowBitCount);
        Assert.Equal(0, sequence[999_998]);
        Assert.Equal(long.MaxValue, sequence[999_999]);
        Assert.Equal(999_999, sequence.CountLessThan(long.MaxValue));
        Assert.Equal(999_999, sequence.CountLessThan(1));
        Assert.Equal(999_999, sequence.IndexOf(long.MaxValue));
        Assert.True(sequence.TryGetLowerBound(1, out long bound));
        Assert.Equal(long.MaxValue, bound);
    }

    [Fact]
    public void Queries_ShouldSkipALongRunOfOnes_WhenEveryValueSharesAHighPart()
    {
        // A million repeats of one value whose high part is 1,000,001 (L = 20): the zero that ends that high
        // part sits after a million consecutive ones, so the zero-select bracketing it spans the whole run.
        const long Repeated = 1_000_001L << 20;
        long[] values = [.. Enumerable.Repeat(Repeated, 1_000_000)];
        var sequence = new EliasFanoSequence(values);

        Assert.Equal(20, sequence.LowBitCount);
        Assert.Equal(0, sequence.CountLessThan(Repeated));
        Assert.Equal(1_000_000, sequence.CountLessThan(Repeated + 1));
        Assert.Equal(0, sequence.IndexOf(Repeated));
        Assert.False(sequence.Contains(Repeated - 1));
        Assert.Equal(Repeated, sequence[999_999]);
    }
}
