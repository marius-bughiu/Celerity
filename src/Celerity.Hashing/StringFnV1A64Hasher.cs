using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="string"/> keys using the FNV-1a <em>64-bit</em>
/// algorithm over the full UTF-16 representation of the string — both bytes of
/// every character — xor-folded down to a signed 32-bit result.
/// </summary>
/// <remarks>
/// This is the wide-accumulator counterpart to <see cref="StringFnV1AFullHasher"/>:
/// it folds the same little-endian UTF-16 byte stream (low byte then high byte of
/// every 16-bit code unit), but accumulates into a 64-bit state with the FNV-1a
/// 64-bit parameters (offset basis <c>14695981039346656037</c>, prime
/// <c>1099511628211</c>) instead of the 32-bit ones. Carrying twice as many bits
/// through the accumulation means intermediate values collide far less often, so
/// for longer keys and larger key sets the distribution holds up better than the
/// 32-bit FNV-1a hashers before the final fold. The result is reduced to 32 bits
/// by xor-folding the high half into the low half (<c>h ^ (h &gt;&gt; 32)</c>),
/// the standard FNV retraction, which keeps the extra high-half entropy in the
/// returned value.
/// <para>
/// It sits one rung above <see cref="StringFnV1AFullHasher"/> on the
/// <see cref="string"/> escalation ladder:
/// <see cref="StringFnV1AHasher"/> (cheapest, low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> (cheap, full Unicode width, 32-bit state) →
/// <see cref="StringFnV1A64Hasher"/> (full Unicode width, 64-bit state) →
/// <see cref="StringMurmur3Hasher"/> (the <c>fmix32</c> finalizer gives every
/// input bit influence over every output bit). Like <see cref="StringFnV1AFullHasher"/>
/// it costs only a pair of XOR / multiply steps per character — but each multiply
/// is 64-bit, which on 64-bit platforms is the same throughput as a 32-bit
/// multiply, so the wider state is effectively free there. Prefer it over
/// <see cref="StringFnV1AFullHasher"/> when keys are long or numerous enough that
/// the narrower 32-bit accumulator starts clustering; escalate to
/// <see cref="StringMurmur3Hasher"/> when FNV-1a's weaker per-bit avalanche pushes
/// clustered or adversarial keys into long probe chains regardless of state width.
/// </para>
/// <para>
/// The empty string folds no characters, so it hashes to the 64-bit offset basis
/// xor-folded to 32 bits. The dictionaries store the out-of-band <c>null</c>-key
/// entry without ever calling the hasher, so this does not collide with the
/// empty-slot sentinel.
/// </para>
/// <para>
/// The algorithm carries 64 bits of state internally, so the type also implements
/// <see cref="IHashProvider64{T}"/>: <see cref="Hash64"/> returns that state un-folded, which is
/// what the probabilistic sketches want (see <see cref="IHashProvider64{T}"/> for why the extra
/// 32 bits matter there and not in a hash table). <see cref="Hash(string)"/> is unchanged — it is
/// exactly <c>h ^ (h &gt;&gt; 32)</c> of the 64-bit result.
/// </para>
/// </remarks>
public struct StringFnV1A64Hasher : IHashProvider<string>, IHashProvider64<string>, ISpanHashProvider
{
    /// <summary>
    /// Computes the FNV-1a 64-bit hash of the specified string over the full
    /// little-endian UTF-16 byte stream (both bytes of every character), xor-folded
    /// to a signed 32-bit result.
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit, xor-folded FNV-1a 64-bit hash of <paramref name="key"/>.</returns>
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
    /// Computes the FNV-1a 64-bit hash of the specified character span, xor-folded to a
    /// signed 32-bit result.
    /// </summary>
    /// <param name="key">The characters to hash.</param>
    /// <returns>
    /// The signed 32-bit, xor-folded FNV-1a hash of <paramref name="key"/> — the same
    /// value <see cref="Hash(string)"/> returns for a string with the same contents.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(ReadOnlySpan<char> key)
    {
        ulong h64 = Hash64Core(key);
        return unchecked((int)(h64 ^ (h64 >> 32)));
    }

    /// <summary>
    /// Computes the full 64-bit FNV-1a 64-bit hash of the specified string over its
    /// native little-endian UTF-16 byte stream — the value <see cref="Hash(string)"/>
    /// xor-folds down to 32 bits.
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The 64-bit FNV-1a 64-bit hash of <paramref name="key"/>.</returns>
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
        // The FNV-1a 64-bit parameters.
        const ulong fnvPrime = 1099511628211UL;
        const ulong offsetBasis = 14695981039346656037UL;

        ulong hash = offsetBasis;
        foreach (char c in key)
        {
            // Fold the low byte then the high byte of each 16-bit code unit —
            // i.e. FNV-1a over the character's little-endian UTF-16 bytes, exactly
            // as StringFnV1AFullHasher does, but into a 64-bit accumulator.
            hash ^= (byte)(c & 0xFF);
            hash *= fnvPrime;
            hash ^= (byte)(c >> 8);
            hash *= fnvPrime;
        }

        // Xor-fold the 64-bit state down to 32 bits, keeping the high-half entropy.
        return hash;
    }
}
