using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="long"/> keys using the Thomas Wang
/// 64-bit integer hash function.
/// </summary>
/// <remarks>
/// A non-cryptographic, invertible bit-mixer that provides excellent avalanche
/// for adversarially or heavily-clustered keys. The full mixer is roughly 5x
/// the per-call cost of <see cref="Int64WangNaiveHasher"/> (the default for
/// <c>Celerity.Collections.LongDictionary&lt;TValue&gt;</c> and
/// <c>Celerity.Collections.LongSet</c>), so switch to this hasher only
/// when profiling shows the lighter mixer is producing measurable clustering.
/// It still sits below <see cref="Int64Murmur3Hasher"/> on the
/// cost-vs-avalanche curve.
/// <para>
/// Unlike the Murmur3 finalizer, this function does <em>not</em> map
/// <c>0</c> to <c>0</c>. If your keys are heavily concentrated around zero,
/// measure both hashers before committing to one.
/// </para>
/// </remarks>
public struct Int64WangHasher : IHashProvider<long>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(long key)
    {
        ulong u = (ulong)key;

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
