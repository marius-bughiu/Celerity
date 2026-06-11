using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="string"/> keys using the classic <em>PJW</em> /
/// <em>ELF</em> hash over the full little-endian UTF-16 byte stream of the string —
/// both bytes of every character.
/// </summary>
/// <remarks>
/// The PJW hash — Peter J. Weinberger's hash from the "Dragon Book" (Aho, Sethi,
/// Ullman, <em>Compilers: Principles, Techniques, and Tools</em>) — in the exact
/// 32-bit form standardized as the <c>elf_hash</c> used by the System V ABI for ELF
/// object-file symbol tables. The accumulator is seeded at <c>0</c>, and each input
/// byte <c>b</c> is folded with the step
/// <c>hash = (hash &lt;&lt; 4) + b; high = hash &amp; 0xF0000000; if (high != 0) hash ^= high &gt;&gt; 24; hash &amp;= ~high</c>.
/// The shift-by-4 walks new bytes up through the accumulator; whenever data reaches
/// the top nibble, those four bits are folded back down into bits 4–7 via
/// <c>high &gt;&gt; 24</c> and then cleared, so the state never overflows past 28 bits
/// and high-order entropy is recirculated rather than discarded. Like djb2 and sdbm
/// it uses no real multiply, no table, and no separate finalizer.
/// <para>
/// It is a peer of <see cref="StringDjb2Hasher"/> and <see cref="StringSdbmHasher"/>
/// at the cheapest, classic end of the ladder. Its high-nibble feedback gives it a
/// touch more diffusion than the pure shift-and-add classics — a changed byte that
/// has climbed into the top nibble is XORed back across the low byte — but it still
/// has no full avalanche step, so a single changed input byte propagates less
/// thoroughly than under <see cref="StringJenkinsOaatHasher"/>. Because the top
/// nibble is always cleared the result occupies only 28 bits, which is immaterial
/// once the dictionary masks the hash down to the table size but means the raw value
/// is never negative.
/// </para>
/// <para>
/// Like the other full-width string hashers it folds both bytes of every 16-bit
/// code unit (low byte then high byte) — exactly the ELF hash over the string's
/// native little-endian UTF-16 byte stream — so it distinguishes characters that
/// differ only in their upper byte, for example <c>'A'</c> (<c>U+0041</c>) and
/// <c>'Ł'</c> (<c>U+0141</c>), which <see cref="StringFnV1AHasher"/> collides on.
/// </para>
/// <para>
/// It sits at the cheapest, classic end of the <see cref="string"/> escalation
/// ladder, a peer to <see cref="StringDjb2Hasher"/>, <see cref="StringSdbmHasher"/>,
/// the FNV-1a variants, and <see cref="StringJenkinsOaatHasher"/>:
/// <see cref="StringDjb2Hasher"/> / <see cref="StringSdbmHasher"/> /
/// <see cref="StringElfHasher"/> (cheapest, multiply-free classics, weaker
/// avalanche) / <see cref="StringFnV1AHasher"/> (low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> / <see cref="StringFnV1A64Hasher"/> (cheap
/// FNV-1a, full Unicode width) → <see cref="StringJenkinsOaatHasher"/> (cheap, full
/// Unicode width, multiply-free, stronger per-bit avalanche than the classics or
/// FNV-1a) → <see cref="StringMurmur3Hasher"/> / <see cref="StringXxHash32Hasher"/> /
/// <see cref="StringXxHash64Hasher"/> / <see cref="StringMetroHash64Hasher"/> /
/// <see cref="StringCityHash64Hasher"/> (strong avalanche, maximum throughput).
/// Prefer it when you want a simple, familiar cheap hash and your keys are short
/// ASCII identifiers; step up to <see cref="StringJenkinsOaatHasher"/> (still cheap,
/// but with proper shift/xor diffusion) or the FNV-1a variants when the ELF hash's
/// weak avalanche starts clustering keys, and escalate to the throughput-oriented
/// strong family for clustered or adversarial keys.
/// </para>
/// <para>
/// The empty string hashes to the seed constant <c>0</c> — no bytes are folded.
/// The dictionaries store the out-of-band <c>null</c>-key entry without ever
/// calling the hasher, so this does not collide with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringElfHasher : IHashProvider<string>
{
    /// <summary>
    /// Computes the PJW / ELF hash of the specified string over the full
    /// little-endian UTF-16 byte stream (both bytes of every character).
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit ELF hash of <paramref name="key"/> (always in the
    /// range <c>[0, 0x0FFFFFFF]</c>, since the algorithm clears the top nibble).</returns>
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

        uint hash = 0u;
        foreach (char c in key)
        {
            // Fold the low byte then the high byte of each 16-bit code unit —
            // i.e. the ELF hash over the character's little-endian UTF-16 bytes.
            // The extra high-byte step keeps characters that share a low byte but
            // differ in their upper byte distinct, unlike StringFnV1AHasher.
            hash = Fold(hash, (byte)(c & 0xFF));
            hash = Fold(hash, (byte)(c >> 8));
        }

        // The top nibble is always cleared, so the result is in [0, 0x0FFFFFFF]
        // and the cast is value-preserving.
        return unchecked((int)hash);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Fold(uint hash, byte b)
    {
        hash = (hash << 4) + b;
        uint high = hash & 0xF0000000u;
        if (high != 0u)
        {
            // Recirculate the top nibble's entropy into the low byte before
            // discarding it, exactly as the System V ABI elf_hash does.
            hash ^= high >> 24;
        }

        // Clear the bits that were in the top nibble (~high clears exactly those).
        hash &= ~high;
        return hash;
    }
}
