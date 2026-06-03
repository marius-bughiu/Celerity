using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A keyed, hash-flooding-resistant hash provider for <see cref="string"/> keys
/// using Google's HighwayHash (64-bit output) over the string's native
/// little-endian UTF-16 byte stream, xor-folded down to a signed 32-bit result.
/// </summary>
/// <remarks>
/// HighwayHash (Jyrki Alakuijala, Bill Cox &amp; Jan Wassenberg, 2016) is a keyed
/// pseudorandom hash family that Google designed as a faster successor to SipHash
/// for the same job — resisting <em>hash flooding</em>, where an adversary who
/// controls the keys floods a hash table with values that collide into one bucket
/// and degrade it to O(n) probe chains. Like <see cref="StringSipHash24Hasher"/> it
/// is <em>keyed</em>: an attacker cannot construct such a collision set without
/// recovering the secret key. Where SipHash is a compact add-rotate-xor lattice,
/// HighwayHash keeps a wide 256-bit-by-four-lane state (<c>v0</c>, <c>v1</c>,
/// <c>mul0</c>, <c>mul1</c>, four 64-bit words each) that it advances 32 bytes
/// (sixteen chars) at a time through per-lane 32&#215;32&#8594;64-bit multiplies, a
/// byte-shuffling <em>zipper merge</em>, and four permute-and-update finalization
/// rounds. That structure is built to map onto SIMD lanes (AVX2 / NEON), which is
/// where HighwayHash earns its throughput advantage over SipHash. This type is a
/// portable <em>scalar</em> implementation of that algorithm: it produces the exact
/// HighwayHash64 output (verified against the reference test vectors) and gives
/// every Celerity collection HighwayHash's strong keyed distribution; explicit SIMD
/// acceleration is a potential future optimization, so on a scalar code path do not
/// assume it out-runs the throughput-oriented unkeyed family below.
/// <para>
/// This places <see cref="StringHighwayHash64Hasher"/> at the keyed top of the
/// <see cref="string"/> escalation ladder, as a peer to
/// <see cref="StringSipHash24Hasher"/>:
/// <see cref="StringFnV1AHasher"/> (cheapest, low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> (cheap, full Unicode width, 32-bit state) →
/// <see cref="StringFnV1A64Hasher"/> (full Unicode width, 64-bit state) →
/// <see cref="StringMurmur3Hasher"/> / <see cref="StringXxHash32Hasher"/> /
/// <see cref="StringXxHash64Hasher"/> / <see cref="StringMetroHash64Hasher"/> /
/// <see cref="StringCityHash64Hasher"/> (strong avalanche, maximum throughput,
/// unkeyed) → <see cref="StringSipHash24Hasher"/> /
/// <see cref="StringHighwayHash64Hasher"/> (strong avalanche, <em>keyed</em>
/// hash-flooding resistance). The unkeyed throughput family is faster on trusted
/// keys and should be preferred there; reach for a keyed hasher when the keys come
/// from an untrusted source (request paths, header names, user-supplied
/// identifiers) and an adversary could otherwise deliberately drive worst-case
/// collisions. Between the two keyed options, <see cref="StringSipHash24Hasher"/> is
/// the compact, well-established default; <see cref="StringHighwayHash64Hasher"/> is
/// the SIMD-oriented alternative whose wider state pays off most once it is
/// vectorized. Like the other full-width string hashers it consumes the
/// <em>full</em> 16-bit value of every character — treating the string as its
/// native little-endian UTF-16 byte stream — so it distinguishes characters that
/// differ only in their upper byte (for example <c>'A'</c> (<c>U+0041</c>) and
/// <c>'Ł'</c> (<c>U+0141</c>), which <see cref="StringFnV1AHasher"/> collides on).
/// </para>
/// <para>
/// Because Celerity hashers are zero-state structs that collections construct via
/// <c>new THasher()</c>, this hasher carries a <em>fixed</em> built-in 256-bit key
/// (the canonical HighwayHash reference test key, bytes <c>00..1f</c> read as four
/// little-endian 64-bit words) rather than a per-process secret — exactly as
/// <see cref="StringSipHash24Hasher"/> does. That is enough to give every Celerity
/// collection HighwayHash's strong, well-distributed avalanche and to defeat
/// collision sets crafted against a weaker published algorithm, but a determined
/// attacker who knows the table uses this exact fixed key could still precompute
/// collisions against it. Callers who need full secret-keyed hash-flooding
/// resistance (a key the attacker cannot know) should seed a per-process key out of
/// band; this type's value is a strong, standards-based mixing function with a clear
/// upgrade path, not a turnkey secret.
/// </para>
/// <para>
/// <c>StringHighwayHash64Hasher.Hash(s)</c> equals canonical HighwayHash64 (with the
/// fixed key above) over <c>Encoding.Unicode.GetBytes(s)</c>, xor-folded to a signed
/// 32-bit integer (<c>h ^ (h &gt;&gt; 32)</c>, keeping the high-half entropy in the
/// result). The empty string maps to HighwayHash64's length-<c>0</c> output for that
/// key (the published reference vector <c>0x907A56DE22C26E53</c>) folded to 32 bits
/// (its UTF-16 byte stream is zero bytes). The dictionaries store the out-of-band
/// <c>null</c>-key entry without ever calling the hasher, so this does not collide
/// with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringHighwayHash64Hasher : IHashProvider<string>
{
    // Fixed 256-bit key: the canonical HighwayHash reference test key (bytes
    // 00..1f read as four little-endian 64-bit words). Fixed because collections
    // build the hasher via new THasher() and so cannot supply a per-process secret.
    private const ulong Key0 = 0x0706050403020100UL;
    private const ulong Key1 = 0x0F0E0D0C0B0A0908UL;
    private const ulong Key2 = 0x1716151413121110UL;
    private const ulong Key3 = 0x1F1E1D1C1B1A1918UL;

    /// <summary>
    /// Computes the HighwayHash64 hash of the specified string over its native
    /// little-endian UTF-16 byte stream (using this type's fixed built-in key),
    /// xor-folded to a signed 32-bit result.
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit, xor-folded HighwayHash64 hash of <paramref name="key"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/> is <c>null</c>. Celerity dictionaries store the
    /// out-of-band <c>null</c>-key entry without calling the hasher, so this
    /// check only surfaces when the hasher is used directly or plugged into a
    /// consumer that does not handle <c>null</c> keys out-of-band.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        // Four lanes of 64-bit state, held on the stack (no heap allocation).
        Span<ulong> v0 = stackalloc ulong[4];
        Span<ulong> v1 = stackalloc ulong[4];
        Span<ulong> mul0 = stackalloc ulong[4];
        Span<ulong> mul1 = stackalloc ulong[4];
        Reset(v0, v1, mul0, mul1);

        int charLen = key.Length;               // count of UTF-16 code units (chars)

        // Each 32-byte packet is sixteen chars (four 8-byte / four-char lanes).
        // Computed in chars to avoid overflowing int on a near-maximal string.
        int fullPackets = charLen >> 4;         // charLen / 16
        for (int pkt = 0; pkt < fullPackets; pkt++)
        {
            int baseChar = pkt << 4;            // pkt * 16
            Update(
                Lane(key, baseChar),
                Lane(key, baseChar + 4),
                Lane(key, baseChar + 8),
                Lane(key, baseChar + 12),
                v0, v1, mul0, mul1);
        }

        // The UTF-16 byte stream is always even-length, so the leftover after the
        // last full packet is an even number of bytes in [0, 30].
        int sizeMod32 = (charLen & 15) << 1;    // (charLen % 16) * 2 bytes
        if (sizeMod32 != 0)
            UpdateRemainder(key, fullPackets << 4, sizeMod32, v0, v1, mul0, mul1);

        // Finalization: four permute-and-update rounds, then sum the lane-0 words.
        for (int i = 0; i < 4; i++)
            PermuteAndUpdate(v0, v1, mul0, mul1);

        ulong h = v0[0] + v1[0] + mul0[0] + mul1[0];

        // Xor-fold the 64-bit state down to 32 bits, keeping the high-half entropy.
        return unchecked((int)(h ^ (h >> 32)));
    }

    // ── State initialization ─────────────────────────────────────────────────────

    /// <summary>
    /// Initializes the four lanes of state from the fixed key, exactly as the
    /// HighwayHash reference <c>HighwayHashReset</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Reset(Span<ulong> v0, Span<ulong> v1, Span<ulong> mul0, Span<ulong> mul1)
    {
        mul0[0] = 0xDBE6D5D5FE4CCE2FUL;
        mul0[1] = 0xA4093822299F31D0UL;
        mul0[2] = 0x13198A2E03707344UL;
        mul0[3] = 0x243F6A8885A308D3UL;
        mul1[0] = 0x3BD39E10CB0EF593UL;
        mul1[1] = 0xC0ACF169B5F18A8CUL;
        mul1[2] = 0xBE5466CF34E90C6CUL;
        mul1[3] = 0x452821E638D01377UL;

        v0[0] = mul0[0] ^ Key0;
        v0[1] = mul0[1] ^ Key1;
        v0[2] = mul0[2] ^ Key2;
        v0[3] = mul0[3] ^ Key3;
        v1[0] = mul1[0] ^ Rot32(Key0);
        v1[1] = mul1[1] ^ Rot32(Key1);
        v1[2] = mul1[2] ^ Rot32(Key2);
        v1[3] = mul1[3] ^ Rot32(Key3);
    }

    // ── Core update ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Advances the state by one 32-byte packet (four 64-bit lanes): the per-lane
    /// multiply-and-accumulate mix followed by the four zipper-merge steps.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Update(
        ulong l0, ulong l1, ulong l2, ulong l3,
        Span<ulong> v0, Span<ulong> v1, Span<ulong> mul0, Span<ulong> mul1)
    {
        MixLane(0, l0, v0, v1, mul0, mul1);
        MixLane(1, l1, v0, v1, mul0, mul1);
        MixLane(2, l2, v0, v1, mul0, mul1);
        MixLane(3, l3, v0, v1, mul0, mul1);

        ZipperMergeAndAdd(v1[1], v1[0], ref v0[1], ref v0[0]);
        ZipperMergeAndAdd(v1[3], v1[2], ref v0[3], ref v0[2]);
        ZipperMergeAndAdd(v0[1], v0[0], ref v1[1], ref v1[0]);
        ZipperMergeAndAdd(v0[3], v0[2], ref v1[3], ref v1[2]);
    }

    /// <summary>The per-lane multiply-and-accumulate step of <c>Update</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MixLane(
        int i, ulong lane,
        Span<ulong> v0, Span<ulong> v1, Span<ulong> mul0, Span<ulong> mul1)
    {
        v1[i] += mul0[i] + lane;
        mul0[i] ^= (v1[i] & 0xFFFFFFFFUL) * (v0[i] >> 32);
        v0[i] += mul1[i];
        mul1[i] ^= (v0[i] & 0xFFFFFFFFUL) * (v1[i] >> 32);
    }

    /// <summary>
    /// HighwayHash's zipper-merge: a fixed byte-shuffle of two state words added
    /// into two other state words. Transcribed verbatim from the reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ZipperMergeAndAdd(ulong v1, ulong v0, ref ulong add1, ref ulong add0)
    {
        add0 += (((v0 & 0xFF000000UL) | (v1 & 0xFF00000000UL)) >> 24)
              | (((v0 & 0xFF0000000000UL) | (v1 & 0xFF000000000000UL)) >> 16)
              | (v0 & 0xFF0000UL) | ((v0 & 0xFF00UL) << 32)
              | ((v1 & 0xFF00000000000000UL) >> 8) | (v0 << 56);
        add1 += (((v1 & 0xFF000000UL) | (v0 & 0xFF00000000UL)) >> 24)
              | (v1 & 0xFF0000UL) | ((v1 & 0xFF0000000000UL) >> 16)
              | ((v1 & 0xFF00UL) << 24) | ((v0 & 0xFF000000000000UL) >> 8)
              | ((v1 & 0xFFUL) << 48) | (v0 & 0xFF00000000000000UL);
    }

    /// <summary>
    /// The finalization step: permute the <c>v0</c> lanes (reorder and swap 32-bit
    /// halves) and feed them back through <c>Update</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PermuteAndUpdate(
        Span<ulong> v0, Span<ulong> v1, Span<ulong> mul0, Span<ulong> mul1)
    {
        Update(
            Rot32(v0[2]),
            Rot32(v0[3]),
            Rot32(v0[0]),
            Rot32(v0[1]),
            v0, v1, mul0, mul1);
    }

    // ── Tail (partial packet) ─────────────────────────────────────────────────────

    /// <summary>
    /// Folds the leftover 1–30 trailing bytes into the state, mirroring the
    /// reference <c>UpdateRemainder</c> byte-for-byte: it mixes the remainder length
    /// into <c>v0</c>, rotates <c>v1</c> by that length, packs the leftover bytes
    /// into a zero-filled 32-byte packet under the same alignment rules, and runs
    /// one more <c>Update</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateRemainder(
        string key, int tailStartChar, int sizeMod32,
        Span<ulong> v0, Span<ulong> v1, Span<ulong> mul0, Span<ulong> mul1)
    {
        int sizeMod4 = sizeMod32 & 3;           // 0 or 2 on an always-even byte stream
        int rem = sizeMod32 & ~3;               // count of leading bytes copied directly

        for (int i = 0; i < 4; i++)
            v0[i] += ((ulong)sizeMod32 << 32) + (ulong)sizeMod32;

        Rotate32By((uint)sizeMod32, v1);

        Span<byte> packet = stackalloc byte[32];
        packet.Clear();
        for (int i = 0; i < rem; i++)
            packet[i] = TailByte(key, tailStartChar, i);

        if ((sizeMod32 & 16) != 0)
        {
            for (int i = 0; i < 4; i++)
                packet[28 + i] = TailByte(key, tailStartChar, rem + i + sizeMod4 - 4);
        }
        else if (sizeMod4 != 0)
        {
            packet[16] = TailByte(key, tailStartChar, rem);
            packet[17] = TailByte(key, tailStartChar, rem + (sizeMod4 >> 1));
            packet[18] = TailByte(key, tailStartChar, rem + sizeMod4 - 1);
        }

        Update(
            BinaryPrimitives.ReadUInt64LittleEndian(packet),
            BinaryPrimitives.ReadUInt64LittleEndian(packet[8..]),
            BinaryPrimitives.ReadUInt64LittleEndian(packet[16..]),
            BinaryPrimitives.ReadUInt64LittleEndian(packet[24..]),
            v0, v1, mul0, mul1);
    }

    /// <summary>
    /// Rotates each 32-bit half of every <c>v1</c> lane left by <paramref name="count"/>
    /// (the remainder length in bytes, always in [1, 31]), mirroring the reference
    /// <c>Rotate32By</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Rotate32By(uint count, Span<ulong> v1)
    {
        for (int i = 0; i < 4; i++)
        {
            uint half0 = (uint)v1[i];
            uint half1 = (uint)(v1[i] >> 32);
            uint r0 = (half0 << (int)count) | (half0 >> (int)(32 - count));
            uint r1 = (half1 << (int)count) | (half1 >> (int)(32 - count));
            v1[i] = r0 | ((ulong)r1 << 32);
        }
    }

    // ── Primitives ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads one 64-bit little-endian word (eight bytes / four UTF-16 code units)
    /// starting at char index <paramref name="i"/>: each successive char supplies the
    /// next 16 bits — exactly the 8-byte word a byte-oriented HighwayHash would read
    /// over the native little-endian UTF-16 stream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Lane(string key, int i) =>
        (ulong)key[i]
        | ((ulong)key[i + 1] << 16)
        | ((ulong)key[i + 2] << 32)
        | ((ulong)key[i + 3] << 48);

    /// <summary>
    /// Reads the byte at offset <paramref name="b"/> into the tail's native
    /// little-endian UTF-16 byte stream (the tail starts at char index
    /// <paramref name="tailStartChar"/>): even offsets read a char's low byte, odd
    /// offsets its high byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte TailByte(string key, int tailStartChar, int b) =>
        (byte)(key[tailStartChar + (b >> 1)] >> ((b & 1) << 3));

    /// <summary>Rotates a 64-bit value by 32 bits (swaps its two 32-bit halves).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Rot32(ulong x) => (x >> 32) | (x << 32);
}
