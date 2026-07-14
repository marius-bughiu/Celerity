using System.Numerics;
using System.Runtime.CompilerServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A <strong>build-once, immutable</strong> probabilistic set membership filter that is
/// <strong>smaller and faster to query</strong> than <see cref="BloomFilter{T,THasher}"/> or
/// <see cref="CuckooFilter{T,THasher}"/> at the same false-positive rate, parameterized on a custom
/// <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Like a Bloom or cuckoo filter, an xor filter answers "is this element <em>possibly</em> in the set?"
/// with <strong>no false negatives</strong> (a <see cref="Contains"/> that returns <c>false</c> is always
/// correct) and a bounded false-positive probability — but it is the <em>static</em> member of the family:
/// the whole element set is supplied once at construction and the filter is then immutable (there is no
/// <c>Add</c>, <c>Remove</c>, or <c>Clear</c>). In exchange it is the most space-efficient of the three —
/// ~9.84 bits per element for the fixed 8-bit fingerprint, versus a Bloom filter's ~12–14 bits at the same
/// ~0.4% rate — and every lookup is exactly <strong>three memory probes and two XORs</strong>, with no probe
/// loop and no data-dependent branch, so query throughput is higher and more predictable. The BCL ships no
/// probabilistic membership filter, so for membership-only workloads over a fixed set this uses a small
/// fraction of the memory of a <see cref="HashSet{T}"/> and never grows with element size.
/// </para>
/// <para>
/// The structure is the <em>xor filter</em> of Graf &amp; Lemire (<em>"Xor Filters: Faster and Smaller Than
/// Bloom and Cuckoo Filters"</em>, ACM JEA&#160;2020). The backing store is a byte array of
/// <c>3&#160;·&#160;blockLength ≈ 1.23&#160;·&#160;n</c> 8-bit fingerprints, split into three equal
/// segments; each element maps to one slot in each segment (<c>h0</c>, <c>h1</c>, <c>h2</c>), and the
/// filter is built so that <c>fingerprint(x) == store[h0] XOR store[h1] XOR store[h2]</c> holds for every
/// element <c>x</c> of the set. Construction assigns the fingerprints by <strong>peeling</strong> the
/// 3-uniform hypergraph of element→slot incidences (repeatedly claiming a slot touched by exactly one
/// remaining element, then back-filling in reverse), retrying with a fresh internal seed on the rare peel
/// failure. All three slots and the fingerprint are derived from a <strong>single</strong>
/// <see cref="IHashProvider{T}.Hash"/> call per element, avalanched to 64 bits, so the hasher is invoked
/// exactly once per element at build time and once per <see cref="Contains"/>.
/// </para>
/// <para>
/// Because the source is a <em>set</em>, the constructor deduplicates internally: two elements that hash to
/// the same 64-bit value collapse to one entry (harmless for a membership filter — both still test present),
/// so <see cref="Count"/> is the number of <em>distinct</em> element hashes, which can be below the source
/// length if the source contained duplicates or hash-colliding elements.
/// </para>
/// <para>
/// Because the filter stores fingerprints, not keys, it needs <strong>no out-of-band handling</strong> for
/// <c>default(T)</c> (a zero <c>int</c>, <see cref="System.Guid.Empty"/>, the empty string): those are
/// hashed like any other element. A <c>null</c> reference is mapped to a fixed base hash so the filter never
/// invokes the hasher with <c>null</c> (string hashers throw on <c>null</c>), matching the library's
/// out-of-band-<c>null</c> convention.
/// </para>
/// <para>
/// Choose <see cref="XorFilter{T,THasher}"/> when the element set is known up front and does not change —
/// static allow/deny lists, a precomputed "have I seen this key?" gate in front of an expensive exact
/// lookup, read-only shard membership. If the set grows over the filter's lifetime use
/// <see cref="BloomFilter{T,THasher}"/>; if it also shrinks use <see cref="CuckooFilter{T,THasher}"/>; if you
/// need exact membership or to enumerate the elements use <see cref="FrozenCeleritySet"/> /
/// <see cref="CeleritySet{T,THasher}"/>.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of elements tested for membership.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing <see cref="IHashProvider{T}"/>
/// so the JIT can devirtualize and inline it.
/// </typeparam>
public class XorFilter<T, THasher> where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The width in bits of each stored fingerprint. Fixed at 8, giving a false-positive probability of
    /// <c>1 / 2^8 ≈ 0.39%</c>.
    /// </summary>
    public const int FINGERPRINT_BITS = 8;

    // Upper bound on peel-retry attempts before construction gives up. With the 1.23x slot factor the first
    // attempt succeeds with high probability and a handful of reseeds cover the rest; this bound only guards
    // against a pathological hasher and is effectively never reached.
    private const int MaxConstructionAttempts = 1024;

    private readonly byte[] _fingerprints;   // 3 · blockLength slots, one 8-bit fingerprint each
    private readonly int _blockLength;        // slots per segment
    private readonly ulong _seed;             // the seed of the successful peel
    private readonly int _count;              // distinct element hashes represented
    private readonly THasher _hasher;

    /// <summary>
    /// Initializes a new instance of the <see cref="XorFilter{T,THasher}"/> class holding exactly the elements
    /// of <paramref name="source"/>. The filter is immutable once built.
    /// </summary>
    /// <param name="source">
    /// The complete set of elements the filter should report present. Duplicates (and elements whose hashes
    /// collide) are collapsed, so the built filter is sized to the number of distinct element hashes.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The peeling construction failed to converge after <see cref="MaxConstructionAttempts"/> reseeds — only
    /// possible with a pathologically degenerate hasher, and effectively never in practice.
    /// </exception>
    public XorFilter(IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _hasher = default;

        // A membership filter over a set: dedupe the 64-bit element hashes up front. Two elements that hash to
        // the same 64-bit value are indistinguishable to the filter (both test present), and — crucially for
        // construction — the peeling algorithm requires distinct keys, so collapsing them here is both correct
        // and necessary. The per-element base hash is avalanched to 64 bits so a weak 32-bit hasher still
        // spreads across the three segments.
        var distinct = new HashSet<ulong>();
        foreach (T item in source)
            distinct.Add(KeyHash(item));

        int size = distinct.Count;
        _count = size;

        // Slot count ≈ 1.23·n, split into three equal segments. The peelability threshold for a 3-uniform
        // hypergraph sits just above this ratio, so a fresh seed almost always yields a peelable layout.
        int arrayLength = 32 + (int)Math.Ceiling(1.23 * size);
        int blockLength = arrayLength / 3;
        if (blockLength < 1)
            blockLength = 1;
        _blockLength = blockLength;
        int capacity = blockLength * 3;

        _fingerprints = new byte[capacity];

        if (size == 0)
        {
            // Empty set: nothing to peel. Contains short-circuits on _count == 0, so the (all-zero) store is
            // never consulted and every lookup correctly reports absent.
            _seed = 0;
            return;
        }

        ulong[] keys = new ulong[size];
        distinct.CopyTo(keys);

        if (!TryBuild(keys, capacity, blockLength, out _seed, _fingerprints))
            throw new InvalidOperationException(
                "XorFilter construction failed to converge; the hasher maps the element set too degenerately to peel.");
    }

    /// <summary>
    /// Gets the number of distinct element hashes the filter represents. This is the deduplicated element
    /// count, which can be below the length of the constructor source when it held duplicates or
    /// hash-colliding elements.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the number of 8-bit fingerprint slots in the backing store (<c>3 · blockLength ≈ 1.23 · n</c>).
    /// This is also the filter's size in bytes.
    /// </summary>
    public int SlotCount => _fingerprints.Length;

    /// <summary>
    /// Gets the width in bits of each stored fingerprint (<see cref="FINGERPRINT_BITS"/>, 8).
    /// </summary>
    public int FingerprintBits => FINGERPRINT_BITS;

    /// <summary>
    /// Gets the theoretical false-positive probability of the filter: <c>1 / 2^FingerprintBits ≈ 0.39%</c>.
    /// Independent of the element count (unlike a Bloom filter, an xor filter does not degrade as it fills —
    /// it is sized exactly for its element set at build time).
    /// </summary>
    public double FalsePositiveRate => 1.0 / (1 << FINGERPRINT_BITS);

    /// <summary>
    /// Gets the storage cost in bits per represented element (<c>SlotCount · 8 / Count</c>), ≈ 9.84 for a
    /// well-sized filter. Returns 0 for an empty filter.
    /// </summary>
    public double BitsPerElement => _count == 0 ? 0d : _fingerprints.Length * 8.0 / _count;

    /// <summary>
    /// Determines whether the filter <em>possibly</em> contains an element.
    /// </summary>
    /// <param name="item">The element to test.</param>
    /// <returns>
    /// <c>false</c> if <paramref name="item"/> was definitely not in the construction set (no false
    /// negatives); <c>true</c> if it probably was — subject to the filter's false-positive rate.
    /// </returns>
    public bool Contains(T item)
    {
        if (_count == 0)
            return false;

        ulong h = Mix(KeyHash(item) ^ _seed);
        byte fp = Fingerprint(h);
        Positions(h, _blockLength, out int h0, out int h1, out int h2);

        byte[] store = _fingerprints;
        return fp == (byte)(store[h0] ^ store[h1] ^ store[h2]);
    }

    // ── construction ─────────────────────────────────────────────────────────────────────

    // One peel attempt for a fixed seed. Returns true and fills 'store' on success. The 3-uniform hypergraph
    // has one vertex per slot and one edge per key (its three segment slots); peeling repeatedly claims a slot
    // incident to exactly one remaining key, records (key, slot), and removes that key. If every key is peeled
    // the fingerprints are back-filled in reverse peel order, which guarantees each key's own slot is the last
    // of its three to be written, so the query XOR reproduces its fingerprint.
    private static bool TryBuild(ulong[] keys, int capacity, int blockLength, out ulong seed, byte[] store)
    {
        int size = keys.Length;

        int[] slotCount = new int[capacity];   // number of not-yet-peeled keys touching each slot
        ulong[] slotHashXor = new ulong[capacity]; // XOR of those keys' mixed hashes (isolates the last one)

        int[] queue = new int[capacity];       // slots currently at degree 1
        ulong[] stackHash = new ulong[size];   // peel order: the key's mixed hash
        int[] stackSlot = new int[size];       // peel order: the slot the key claims

        ulong candidate = 0x726b2b9d438b9d4dUL; // deterministic starting seed; reseeded on failure
        for (int attempt = 0; attempt < MaxConstructionAttempts; attempt++)
        {
            Array.Clear(slotCount, 0, capacity);
            Array.Clear(slotHashXor, 0, capacity);

            // Scatter every key across its three segment slots.
            for (int i = 0; i < size; i++)
            {
                ulong h = Mix(keys[i] ^ candidate);
                Positions(h, blockLength, out int h0, out int h1, out int h2);
                slotCount[h0]++; slotHashXor[h0] ^= h;
                slotCount[h1]++; slotHashXor[h1] ^= h;
                slotCount[h2]++; slotHashXor[h2] ^= h;
            }

            // Seed the queue with every degree-1 slot.
            int qSize = 0;
            for (int i = 0; i < capacity; i++)
                if (slotCount[i] == 1)
                    queue[qSize++] = i;

            int stackSize = 0;
            while (qSize > 0)
            {
                int slot = queue[--qSize];
                if (slotCount[slot] != 1)
                    continue; // its lone key was already peeled via another slot

                ulong h = slotHashXor[slot]; // the one remaining key touching this slot
                stackHash[stackSize] = h;
                stackSlot[stackSize] = slot;
                stackSize++;

                Positions(h, blockLength, out int h0, out int h1, out int h2);
                RemoveKey(h0, h, slotCount, slotHashXor, queue, ref qSize);
                RemoveKey(h1, h, slotCount, slotHashXor, queue, ref qSize);
                RemoveKey(h2, h, slotCount, slotHashXor, queue, ref qSize);
            }

            if (stackSize == size)
            {
                // Back-fill in reverse peel order. Each key's claimed slot is xored with its fingerprint and
                // the current values of its two other slots; those other slots are already final (their owners
                // were peeled later, hence assigned earlier in this reverse pass), so the invariant holds.
                Array.Clear(store, 0, capacity);
                for (int i = stackSize - 1; i >= 0; i--)
                {
                    ulong h = stackHash[i];
                    int slot = stackSlot[i];
                    Positions(h, blockLength, out int h0, out int h1, out int h2);

                    byte val = Fingerprint(h);
                    if (slot != h0) val ^= store[h0];
                    if (slot != h1) val ^= store[h1];
                    if (slot != h2) val ^= store[h2];
                    store[slot] = val;
                }

                seed = candidate;
                return true;
            }

            // Peel stalled (a 2-core remains): reseed and retry with a different slot layout.
            candidate = Mix(candidate + 0x9E3779B97F4A7C15UL);
        }

        seed = 0;
        return false;
    }

    // Detaches key 'h' from slot 'pos'; if that leaves the slot at degree 1 it becomes peelable.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RemoveKey(int pos, ulong h, int[] slotCount, ulong[] slotHashXor, int[] queue, ref int qSize)
    {
        slotCount[pos]--;
        slotHashXor[pos] ^= h;
        if (slotCount[pos] == 1)
            queue[qSize++] = pos;
    }

    // The three segment slots of a mixed hash. Each segment owns a distinct [k·blockLength, (k+1)·blockLength)
    // range, so h0/h1/h2 are always distinct indices. Lemire's multiply-shift maps a 32-bit lane uniformly
    // into [0, blockLength) without a modulo.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Positions(ulong h, int blockLength, out int h0, out int h1, out int h2)
    {
        uint r0 = (uint)h;
        uint r1 = (uint)BitOperations.RotateLeft(h, 21);
        uint r2 = (uint)BitOperations.RotateLeft(h, 42);
        h0 = Reduce(r0, blockLength);
        h1 = Reduce(r1, blockLength) + blockLength;
        h2 = Reduce(r2, blockLength) + 2 * blockLength;
    }

    // Lemire's fast alternative to (x % n) for a 32-bit x: the high half of x·n.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Reduce(uint x, int n) => (int)(((ulong)x * (ulong)n) >> 32);

    // The 8-bit fingerprint of a mixed hash.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Fingerprint(ulong h) => (byte)(h ^ (h >> 32));

    // The element's stable 64-bit key, independent of the construction seed. A null reference is mapped to a
    // fixed base hash so the hasher (which may throw on null, e.g. the string hashers) is never invoked with
    // null — value-type defaults (0, Guid.Empty) are valid inputs and go through the hasher normally. The
    // typeof(T).IsValueType guard is a JIT-time constant, so the null check is compiled away entirely for
    // value-type instantiations (no boxing).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong KeyHash(T item)
    {
        int baseHash = (!typeof(T).IsValueType && item is null) ? 0 : _hasher.Hash(item);
        return Mix((uint)baseHash);
    }

    // Murmur3 fmix64 — a bijective 64-bit avalanche, so distinct inputs always yield distinct outputs (no
    // spurious hash collisions within a construction attempt, which would make peeling impossible).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mix(ulong x)
    {
        x ^= x >> 33;
        x *= 0xFF51AFD7ED558CCDUL;
        x ^= x >> 33;
        x *= 0xC4CEB9FE1A85EC53UL;
        x ^= x >> 33;
        return x;
    }
}
