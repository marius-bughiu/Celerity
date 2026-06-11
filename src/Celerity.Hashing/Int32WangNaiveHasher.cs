using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A fast hash provider for <see cref="int"/> keys using a Wang/Jenkins-style
/// integer bit-mixer.
/// </summary>
/// <remarks>
/// This is a lightweight finalizer that folds the high bits of the integer into
/// the low bits (<c>key ^ (key &gt;&gt; 16)</c>). It is extremely fast but has modest
/// avalanche properties compared to a full Murmur3 finalizer. Prefer it when
/// key distribution is already reasonably uniform and latency matters more than
/// collision resistance.
/// </remarks>
public struct Int32WangNaiveHasher : IHashProvider<int>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(int key) => key ^ (key >> 16);
}
