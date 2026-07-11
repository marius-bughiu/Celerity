using System.Runtime.CompilerServices;

namespace Celerity.Ring;

/// <summary>
/// Deterministic, allocation-free integer mixing shared by <see cref="ConsistentHashRing{TNode, TKey, THasher}"/>
/// and <see cref="RendezvousHash{TNode, TKey, THasher}"/>. Every function here is pure integer arithmetic with a
/// fixed, specified constant set, so its output is byte-identical on every runtime and architecture .NET
/// targets (all little-endian) — which is what makes a ring's shard assignment reproducible fleet-wide.
/// </summary>
internal static class RingHash
{
    // "lowbias32" integer finalizer (the same well-distributed mix Celerity's frozen collections use to place
    // elements). A single hardware-independent multiply/xor/shift chain, so it avalanches a raw node hash into a
    // uniformly spread ring position without any platform-varying operation.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Finalize(uint h)
    {
        h ^= h >> 16;
        h *= 0x7FEB352Du;
        h ^= h >> 15;
        h *= 0x846CA68Bu;
        h ^= h >> 16;
        return h;
    }

    /// <summary>
    /// Computes the ring position of a node's <paramref name="replicaIndex"/>-th virtual node from the node's
    /// base hash. Folding the replica index in with the golden-ratio increment before the finalizer spreads a
    /// single node's virtual nodes uniformly around the ring, and does so identically on every process.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint VirtualNodePosition(uint nodeHash, int replicaIndex) =>
        Finalize(nodeHash + unchecked((uint)replicaIndex * 0x9E3779B9u));

    /// <summary>
    /// Combines a node hash and a key hash into a 64-bit avalanche used as a rendezvous (highest-random-weight)
    /// score seed. The SplitMix64 finalizer is a bijection on 64 bits, so distinct (node, key) pairs stay
    /// distinct, and the value is identical on every runtime.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Mix64(uint nodeHash, uint keyHash)
    {
        ulong z = ((((ulong)nodeHash) << 32) | keyHash) + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
