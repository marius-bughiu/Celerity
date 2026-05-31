using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="ulong"/> keys using the Thomas Wang
/// 64-bit integer hash function.
/// </summary>
/// <remarks>
/// A non-cryptographic, invertible bit-mixer (Thomas Wang's <c>hash64shift</c>)
/// that provides strong avalanche for adversarially or heavily-clustered keys.
/// It is the <see cref="ulong"/> counterpart to <see cref="Int64WangHasher"/>:
/// the mixer uses only shifts and adds (no multiplies), so it is cheaper than
/// the two-multiply <see cref="UInt64Hasher"/> (which is the Murmur3
/// <c>fmix64</c> finalizer) while still giving every input bit influence over
/// the result. For any given 64-bit pattern it returns exactly what
/// <see cref="Int64WangHasher"/> returns for the same bits.
/// <para>
/// Prefer this hasher over <see cref="UInt64Hasher"/> when profiling shows
/// the two 64-bit multiplies of <c>fmix64</c> are a hot-path cost and the
/// keys are already reasonably uniform; escalate back to
/// <see cref="UInt64Hasher"/> for adversarial workloads that need maximum
/// avalanche.
/// </para>
/// <para>
/// Unlike the Murmur3 finalizer, this function does <em>not</em> map
/// <c>0</c> to <c>0</c>. The dictionaries store the out-of-band zero-key
/// entry without calling the hasher, so this does not collide with the
/// empty-slot sentinel.
/// </para>
/// </remarks>
public struct UInt64WangHasher : IHashProvider<ulong>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(ulong key)
    {
        ulong u = key;

        u = (~u) + (u << 21);
        u ^= u >> 24;
        u = (u + (u << 3)) + (u << 8);
        u ^= u >> 14;
        u = (u + (u << 2)) + (u << 4);
        u ^= u >> 28;
        u += u << 31;

        return (int)u;
    }
}
