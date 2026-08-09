using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// Obsolete alias for <see cref="UInt32WangNaiveHasher"/>, kept for source compatibility.
/// </summary>
/// <remarks>
/// The bare <c>UIntNN Hasher</c> name mapped to opposite tiers of the escalation ladder
/// across the two unsigned families: this type was the cheap XOR-fold for <see cref="uint"/>,
/// while <c>UInt64Hasher</c> was the strong Murmur3 <c>fmix64</c> finalizer for
/// <see cref="ulong"/>. A caller reasoning by analogy across widths — the way the rest of the
/// hasher surface is designed to be read — silently changed hash strength, not just key width.
/// The signed families never had the problem, because they name the algorithm in the type
/// (<see cref="Int32WangNaiveHasher"/> / <see cref="Int32WangHasher"/> /
/// <see cref="Int32Murmur3Hasher"/>); the unsigned ones now do too.
/// <para>
/// This alias forwards to <see cref="UInt32WangNaiveHasher"/> rather than repeating the
/// mixer, so the two cannot drift. It will be removed in a future major version.
/// </para>
/// </remarks>
[Obsolete("UInt32Hasher is the cheap XOR-fold, which its bare name does not say — and the " +
          "same bare name means the strong fmix64 finalizer for ulong. Use " +
          "UInt32WangNaiveHasher, which names the algorithm and hashes identically. " +
          "This alias will be removed in a future major version.")]
public struct UInt32Hasher : IHashProvider<uint>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(uint key) => default(UInt32WangNaiveHasher).Hash(key);
}
