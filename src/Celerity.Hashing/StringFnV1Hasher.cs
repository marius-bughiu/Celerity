using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="string"/> keys using the original FNV-1 32-bit
/// algorithm (multiply-then-XOR) over the <em>full</em> UTF-16 representation of
/// the string — both bytes of every character.
/// </summary>
/// <remarks>
/// FNV-1 and FNV-1a share the same constants (offset basis <c>2166136261</c>,
/// prime <c>16777619</c>) and differ only in the order of the two per-byte steps:
/// FNV-1 multiplies first and XORs second (<c>hash = hash * prime; hash ^= b</c>),
/// while <see cref="StringFnV1AFullHasher"/> XORs first and multiplies second.
/// This hasher is the FNV-1 counterpart to <see cref="StringFnV1AFullHasher"/>,
/// provided for users who specifically want the original FNV-1 ordering.
/// <para>
/// The ordering matters for avalanche. In FNV-1 the very last byte folded is only
/// XORed into the accumulator with no subsequent multiply to diffuse it, so a
/// change in the final byte propagates less thoroughly than in FNV-1a, whose last
/// operation is always a multiply. For this reason FNV-1a is the generally
/// preferred member of the family and the recommended default; reach for
/// <see cref="StringFnV1Hasher"/> only when you specifically need FNV-1's ordering
/// (for example, to match an external system that hashes with FNV-1).
/// </para>
/// <para>
/// Unlike the low-byte <see cref="StringFnV1AHasher"/>, there is intentionally only
/// one FNV-1 variant and it folds the <em>full</em> 16-bit value of every
/// character (low byte then high byte) — exactly FNV-1 over the string's native
/// little-endian UTF-16 byte stream. It therefore distinguishes characters that
/// differ only in their upper byte — for example <c>'A'</c> (<c>U+0041</c>) and
/// <c>'Ł'</c> (<c>U+0141</c>), which <see cref="StringFnV1AHasher"/> collides on —
/// without re-introducing that low-byte fold's flaw, while still costing only a
/// pair of multiply / XOR steps per character.
/// </para>
/// <para>
/// It sits at the cheap, classic end of the <see cref="string"/> escalation
/// ladder, a peer of the FNV-1a variants: <see cref="StringDjb2Hasher"/> /
/// <see cref="StringFnV1Hasher"/> / <see cref="StringFnV1AHasher"/> (low-byte
/// only) → <see cref="StringFnV1AFullHasher"/> / <see cref="StringFnV1A64Hasher"/>
/// (FNV-1a, full Unicode width) → <see cref="StringJenkinsOaatHasher"/> (cheap,
/// full Unicode width, multiply-free, stronger per-bit avalanche) →
/// <see cref="StringMurmur3Hasher"/> and the throughput-oriented strong family.
/// Prefer <see cref="StringFnV1AFullHasher"/> for general use; escalate to the
/// strong family when FNV's weaker avalanche pushes clustered or adversarial keys
/// into long probe chains.
/// </para>
/// <para>
/// The empty string hashes to the FNV offset basis (<c>2166136261</c>,
/// <c>-2128831035</c> as a signed <see cref="int"/>) — no characters are folded,
/// exactly as <see cref="StringFnV1AFullHasher"/> maps the empty string. The
/// dictionaries store the out-of-band <c>null</c>-key entry without ever calling
/// the hasher, so this does not collide with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringFnV1Hasher : IHashProvider<string>
{
    /// <summary>
    /// Computes the FNV-1 32-bit hash of the specified string over the full
    /// little-endian UTF-16 byte stream (both bytes of every character).
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit FNV-1 hash of <paramref name="key"/>.</returns>
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

        // The FNV-1 32-bit parameters (identical to FNV-1a).
        const uint fnvPrime = 16777619u;
        const uint offsetBasis = 2166136261u;

        uint hash = offsetBasis;
        foreach (char c in key)
        {
            // FNV-1 ordering: multiply first, then XOR the byte — the inverse of
            // StringFnV1AFullHasher's XOR-then-multiply. Fold the low byte then the
            // high byte of each 16-bit code unit, i.e. FNV-1 over the character's
            // little-endian UTF-16 bytes. The high-byte step keeps characters that
            // share a low byte but differ in their upper byte distinct.
            hash *= fnvPrime;
            hash ^= (byte)(c & 0xFF);
            hash *= fnvPrime;
            hash ^= (byte)(c >> 8);
        }

        return unchecked((int)hash);
    }
}
