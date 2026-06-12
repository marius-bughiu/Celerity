using System;
using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// Fast, allocation-free GUID generation from a caller-supplied struct PRNG: a non-cryptographic
/// <strong>version&#160;4</strong> (fully random) and an RFC&#160;9562 <strong>version&#160;7</strong>
/// (Unix-millisecond time-ordered, big-endian, sortable), filling the byte buffer from a single
/// <see cref="IRandomSource"/> rather than the OS cryptographic RNG.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Not cryptographically secure.</strong> Both methods draw their random bits from the supplied
/// PRNG (e.g. <see cref="Xoshiro256StarStar"/>, <see cref="WyRand"/>), <em>not</em> from
/// <see cref="System.Security.Cryptography.RandomNumberGenerator"/>. They are for high-rate
/// <strong>trace / correlation / ephemeral IDs</strong> where uniqueness — not unpredictability — is the
/// requirement, and run several times faster than <see cref="Guid.NewGuid()"/> (which is RNG-backed). When
/// the identifier must be unguessable (security tokens, password-reset links, anything an attacker could
/// abuse if predicted), use <see cref="Guid.NewGuid()"/> or
/// <see cref="System.Security.Cryptography.RandomNumberGenerator"/> instead.
/// </para>
/// <para>
/// <strong>Version&#160;7 is RFC&#160;9562 big-endian.</strong> The 48-bit Unix-millisecond timestamp occupies
/// the most-significant bytes in network byte order, so the GUID's canonical string form sorts in creation
/// order — the property that keeps database B-tree indexes compact and locally inserted. .NET&#160;9's
/// <c>Guid.CreateVersion7</c> stores the timestamp in the mixed-endian in-memory <see cref="Guid"/> layout,
/// which scrambles the lexical/database sort order versus the spec; <see cref="CreateVersion7{TRng}"/> emits
/// the on-the-wire big-endian layout so <c>ToString()</c> ordering matches time ordering. Within a single
/// millisecond the stateless method orders by random bits only; use <see cref="GuidV7Generator{TRng}"/> for
/// a strictly monotonic same-millisecond sequence.
/// </para>
/// <para>
/// The methods are <c>ref</c>-generic over <c>TRng : struct, IRandomSource</c>, so the generator is
/// devirtualized and inlined and no allocation occurs — the same zero-cost pattern the struct hashers and
/// <see cref="RandomSourceExtensions"/> use. Pass a mutable generator variable (the state advances in place).
/// </para>
/// </remarks>
public static class FastGuid
{
    /// <summary>
    /// Creates a non-cryptographic random <strong>version&#160;4</strong> GUID, filling all 122 free bits
    /// from <paramref name="rng"/> and setting the version (<c>4</c>) and variant (RFC&#160;4122) fields.
    /// </summary>
    /// <typeparam name="TRng">The concrete generator type.</typeparam>
    /// <param name="rng">The generator, advanced in place.</param>
    /// <returns>A version&#160;4 GUID. <strong>Not cryptographically secure</strong> — see the type remarks.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid CreateVersion4<TRng>(ref TRng rng) where TRng : struct, IRandomSource
    {
        Span<byte> b = stackalloc byte[16];
        WriteUInt64BigEndian(b, rng.NextUInt64());
        WriteUInt64BigEndian(b.Slice(8), rng.NextUInt64());

        // Version 4 in the high nibble of byte 6; RFC 4122 variant (10xx) in the high bits of byte 8.
        b[6] = (byte)((b[6] & 0x0F) | 0x40);
        b[8] = (byte)((b[8] & 0x3F) | 0x80);
        return FromBigEndian(b);
    }

    /// <summary>
    /// Creates an RFC&#160;9562 <strong>version&#160;7</strong> GUID for the given Unix-millisecond timestamp:
    /// the 48-bit time prefix in big-endian byte order followed by 74 random bits from <paramref name="rng"/>.
    /// </summary>
    /// <typeparam name="TRng">The concrete generator type.</typeparam>
    /// <param name="rng">The generator, advanced in place.</param>
    /// <param name="unixTimeMilliseconds">The Unix timestamp in milliseconds; only the low 48 bits are stored
    /// (valid through year&#160;10889). Pass <c>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()</c> for the
    /// current time.</param>
    /// <returns>A time-ordered, sortable version&#160;7 GUID. <strong>Not cryptographically secure</strong>, and
    /// not strictly monotonic within a single millisecond (use <see cref="GuidV7Generator{TRng}"/> for that).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid CreateVersion7<TRng>(ref TRng rng, long unixTimeMilliseconds)
        where TRng : struct, IRandomSource
    {
        // rand_a: 12 random bits; rand_b: 62 random bits — both from one 64-bit draw plus a 32-bit one.
        uint randA = (uint)(rng.NextUInt32() & 0x0FFFu);
        ulong randB = rng.NextUInt64();
        return AssembleVersion7(unixTimeMilliseconds, randA, randB);
    }

    /// <summary>
    /// Assembles a version&#160;7 GUID from a 48-bit timestamp, a 12-bit <c>rand_a</c> field (random or a
    /// monotonic counter), and 62 bits drawn from <paramref name="randB"/>, in RFC&#160;9562 big-endian layout.
    /// </summary>
    internal static Guid AssembleVersion7(long unixTimeMilliseconds, uint randA, ulong randB)
    {
        ulong ms = (ulong)unixTimeMilliseconds;
        Span<byte> b = stackalloc byte[16];

        // unix_ts_ms: 48 bits, most-significant byte first (big-endian).
        b[0] = (byte)(ms >> 40);
        b[1] = (byte)(ms >> 32);
        b[2] = (byte)(ms >> 24);
        b[3] = (byte)(ms >> 16);
        b[4] = (byte)(ms >> 8);
        b[5] = (byte)ms;

        // ver (0111) + rand_a high 4 bits, then rand_a low 8 bits.
        b[6] = (byte)(0x70 | ((randA >> 8) & 0x0F));
        b[7] = (byte)randA;

        // variant (10) + rand_b high 6 bits, then rand_b low 56 bits.
        b[8] = (byte)(0x80 | (byte)((randB >> 56) & 0x3F));
        b[9] = (byte)(randB >> 48);
        b[10] = (byte)(randB >> 40);
        b[11] = (byte)(randB >> 32);
        b[12] = (byte)(randB >> 24);
        b[13] = (byte)(randB >> 16);
        b[14] = (byte)(randB >> 8);
        b[15] = (byte)randB;

        return FromBigEndian(b);
    }

    /// <summary>
    /// Constructs a <see cref="Guid"/> whose canonical string / big-endian byte representation is exactly the
    /// 16 supplied bytes in order, by feeding the first three fields to the field constructor in big-endian.
    /// </summary>
    /// <remarks>
    /// A <see cref="Guid"/> stores its first three fields (<c>Data1</c> <see cref="int"/>, <c>Data2</c> /
    /// <c>Data3</c> <see cref="short"/>) in native byte order but <em>renders</em> them most-significant-first.
    /// Passing the big-endian reads of bytes 0–7 makes <c>ToString()</c> / <c>ToString("N")</c> emit the bytes
    /// in the given order — the RFC&#160;9562 on-the-wire ordering — without needing the .NET&#160;9
    /// <c>Guid(ReadOnlySpan&lt;byte&gt;, bool bigEndian)</c> constructor (the library's lowest target is net8.0,
    /// so shared code stays on the field constructor rather than gating a net9+ path here).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Guid FromBigEndian(ReadOnlySpan<byte> b)
    {
        uint a = ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
        short c = (short)((b[4] << 8) | b[5]);
        short d = (short)((b[6] << 8) | b[7]);
        return new Guid((int)a, c, d, b[8], b[9], b[10], b[11], b[12], b[13], b[14], b[15]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteUInt64BigEndian(Span<byte> destination, ulong value)
    {
        destination[0] = (byte)(value >> 56);
        destination[1] = (byte)(value >> 48);
        destination[2] = (byte)(value >> 40);
        destination[3] = (byte)(value >> 32);
        destination[4] = (byte)(value >> 24);
        destination[5] = (byte)(value >> 16);
        destination[6] = (byte)(value >> 8);
        destination[7] = (byte)value;
    }
}
