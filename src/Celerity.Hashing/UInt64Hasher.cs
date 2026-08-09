using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// Obsolete alias for <see cref="UInt64Murmur3Hasher"/>, kept for source compatibility.
/// </summary>
/// <remarks>
/// The bare <c>UIntNN Hasher</c> name mapped to opposite tiers of the escalation ladder
/// across the two unsigned families: this type was the strong Murmur3 <c>fmix64</c> finalizer
/// for <see cref="ulong"/>, while <c>UInt32Hasher</c> was the cheap XOR-fold for
/// <see cref="uint"/>. A caller reasoning by analogy across widths — the way the rest of the
/// hasher surface is designed to be read — silently changed hash strength, not just key width.
/// The signed families never had the problem, because they name the algorithm in the type
/// (<see cref="Int64WangNaiveHasher"/> / <see cref="Int64WangHasher"/> /
/// <see cref="Int64Murmur3Hasher"/>); the unsigned ones now do too.
/// <para>
/// This alias forwards to <see cref="UInt64Murmur3Hasher"/> rather than repeating the
/// mixer, so the two cannot drift. It will be removed in a future major version.
/// </para>
/// </remarks>
[Obsolete("UInt64Hasher is the strong Murmur3 fmix64 finalizer, which its bare name does not " +
          "say — and the same bare name means the cheap XOR-fold for uint. Use " +
          "UInt64Murmur3Hasher, which names the algorithm and hashes identically. " +
          "This alias will be removed in a future major version.")]
public struct UInt64Hasher : IHashProvider<ulong>, IHashProvider64<ulong>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(ulong key) => default(UInt64Murmur3Hasher).Hash(key);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Hash64(ulong key) => default(UInt64Murmur3Hasher).Hash64(key);
}
