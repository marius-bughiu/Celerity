using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="string"/> keys using the original FNV-1
/// <em>64-bit</em> algorithm (multiply-then-XOR) over the full UTF-16
/// representation of the string — both bytes of every character — xor-folded down
/// to a signed 32-bit result.
/// </summary>
/// <remarks>
/// This is the wide-accumulator counterpart to <see cref="StringFnV1Hasher"/>,
/// and the FNV-1 counterpart to <see cref="StringFnV1A64Hasher"/>: it folds the
/// same little-endian UTF-16 byte stream (low byte then high byte of every 16-bit
/// code unit), but accumulates into a 64-bit state with the FNV-1 64-bit
/// parameters (offset basis <c>14695981039346656037</c>, prime
/// <c>1099511628211</c>) instead of the 32-bit ones. FNV-1 and FNV-1a share the
/// same constants and differ only in the order of the two per-byte steps: FNV-1
/// multiplies first and XORs second (<c>hash = hash * prime; hash ^= b</c>),
/// while <see cref="StringFnV1A64Hasher"/> XORs first and multiplies second.
/// This hasher is provided for users who specifically want the original FNV-1
/// ordering at 64-bit width (for example, to match an external system that hashes
/// with FNV-1-64).
/// <para>
/// The ordering matters for avalanche. In FNV-1 the very last byte folded is only
/// XORed into the accumulator with no subsequent multiply to diffuse it, so a
/// change in the final byte propagates less thoroughly than in FNV-1a, whose last
/// operation is always a multiply. For this reason FNV-1a
/// (<see cref="StringFnV1A64Hasher"/>) is the generally preferred member of the
/// family and the recommended default; reach for <see cref="StringFnV164Hasher"/>
/// only when you specifically need FNV-1's ordering.
/// </para>
/// <para>
/// Carrying twice as many bits through the accumulation as
/// <see cref="StringFnV1Hasher"/> means intermediate values collide far less
/// often, so for longer keys and larger key sets the distribution holds up better
/// before the final fold. The result is reduced to 32 bits by xor-folding the high
/// half into the low half (<c>h ^ (h &gt;&gt; 32)</c>), the standard FNV
/// retraction, which keeps the extra high-half entropy in the returned value. Like
/// <see cref="StringFnV1Hasher"/> it folds the <em>full</em> 16-bit value of every
/// character (low byte then high byte), so it distinguishes characters that differ
/// only in their upper byte — for example <c>'A'</c> (<c>U+0041</c>) and
/// <c>'Ł'</c> (<c>U+0141</c>), which <see cref="StringFnV1AHasher"/> collides on.
/// </para>
/// <para>
/// It sits at the cheap, classic end of the <see cref="string"/> escalation
/// ladder, a peer of the FNV-1a variants: <see cref="StringFnV1AHasher"/>
/// (cheapest, low-byte only) → <see cref="StringFnV1AFullHasher"/> /
/// <see cref="StringFnV1A64Hasher"/> (FNV-1a, full Unicode width) →
/// <see cref="StringJenkinsOaatHasher"/> (cheap, full Unicode width, multiply-free,
/// stronger per-bit avalanche) → <see cref="StringMurmur3Hasher"/> and the
/// throughput-oriented strong family. Prefer <see cref="StringFnV1A64Hasher"/> for
/// general use; reach for <see cref="StringFnV164Hasher"/> when you need FNV-1's
/// ordering at the wider 64-bit width, and escalate to the strong family when FNV's
/// weaker avalanche pushes clustered or adversarial keys into long probe chains.
/// </para>
/// <para>
/// The empty string folds no characters, so it hashes to the 64-bit offset basis
/// xor-folded to 32 bits — exactly as <see cref="StringFnV1A64Hasher"/> maps the
/// empty string (FNV-1 and FNV-1a share the same offset basis). The dictionaries
/// store the out-of-band <c>null</c>-key entry without ever calling the hasher, so
/// this does not collide with the empty-slot sentinel.
/// </para>
/// <para>
/// The algorithm carries 64 bits of state internally, so the type also implements
/// <see cref="IHashProvider64{T}"/>: <see cref="Hash64"/> returns that state un-folded, which is
/// what the probabilistic sketches want (see <see cref="IHashProvider64{T}"/> for why the extra
/// 32 bits matter there and not in a hash table). <see cref="Hash(string)"/> is unchanged — it is
/// exactly <c>h ^ (h &gt;&gt; 32)</c> of the 64-bit result.
/// </para>
/// </remarks>
public struct StringFnV164Hasher : IHashProvider<string>, IHashProvider64<string>, ISpanHashProvider
{
    /// <summary>
    /// Computes the FNV-1 64-bit hash of the specified string over the full
    /// little-endian UTF-16 byte stream (both bytes of every character), xor-folded
    /// to a signed 32-bit result.
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit, xor-folded FNV-1 64-bit hash of <paramref name="key"/>.</returns>
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
        return Hash(key.AsSpan());
    }

    /// <summary>
    /// Computes the FNV-1 64-bit hash of the specified character span, xor-folded to a
    /// signed 32-bit result.
    /// </summary>
    /// <param name="key">The characters to hash.</param>
    /// <returns>
    /// The signed 32-bit, xor-folded FNV-1 hash of <paramref name="key"/> — the same
    /// value <see cref="Hash(string)"/> returns for a string with the same contents.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(ReadOnlySpan<char> key)
    {
        ulong h64 = Hash64Core(key);
        return unchecked((int)(h64 ^ (h64 >> 32)));
    }

    /// <summary>
    /// Computes the full 64-bit FNV-1 64-bit hash of the specified string over its
    /// native little-endian UTF-16 byte stream — the value <see cref="Hash(string)"/>
    /// xor-folds down to 32 bits.
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The 64-bit FNV-1 64-bit hash of <paramref name="key"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/> is <c>null</c>. Celerity dictionaries store the
    /// out-of-band <c>null</c>-key entry without calling the hasher, so this
    /// check only surfaces when the hasher is used directly or plugged into a
    /// consumer that does not handle <c>null</c> keys out-of-band.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Hash64(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Hash64Core(key.AsSpan());
    }

    // The single 64-bit body. Both Hash64(string) and the span-based Hash(ReadOnlySpan<char>)
    // route through it, so the string and span overloads cannot drift apart — the parity the
    // ISpanHashProvider contract requires.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Hash64Core(ReadOnlySpan<char> key)
    {
        // The FNV-1 64-bit parameters (identical to FNV-1a 64-bit).
        const ulong FnvPrime = 1099511628211UL;
        const ulong OffsetBasis = 14695981039346656037UL;

        ulong hash = OffsetBasis;
        foreach (char c in key)
        {
            // FNV-1 ordering: multiply first, then XOR the byte — the inverse of
            // StringFnV1A64Hasher's XOR-then-multiply. Fold the low byte then the
            // high byte of each 16-bit code unit, i.e. FNV-1 over the character's
            // little-endian UTF-16 bytes, but into a 64-bit accumulator. The
            // high-byte step keeps characters that share a low byte but differ in
            // their upper byte distinct.
            hash *= FnvPrime;
            hash ^= (byte)(c & 0xFF);
            hash *= FnvPrime;
            hash ^= (byte)(c >> 8);
        }

        // Xor-fold the 64-bit state down to 32 bits, keeping the high-half entropy.
        return hash;
    }
}
