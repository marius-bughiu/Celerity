using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A high-quality hash provider for <see cref="Guid"/> keys.
/// </summary>
/// <remarks>
/// <para>
/// Reinterprets the 128-bit <see cref="Guid"/> as two 64-bit halves, applies the
/// Murmur3 <c>fmix64</c> finalizer to each half, XORs the mixed halves, and
/// truncates to 32 bits. Each half passes through an independent avalanche, so
/// every input bit influences the final hash even when the input Guids share a
/// common prefix (e.g. a database-generated sequence that clusters in the low
/// bytes, or the fixed version/variant nibbles of a V4 Guid).
/// </para>
/// <para>
/// The reinterpret is performed via <see cref="Unsafe.As{TFrom, TTo}(ref TFrom)"/>
/// against the by-value <c>key</c> local, so there is no copy, no allocation,
/// and no stack buffer. On architectures where <see cref="Guid"/>'s 4-byte
/// alignment differs from <see cref="ulong"/>'s 8-byte alignment the resulting
/// reads are unaligned, which is well-defined on x86/x64 and ARM64.
/// </para>
/// <para>
/// Prefer <see cref="GuidHasher"/> over <see cref="DefaultHasher{T}"/> for
/// <see cref="Guid"/> keys on hot paths: it is fully inlineable and avoids the
/// virtual dispatch through <see cref="EqualityComparer{T}.Default"/>.
/// </para>
/// <para>
/// A <see cref="Guid"/> carries 128 bits, so folding the mixed halves to 32 throws away
/// most of them. The type therefore also implements <see cref="IHashProvider64{T}"/>:
/// <see cref="Hash64"/> returns the full <c>lo ^ hi</c> combination, which is what the
/// probabilistic sketches want (see <see cref="IHashProvider64{T}"/> for why the extra
/// 32 bits matter there and not in a hash table).
/// </para>
/// </remarks>
public struct GuidHasher : IHashProvider<Guid>, IHashProvider64<Guid>
{
    private const ulong C1 = 0xff51afd7ed558ccdUL;
    private const ulong C2 = 0xc4ceb9fe1a85ec53UL;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(Guid key) => (int)Hash64(key);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Hash64(Guid key)
    {
        // Reinterpret the 16-byte Guid as two ulongs. `key` is a by-value local,
        // so `ref key` is safe to take and outlives the reinterpret.
        ref ulong p = ref Unsafe.As<Guid, ulong>(ref key);
        ulong lo = p;
        ulong hi = Unsafe.Add(ref p, 1);

        // Mix each half independently so every input bit reaches every output
        // bit of its half before we combine.
        lo = Fmix64(lo);
        hi = Fmix64(hi);

        return lo ^ hi;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Fmix64(ulong x)
    {
        x ^= x >> 33;
        x *= C1;
        x ^= x >> 33;
        x *= C2;
        x ^= x >> 33;
        return x;
    }
}
