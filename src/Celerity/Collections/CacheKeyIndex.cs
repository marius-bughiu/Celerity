using System.Numerics;
using System.Runtime.CompilerServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// The key&#8594;node-slot index shared by the bounded caches (<see cref="LruCache{TKey, TValue, THasher}"/>,
/// <see cref="SieveCache{TKey, TValue, THasher}"/> and <see cref="LfuCache{TKey, TValue, THasher}"/>):
/// a separately-chained hash table whose chains are threaded through the caches' own fixed node slots.
/// </summary>
/// <remarks>
/// <para>
/// Each cache already keeps its entries in a fixed array of <c>capacity</c> node slots, and a slot
/// never moves while its entry is resident. So the index needs no entry storage of its own: a bucket
/// holds the first slot of its chain, and each slot records its key, its full hash, and the next slot
/// in the same bucket. Insertion pushes onto the bucket's chain; removal unlinks the slot from it.
/// Nothing is ever rehashed or relocated, and nothing allocates after construction.
/// </para>
/// <para>
/// This replaced a <see cref="CelerityDictionary{TKey, TValue, THasher}"/> mapping key to slot (#544).
/// Its linear probing is the wrong shape for a cache. A miss walks a cluster of variable length, which
/// the branch predictor cannot learn over a stream of distinct keys. Each eviction ran a backward-shift
/// delete that rehashed every key after the gap in its cluster. Here a miss usually lands on an empty
/// bucket: the bucket count is at least twice the capacity, so most buckets are empty even when the
/// cache is full. A removal recomputes no hash, because the slot stores its own.
/// </para>
/// <para>
/// A <c>null</c> key (a <c>null</c> reference, or a <see cref="Nullable{T}"/> with no value) hashes to
/// <c>0</c> without consulting the hasher, so a cache accepts a <c>null</c> key whichever hasher it was
/// built with, as it did when it was backed by the dictionary.
/// </para>
/// <para>
/// The type is a <see langword="readonly"/> struct of array references, so a cache holds it in a
/// <see langword="readonly"/> field with no defensive copies and no extra indirection.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="THasher">The struct hasher used to compute key hashes.</typeparam>
internal readonly struct CacheKeyIndex<TKey, THasher>
    where THasher : struct, IHashProvider<TKey>
{
    // Sentinel for "no slot" in a chain. Buckets store slot + 1 instead, so a freshly allocated
    // bucket array already reads as all-empty.
    private const int Nil = -1;

    // Bucket b holds (first slot of its chain) + 1, or 0 when the bucket is empty.
    private readonly int[] _buckets;

    // Per node slot: the next slot in the same bucket's chain (or Nil), and the key's full hash.
    private readonly int[] _chainNext;
    private readonly int[] _hashes;

    private readonly int _mask;

    // Whether TKey has a null value: true for a reference type or a Nullable<T>.
    private static readonly bool KeyCanBeNull = default(TKey) is null;

    /// <summary>
    /// The key resident in each node slot. Owned here because the chain walk compares against it; the
    /// caches read it through this array rather than keeping a second copy.
    /// </summary>
    internal readonly TKey[] Keys;

    /// <summary>
    /// Allocates an empty index over <paramref name="capacity"/> node slots.
    /// </summary>
    /// <param name="capacity">The number of node slots. The caller has validated it is at least 1.</param>
    internal CacheKeyIndex(int capacity)
    {
        // At least twice as many buckets as slots, so a full cache leaves most buckets empty and a miss
        // usually stops at the first load. Capped at 2^30 so the shift cannot overflow; past that cap
        // the chains just run a little longer.
        int buckets = (int)Math.Min(BitOperations.RoundUpToPowerOf2((uint)capacity) * 2UL, 1UL << 30);

        _buckets = new int[buckets];
        _mask = buckets - 1;
        _chainNext = new int[capacity];
        _hashes = new int[capacity];
        Keys = new TKey[capacity];
    }

    /// <summary>
    /// Returns the node slot holding <paramref name="key"/>, or <c>-1</c> when it is not resident.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Find(TKey key)
    {
        int hash = HashOf(key);
        int[] chainNext = _chainNext;
        int[] hashes = _hashes;
        TKey[] keys = Keys;
        var comparer = EqualityComparer<TKey>.Default;

        int node = _buckets[hash & _mask] - 1;
        while (node != Nil)
        {
            // For a value-type key the comparer is a JIT intrinsic, as cheap as the stored-hash test, so
            // the hash test only pays for itself on reference keys, whose Equals may be expensive.
            // typeof(TKey).IsValueType is a JIT-time constant, so each instantiation keeps one form.
            if (typeof(TKey).IsValueType
                    ? comparer.Equals(keys[node], key)
                    : hashes[node] == hash && comparer.Equals(keys[node], key))
                return node;
            node = chainNext[node];
        }
        return Nil;
    }

    /// <summary>
    /// Records <paramref name="key"/> as resident in <paramref name="node"/>. The caller guarantees the
    /// key is absent and the slot is not linked into any chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Insert(int node, TKey key)
    {
        int hash = HashOf(key);
        ref int bucket = ref _buckets[hash & _mask];
        Keys[node] = key;
        _hashes[node] = hash;
        _chainNext[node] = bucket - 1;
        bucket = node + 1;
    }

    /// <summary>
    /// Unlinks the resident slot <paramref name="node"/> from its chain and releases its key. The
    /// caller guarantees the slot is resident.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Remove(int node)
    {
        ref int bucket = ref _buckets[_hashes[node] & _mask];
        int next = _chainNext[node];
        if (bucket - 1 == node)
        {
            bucket = next + 1;
        }
        else
        {
            // Not the chain head: find its predecessor. Chains average well under one slot at the
            // load the bucket count guarantees, so this walk is short.
            int prev = bucket - 1;
            while (_chainNext[prev] != node)
                prev = _chainNext[prev];
            _chainNext[prev] = next;
        }
        Keys[node] = default!;
    }

    /// <summary>
    /// Empties the index: every bucket, and every slot's key so no reference is retained.
    /// </summary>
    internal void Clear()
    {
        Array.Clear(_buckets);
        Array.Clear(Keys);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashOf(TKey key)
    {
        // A null key — a null reference, or a Nullable<T> without a value — never reaches the hasher,
        // which may not accept one. KeyCanBeNull is a static readonly the tiered JIT treats as a
        // constant, so a non-nullable value-type instantiation drops the test entirely. The comparer
        // form, rather than `key is null`, keeps an unoptimized (Debug) build from boxing every key.
        if (KeyCanBeNull && EqualityComparer<TKey>.Default.Equals(key, default!))
            return 0;
        return default(THasher).Hash(key);
    }
}
