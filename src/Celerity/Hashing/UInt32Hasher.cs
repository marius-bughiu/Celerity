using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A fast hash provider for <see cref="uint"/> keys using a Wang/Jenkins-style
/// integer bit-mixer.
/// </summary>
/// <remarks>
/// This is the <see cref="uint"/> counterpart to <see cref="Int32WangNaiveHasher"/>.
/// It folds the high bits of the value into the low bits (<c>key ^ (key &gt;&gt; 16)</c>),
/// then reinterprets the 32-bit result as a signed integer. Prefer this when
/// key distribution is already reasonably uniform and latency matters more than
/// collision resistance; use a full Murmur3 finalizer for clustered or
/// adversarial inputs.
/// </remarks>
public struct UInt32Hasher : IHashProvider<uint>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(uint key) => (int)(key ^ (key >> 16));
}
