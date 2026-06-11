using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A high-quality hash provider for <see cref="string"/> keys using the
/// MurmurHash3 x86_32 algorithm over the string's UTF-16 code units.
/// </summary>
/// <remarks>
/// This is the <see cref="string"/> counterpart to <see cref="Int32Murmur3Hasher"/>:
/// it gives strings the same "strong escalation" option that <c>int</c> and
/// <c>long</c> already have over their fast-fold defaults. Two properties set it
/// apart from <see cref="StringFnV1AHasher"/>:
/// <list type="bullet">
/// <item>
/// <description>
/// It consumes the <em>full</em> 16-bit value of every character — treating the
/// string as its native little-endian UTF-16 byte stream — whereas
/// <see cref="StringFnV1AHasher"/> hashes only the low byte of each character and
/// therefore cannot distinguish characters that differ only in their upper byte
/// (for example <c>'A'</c> (<c>U+0041</c>) and <c>'Ł'</c> (<c>U+0141</c>)).
/// </description>
/// </item>
/// <item>
/// <description>
/// The MurmurHash3 finalizer (<c>fmix32</c>) gives every input bit influence over
/// every output bit, so distribution holds up on clustered or adversarial keys
/// that would push FNV-1a into long probe chains.
/// </description>
/// </item>
/// </list>
/// Prefer it over <see cref="StringFnV1AHasher"/> for keys with significant
/// non-ASCII content, or when collision resistance matters more than the few
/// cycles FNV-1a saves on short ASCII keys.
/// <para>
/// The empty string hashes to <c>0</c> — the fixed point of <c>fmix32</c> over a
/// zero accumulator — exactly as the integer Murmur3 hashers map <c>0 → 0</c>.
/// The dictionaries store the out-of-band <c>null</c>-key entry without ever
/// calling the hasher, so this does not collide with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringMurmur3Hasher : IHashProvider<string>
{
    private const uint C1 = 0xcc9e2d51u;
    private const uint C2 = 0x1b873593u;

    /// <summary>
    /// Computes the MurmurHash3 x86_32 hash of the specified string over its
    /// UTF-16 code units.
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit MurmurHash3 hash of <paramref name="key"/>.</returns>
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

        uint h = 0;
        int length = key.Length;

        // Consume two chars (one 32-bit little-endian block) per iteration: the
        // first char supplies the low 16 bits, the second the high 16 bits, which
        // is exactly the 4-byte block a byte-oriented Murmur3 would read over the
        // native little-endian UTF-16 representation of the string.
        int i = 0;
        for (; i + 1 < length; i += 2)
        {
            uint block = (uint)key[i] | ((uint)key[i + 1] << 16);

            block *= C1;
            block = BitOperations.RotateLeft(block, 15);
            block *= C2;

            h ^= block;
            h = BitOperations.RotateLeft(h, 13);
            h = (h * 5u) + 0xe6546b64u;
        }

        // Tail: a single trailing char (2 bytes) when the length is odd.
        if ((length & 1) != 0)
        {
            uint tail = (uint)key[length - 1];

            tail *= C1;
            tail = BitOperations.RotateLeft(tail, 15);
            tail *= C2;

            h ^= tail;
        }

        // Finalize with the byte length, then the fmix32 avalanche.
        h ^= (uint)(length * 2);
        h ^= h >> 16;
        h *= 0x85ebca6bu;
        h ^= h >> 13;
        h *= 0xc2b2ae35u;
        h ^= h >> 16;

        return unchecked((int)h);
    }
}
