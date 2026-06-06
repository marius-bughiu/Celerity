using System.Text;

/// <summary>
/// The key shapes swept by the hasher benchmarks and the hash-quality report.
/// </summary>
public enum KeyShape
{
    /// <summary>Short lowercase-alphanumeric identifiers (6–12 chars), the common map-key case.</summary>
    ShortAscii,

    /// <summary>Long ASCII path / URL-like keys (48–80 chars).</summary>
    LongAscii,

    /// <summary>Shorter mixed Latin + CJK text (10–20 chars) that exercises the full-width fold.</summary>
    NonAscii,
}

/// <summary>
/// Deterministic, <b>distinct</b> key samples shared by <c>StringHasherBenchmark</c> /
/// <c>IntegerHasherBenchmark</c> (throughput) and the <c>--hash-quality</c> distribution report.
/// </summary>
/// <remarks>
/// Both views drive off the <em>same</em> sample (same seed, same shapes), so a hasher's measured
/// throughput and its measured distribution quality are directly comparable — the whole point of
/// pairing the two: a fast hasher that clusters is not a win. Keys are de-duplicated during
/// generation because <see cref="Celerity.Hashing.HashQualityEvaluator"/> counts duplicate keys as
/// collisions, which would otherwise skew the distribution metrics.
/// </remarks>
public static class HasherKeySamples
{
    /// <summary>The default sample size used by the benchmarks and the report.</summary>
    public const int DefaultCount = 2_000;

    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789_/";

    /// <summary>Builds <paramref name="count"/> distinct <see cref="string"/> keys of the given shape.</summary>
    public static string[] Strings(KeyShape shape, int count = DefaultCount)
    {
        // Deterministic seed so the sample is identical across runs, hashers, and the two views.
        Random rand = new(42);
        var set = new HashSet<string>(count);
        var keys = new string[count];
        int n = 0;
        while (n < count)
        {
            string key = shape switch
            {
                KeyShape.ShortAscii => MakeAscii(rand, rand.Next(6, 13)),
                KeyShape.LongAscii => MakeAscii(rand, rand.Next(48, 81)),
                KeyShape.NonAscii => MakeNonAscii(rand, rand.Next(10, 21)),
                _ => throw new ArgumentOutOfRangeException(nameof(shape)),
            };
            if (set.Add(key))
            {
                keys[n++] = key;
            }
        }
        return keys;
    }

    /// <summary>Builds <paramref name="count"/> distinct non-zero <see cref="int"/> keys.</summary>
    public static int[] Int32(int count = DefaultCount)
    {
        Random rand = new(42);
        var set = new HashSet<int>(count);
        var keys = new int[count];
        int n = 0;
        while (n < count)
        {
            int key = rand.Next(1, int.MaxValue);
            if (set.Add(key))
            {
                keys[n++] = key;
            }
        }
        return keys;
    }

    /// <summary>Builds <paramref name="count"/> distinct <see cref="long"/> keys.</summary>
    public static long[] Int64(int count = DefaultCount)
    {
        Random rand = new(42);
        var set = new HashSet<long>(count);
        var keys = new long[count];
        int n = 0;
        while (n < count)
        {
            long key = ((long)rand.Next() << 32) | (uint)rand.Next();
            if (key != 0 && set.Add(key))
            {
                keys[n++] = key;
            }
        }
        return keys;
    }

    /// <summary>Builds <paramref name="count"/> distinct non-zero <see cref="uint"/> keys.</summary>
    public static uint[] UInt32(int count = DefaultCount)
    {
        Random rand = new(42);
        var set = new HashSet<uint>(count);
        var keys = new uint[count];
        int n = 0;
        while (n < count)
        {
            uint key = (uint)rand.Next(1, int.MaxValue);
            if (set.Add(key))
            {
                keys[n++] = key;
            }
        }
        return keys;
    }

    /// <summary>Builds <paramref name="count"/> distinct non-zero <see cref="ulong"/> keys.</summary>
    public static ulong[] UInt64(int count = DefaultCount)
    {
        Random rand = new(42);
        var set = new HashSet<ulong>(count);
        var keys = new ulong[count];
        int n = 0;
        while (n < count)
        {
            ulong key = ((ulong)(uint)rand.Next() << 32) | (uint)rand.Next();
            if (key != 0 && set.Add(key))
            {
                keys[n++] = key;
            }
        }
        return keys;
    }

    /// <summary>Builds <paramref name="count"/> distinct <see cref="Guid"/> keys.</summary>
    public static Guid[] Guids(int count = DefaultCount)
    {
        Random rand = new(42);
        var set = new HashSet<Guid>(count);
        var keys = new Guid[count];
        byte[] bytes = new byte[16];
        int n = 0;
        while (n < count)
        {
            rand.NextBytes(bytes);
            var key = new Guid(bytes);
            if (set.Add(key))
            {
                keys[n++] = key;
            }
        }
        return keys;
    }

    private static string MakeAscii(Random rand, int length)
    {
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            sb.Append(Alphabet[rand.Next(Alphabet.Length)]);
        }
        return sb.ToString();
    }

    private static string MakeNonAscii(Random rand, int length)
    {
        // Mix Latin letters with CJK code points (U+4E00–U+9FFF) so both bytes of the
        // UTF-16 code unit vary — the case the full-width string hashers are built for.
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            sb.Append(rand.Next(2) == 0
                ? (char)('a' + rand.Next(26))
                : (char)(0x4E00 + rand.Next(0x9FFF - 0x4E00 + 1)));
        }
        return sb.ToString();
    }
}
