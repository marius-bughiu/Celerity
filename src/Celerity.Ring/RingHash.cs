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
    /// <summary>
    /// Computes the ring position of a node's <paramref name="replicaIndex"/>-th virtual node from the node's
    /// base hash: the high half of <see cref="Mix64"/> over the (node hash, replica index) pair.
    /// </summary>
    /// <remarks>
    /// Both inputs go through a real 64-bit mix, so two nodes share a position only by independent chance, one
    /// virtual node at a time. Finalizing an arithmetic progression of the node hash instead (the placement
    /// before #459) made two nodes whose hashes sat <c>j</c> golden-ratio steps apart share all but <c>j</c> of
    /// their positions, and the node that loses ties kept a few percent of its keys. Two node ids whose 32-bit
    /// hashes collide outright still share every position; that is inherent to a 32-bit node hash.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint VirtualNodePosition(uint nodeHash, int replicaIndex) =>
        (uint)(Mix64(nodeHash, (uint)replicaIndex) >> 32);

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
