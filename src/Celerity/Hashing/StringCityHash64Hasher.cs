using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A high-quality, throughput-oriented hash provider for <see cref="string"/>
/// keys using Google's CityHash64 algorithm (the <c>CityHash64</c> entry point of
/// the v1.1 reference) over the string's native little-endian UTF-16 byte stream,
/// xor-folded down to a signed 32-bit result.
/// </summary>
/// <remarks>
/// CityHash is a family of strong, fast non-cryptographic hash functions from
/// Google, designed around 64-bit multiplies and rotates for high throughput on
/// modern CPUs. Like <see cref="StringXxHash64Hasher"/> and
/// <see cref="StringMetroHash64Hasher"/> it carries 64-bit internal state, but its
/// structure is length-classed rather than a single stripe loop: inputs of 16
/// bytes or fewer, 17–32 bytes, and 33–64 bytes each take a dedicated branch of
/// multiply / rotate / <c>ShiftMix</c> steps, and only inputs over 64 bytes enter
/// the 56-byte-state main loop (two <c>WeakHashLen32WithSeeds</c> blocks plus
/// <c>x</c>, <c>y</c>, <c>z</c>) consuming 64 bytes (thirty-two chars) per
/// iteration. This makes it especially strong on the short-to-mid key lengths
/// typical of identifiers and dictionary keys, where a stripe-oriented hash spends
/// most of its time in tail handling. The 64-bit result is reduced to 32 bits by
/// xor-folding the high half into the low half (<c>h ^ (h &gt;&gt; 32)</c>),
/// keeping the extra high-half entropy in the returned value, exactly as
/// <see cref="StringXxHash64Hasher"/> and <see cref="StringMetroHash64Hasher"/> do.
/// <para>
/// It sits at the strong-distribution top of the <see cref="string"/> escalation
/// ladder alongside <see cref="StringMurmur3Hasher"/>,
/// <see cref="StringXxHash32Hasher"/>, <see cref="StringXxHash64Hasher"/>, and
/// <see cref="StringMetroHash64Hasher"/>:
/// <see cref="StringFnV1AHasher"/> (cheapest, low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> (cheap, full Unicode width, 32-bit state) →
/// <see cref="StringFnV1A64Hasher"/> (full Unicode width, 64-bit state) →
/// <see cref="StringMurmur3Hasher"/> / <see cref="StringXxHash32Hasher"/> /
/// <see cref="StringXxHash64Hasher"/> / <see cref="StringMetroHash64Hasher"/> /
/// <see cref="StringCityHash64Hasher"/> (strong avalanche).
/// <see cref="StringCityHash64Hasher"/>, <see cref="StringMetroHash64Hasher"/>,
/// <see cref="StringXxHash64Hasher"/>, and <see cref="StringXxHash32Hasher"/> are
/// peers at the throughput-oriented top of the ladder — profile on your own key
/// shape to pick between them; CityHash's length-classed short paths often edge
/// ahead on the short keys where the stripe-oriented hashers pay more fixed tail
/// overhead, while <see cref="StringMurmur3Hasher"/> remains the simplest choice
/// for very short keys. Like the other full-width string hashers it consumes the
/// <em>full</em> 16-bit value of every character — treating the string as its
/// native little-endian UTF-16 byte stream — so it distinguishes characters that
/// differ only in their upper byte (for example <c>'A'</c> (<c>U+0041</c>) and
/// <c>'Ł'</c> (<c>U+0141</c>), which <see cref="StringFnV1AHasher"/> collides on).
/// All are good answers when FNV-1a's weaker avalanche pushes clustered or
/// adversarial keys into long probe chains.
/// </para>
/// <para>
/// <c>StringCityHash64Hasher.Hash(s)</c> equals canonical CityHash64 (v1.1) over
/// <c>Encoding.Unicode.GetBytes(s)</c>, xor-folded to a signed 32-bit integer. The
/// empty string maps to the algorithm's length-<c>0</c> result — the constant
/// <c>k2</c> (<c>0x9AE16A3B2F90404F</c>) — folded to 32 bits (its UTF-16 byte
/// stream is zero bytes, so the shortest length class returns <c>k2</c> directly).
/// The dictionaries store the out-of-band <c>null</c>-key entry without ever
/// calling the hasher, so this does not collide with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringCityHash64Hasher : IHashProvider<string>
{
    // CityHash v1.1 mixing constants (Geoff Pike & Jyrki Alakuijala, public domain).
    private const ulong K0 = 0xC3A5C85C97CB3127UL;
    private const ulong K1 = 0xB492B66FBE98F273UL;
    private const ulong K2 = 0x9AE16A3B2F90404FUL;

    // The 128->64 mixing multiplier used by HashLen16.
    private const ulong KMul = 0x9DDFEA08EB382D69UL;

    /// <summary>
    /// Computes the CityHash64 (v1.1) hash of the specified string over its native
    /// little-endian UTF-16 byte stream, xor-folded to a signed 32-bit result.
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit, xor-folded CityHash64 hash of <paramref name="key"/>.</returns>
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

        int charLen = key.Length;               // count of UTF-16 code units (chars)
        ulong byteLen = (ulong)charLen * 2UL;   // CityHash operates on the byte length

        ulong h = byteLen <= 32UL
            ? (byteLen <= 16UL ? HashLen0to16(key, charLen, byteLen)
                               : HashLen17to32(key, charLen, byteLen))
            : (byteLen <= 64UL ? HashLen33to64(key, charLen, byteLen)
                               : HashLong(key, charLen, byteLen));

        // Xor-fold the 64-bit state down to 32 bits, keeping the high-half entropy.
        return unchecked((int)(h ^ (h >> 32)));
    }

    // ── Length-classed hash bodies (mirror CityHash64's dispatch) ───────────────

    private static ulong HashLen0to16(string key, int charLen, ulong byteLen)
    {
        if (byteLen >= 8UL)
        {
            ulong mul = K2 + byteLen * 2UL;
            ulong a = Lane(key, 0) + K2;
            ulong b = Lane(key, charLen - 4);          // s + len - 8
            ulong c = BitOperations.RotateRight(b, 37) * mul + a;
            ulong d = (BitOperations.RotateRight(a, 25) + b) * mul;
            return HashLen16(c, d, mul);
        }

        if (byteLen >= 4UL)
        {
            ulong mul = K2 + byteLen * 2UL;
            ulong a = Block(key, 0);
            return HashLen16(byteLen + (a << 3), Block(key, charLen - 2), mul); // s + len - 4
        }

        if (byteLen > 0UL)
        {
            // The UTF-16 byte stream is always even-length, so the only input that
            // reaches here is a single char (two bytes): s[0] is its low byte and
            // s[1] (= s[len >> 1] = s[len - 1]) is its high byte.
            char ch = key[0];
            uint lowByte = (uint)(byte)(ch & 0xFF);
            uint highByte = (uint)(byte)(ch >> 8);
            uint y = lowByte + (highByte << 8);
            uint z = (uint)byteLen + (highByte << 2);
            return ShiftMix((ulong)y * K2 ^ (ulong)z * K0) * K2;
        }

        return K2;
    }

    private static ulong HashLen17to32(string key, int charLen, ulong byteLen)
    {
        ulong mul = K2 + byteLen * 2UL;
        ulong a = Lane(key, 0) * K1;
        ulong b = Lane(key, 4);                         // s + 8
        ulong c = Lane(key, charLen - 4) * mul;         // s + len - 8
        ulong d = Lane(key, charLen - 8) * K2;          // s + len - 16
        return HashLen16(
            BitOperations.RotateRight(a + b, 43) + BitOperations.RotateRight(c, 30) + d,
            a + BitOperations.RotateRight(b + K2, 18) + c,
            mul);
    }

    private static ulong HashLen33to64(string key, int charLen, ulong byteLen)
    {
        ulong mul = K2 + byteLen * 2UL;
        ulong a = Lane(key, 0) * K2;
        ulong b = Lane(key, 4);                          // s + 8
        ulong c = Lane(key, charLen - 12);               // s + len - 24
        ulong d = Lane(key, charLen - 16);               // s + len - 32
        ulong e = Lane(key, 8) * K2;                     // s + 16
        ulong f = Lane(key, 12) * 9UL;                   // s + 24
        ulong g = Lane(key, charLen - 4);                // s + len - 8
        ulong h = Lane(key, charLen - 8) * mul;          // s + len - 16

        ulong u = BitOperations.RotateRight(a + g, 43) + (BitOperations.RotateRight(b, 30) + c) * 9UL;
        ulong v = ((a + g) ^ d) + f + 1UL;
        ulong w = BinaryPrimitives.ReverseEndianness((u + v) * mul) + h;
        ulong x = BitOperations.RotateRight(e + f, 42) + c;
        ulong y = (BinaryPrimitives.ReverseEndianness((v + w) * mul) + g) * mul;
        ulong z = e + f + c;
        a = BinaryPrimitives.ReverseEndianness((x + z) * mul + y) + b;
        b = ShiftMix((z + a) * mul + d + h) * mul;
        return b + x;
    }

    private static ulong HashLong(string key, int charLen, ulong byteLen)
    {
        // For strings over 64 bytes CityHash hashes the end first, then keeps 56
        // bytes of state (v, w, x, y, z) and consumes 64 bytes (thirty-two chars)
        // per loop iteration. All end-relative byte offsets are multiples of eight,
        // hence multiples of four chars, so every lane read stays char-aligned.
        ulong x = Lane(key, charLen - 20);               // s + len - 40
        ulong y = Lane(key, charLen - 8) + Lane(key, charLen - 28); // (len - 16) + (len - 56)
        ulong z = HashLen16(Lane(key, charLen - 24) + byteLen, Lane(key, charLen - 12)); // (len - 48) + len, (len - 24)

        (ulong vFirst, ulong vSecond) = WeakHashLen32WithSeeds(key, charLen - 32, byteLen, z); // s + len - 64
        (ulong wFirst, ulong wSecond) = WeakHashLen32WithSeeds(key, charLen - 16, y + K1, x);  // s + len - 32
        x = x * K1 + Lane(key, 0);

        // Decrease the byte length to the nearest multiple of 64 and walk 64-byte
        // (thirty-two-char) chunks from the front.
        ulong len = (byteLen - 1UL) & ~63UL;
        int p = 0; // char index of the current 64-byte chunk
        do
        {
            x = BitOperations.RotateRight(x + y + vFirst + Lane(key, p + 4), 37) * K1;   // s + 8
            y = BitOperations.RotateRight(y + vSecond + Lane(key, p + 24), 42) * K1;     // s + 48
            x ^= wSecond;
            y += vFirst + Lane(key, p + 20);                                             // s + 40
            z = BitOperations.RotateRight(z + wFirst, 33) * K1;
            (vFirst, vSecond) = WeakHashLen32WithSeeds(key, p, vSecond * K1, x + wFirst); // s
            (wFirst, wSecond) = WeakHashLen32WithSeeds(key, p + 16, z + wSecond, y + Lane(key, p + 8)); // s + 32, s + 16
            (z, x) = (x, z);
            p += 32;
            len -= 64UL;
        }
        while (len != 0UL);

        return HashLen16(
            HashLen16(vFirst, wFirst) + ShiftMix(y) * K1 + z,
            HashLen16(vSecond, wSecond) + x);
    }

    // ── Primitives ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads one 64-bit little-endian word (eight bytes / four UTF-16 code units)
    /// starting at char index <paramref name="i"/>: each successive char supplies
    /// the next 16 bits — exactly the 8-byte word a byte-oriented CityHash would
    /// read over the native little-endian UTF-16 stream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Lane(string key, int i) =>
        (ulong)key[i]
        | ((ulong)key[i + 1] << 16)
        | ((ulong)key[i + 2] << 32)
        | ((ulong)key[i + 3] << 48);

    /// <summary>
    /// Reads one 32-bit little-endian word (four bytes / two UTF-16 code units)
    /// starting at char index <paramref name="i"/>: the low char supplies the low
    /// 16 bits, the next char the high 16 bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Block(string key, int i) =>
        (uint)key[i] | ((uint)key[i + 1] << 16);

    /// <summary>CityHash's <c>ShiftMix</c>: <c>val ^ (val &gt;&gt; 47)</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ShiftMix(ulong val) => val ^ (val >> 47);

    /// <summary>
    /// CityHash's <c>HashLen16(u, v)</c> — the 128-&gt;64 finalizer over a multiply
    /// constant: <c>a = (u ^ v) * mul; a ^= a &gt;&gt; 47; b = (v ^ a) * mul;
    /// b ^= b &gt;&gt; 47; b *= mul</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong HashLen16(ulong u, ulong v, ulong mul)
    {
        ulong a = (u ^ v) * mul;
        a ^= a >> 47;
        ulong b = (v ^ a) * mul;
        b ^= b >> 47;
        b *= mul;
        return b;
    }

    /// <summary>The two-argument <c>HashLen16</c>, which mixes over the fixed <c>kMul</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong HashLen16(ulong u, ulong v) => HashLen16(u, v, KMul);

    /// <summary>
    /// CityHash's <c>WeakHashLen32WithSeeds</c> over the four 64-bit words starting
    /// at char index <paramref name="i"/> (a 32-byte / sixteen-char window) and two
    /// seeds, returning the two-word result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (ulong, ulong) WeakHashLen32WithSeeds(string key, int i, ulong a, ulong b)
    {
        ulong w = Lane(key, i);
        ulong x = Lane(key, i + 4);
        ulong y = Lane(key, i + 8);
        ulong z = Lane(key, i + 12);

        a += w;
        b = BitOperations.RotateRight(b + a + z, 21);
        ulong c = a;
        a += x;
        a += y;
        b += BitOperations.RotateRight(a, 44);
        return (a + z, b + c);
    }
}
