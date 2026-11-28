using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A <strong>build-once, immutable</strong> probabilistic set membership filter — the
/// <em>binary fuse filter</em>, the smaller and faster-to-query successor to
/// <see cref="XorFilter{T,THasher}"/> — parameterized on a custom <see cref="IHashProvider{T}"/>
/// implementation.
/// </summary>
/// <remarks>
/// <para>
/// Like an xor filter, a binary fuse filter answers "is this element <em>possibly</em> in the set?" with
/// <strong>no false negatives</strong> (a <see cref="Contains"/> that returns <c>false</c> is always correct)
/// and a theoretical false-positive probability of <c>1 / 2^8 ≈ 0.39%</c> for an absent element. That rate
/// assumes a well-mixing hasher: an absent element whose 64-bit key equals a stored one always reports present,
/// so a degenerate hasher can push it to 100%, and a 32-bit-only hasher leaves a floor of roughly
/// <c>n / 2^32</c> under it (see <see cref="IHashProvider64{T}"/>). The element set is supplied once at
/// construction and the filter is then immutable: there is no <c>Add</c>, <c>Remove</c>, or <c>Clear</c>.
/// A lookup is the same three byte reads and two XORs as <see cref="XorFilter{T,THasher}"/>, with no probe
/// loop and no data-dependent branch.
/// </para>
/// <para>
/// The structure is Graf &amp; Lemire's <em>"Binary Fuse Filters: Fast and Smaller Than Xor Filters"</em>
/// (ACM JEA&#160;2022). The fingerprint array is cut into many equal segments whose length is a power of two,
/// and each element maps to one slot in each of <strong>three consecutive segments</strong>, rather than one
/// slot in each third of the whole array as an xor filter does. That keyed locality is what changes the
/// numbers. The peel needs fewer spare slots, so the store tends to <c>1.125</c> slots per element
/// (≈&#160;9.0 bits) at large sizes, against an xor filter's <c>1.23</c> (≈&#160;9.84 bits). Lookups were also
/// about 1.34x faster than <see cref="XorFilter{T,THasher}"/>'s on an Apple M4, at a thousand elements and at
/// a hundred thousand, hits and misses alike. Construction costs about the same: it sorts the hashed keys by
/// segment before peeling, which bought no speed at those sizes (1.25x slower than an xor filter's build at a
/// thousand, 1.06x at a hundred thousand), since the deduplicating hash set and the peel dominate.
/// </para>
/// <para>
/// <strong>The saving is a large-set effect.</strong> The slot factor the peel needs grows as the set
/// shrinks — <c>max(1.125, 0.875 + 0.25 · ln(10⁶) / ln n)</c>, the paper's sizing rule — so a binary fuse
/// filter is about the same size as an xor filter at twenty thousand elements, and <em>larger</em> below that:
/// ≈&#160;10.2 bits per element at ten thousand (an xor filter: 9.86) and ≈&#160;11.3 at a thousand. Below
/// roughly twenty thousand elements use
/// <see cref="XorFilter{T,THasher}"/>; <see cref="BitsPerElement"/> reports what a built filter actually
/// costs.
/// </para>
/// <para>
/// All three slots and the fingerprint are derived from a <strong>single</strong>
/// <see cref="IHashProvider{T}.Hash"/> call per element, avalanched to 64 bits (or the hasher's own 64-bit
/// result, when it implements <see cref="IHashProvider64{T}"/>), so the hasher runs exactly once per element
/// at build time and once per <see cref="Contains"/>. Construction retries with a fresh internal seed on the
/// rare peel failure.
/// </para>
/// <para>
/// The constructor deduplicates internally: two elements that hash to the same 64-bit value collapse to one
/// entry (harmless for a membership filter — both still test present), so <see cref="Count"/> is the number
/// of <em>distinct</em> element hashes. The filter stores fingerprints, not keys, so <c>default(T)</c> (a zero
/// <c>int</c>, <see cref="System.Guid.Empty"/>, the empty string) is an ordinary element; a <c>null</c>
/// reference is mapped to a fixed base hash so the hasher is never invoked with <c>null</c>.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of elements tested for membership.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing <see cref="IHashProvider{T}"/>
/// so the JIT can devirtualize and inline it.
/// </typeparam>
public class BinaryFuseFilter<T, THasher> where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The width in bits of each stored fingerprint. Fixed at 8, giving a false-positive probability of
    /// <c>1 / 2^8 ≈ 0.39%</c>.
    /// </summary>
    public const int FingerprintBits = 8;

    // The three slots of an element sit in three consecutive segments.
    private const int Arity = 3;

    // The paper's cap on the segment length. Past it a longer segment buys no locality, since the window of
    // three segments has already outgrown the caches.
    private const int MaxSegmentLength = 1 << 18;

    // Upper bound on peel-retry attempts before construction gives up. A fresh seed peels with high
    // probability at the paper's sizing, so this only guards against a future change to it.
    private const int MaxConstructionAttempts = 1024;

    private readonly byte[] _fingerprints;       // (segmentCount + 2) · segmentLength fingerprints
    private readonly uint _segmentLength;        // a power of two
    private readonly uint _segmentLengthMask;    // segmentLength - 1
    private readonly ulong _segmentCountLength;  // segmentCount · segmentLength: the range of the first slot
    private readonly ulong _seed;                // the seed of the successful peel
    private readonly int _count;                 // distinct element hashes represented
    private readonly THasher _hasher;

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryFuseFilter{T,THasher}"/> class holding exactly the
    /// elements of <paramref name="source"/>. The filter is immutable once built.
    /// </summary>
    /// <param name="source">
    /// The complete set of elements the filter should report present. Duplicates (and elements whose hashes
    /// collide) are collapsed, so the built filter is sized to the number of distinct element hashes.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The peeling construction failed under every seed in its retry budget. Random or ordinary inputs fail a
    /// seed only occasionally, so this takes a deliberately constructed source: the seed schedule is fixed, and
    /// a caller who controls the element hashes can plant, for each seed, a pair of elements that share all
    /// three slots. Inverting the hasher is not required: the hashers are unkeyed, so such a pair can be found
    /// by brute force (about 2^14 trial elements per seed at a few thousand elements). Treat a source an
    /// adversary controls as able to raise this exception.
    /// </exception>
    public BinaryFuseFilter(IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _hasher = default;

        // Peeling requires distinct keys, and two elements with one 64-bit key are indistinguishable to the
        // filter anyway, so collapse them before sizing.
        var distinct = new HashSet<ulong>();
        foreach (T item in source)
            distinct.Add(KeyHash(item));

        int size = distinct.Count;
        _count = size;

        int segmentLength = SegmentLengthFor(size);
        int segmentCount = SegmentCountFor(size, segmentLength);
        _segmentLength = (uint)segmentLength;
        _segmentLengthMask = (uint)segmentLength - 1;
        _segmentCountLength = (ulong)segmentCount * (uint)segmentLength;
        _fingerprints = new byte[ArrayLengthFor(segmentCount, segmentLength)];

        if (size == 0)
        {
            // Nothing to peel. Contains short-circuits on _count == 0, so the all-zero store is never read.
            _seed = 0;
            return;
        }

        ulong[] keys = new ulong[size];
        distinct.CopyTo(keys);

        BuildOrThrow(keys, segmentCount, _fingerprints, out _seed);
    }

    /// <summary>
    /// Gets the number of distinct element hashes the filter represents. This is the deduplicated element
    /// count, which can be below the length of the constructor source when it held duplicates or
    /// hash-colliding elements.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the number of 8-bit fingerprint slots in the backing store, which is also the filter's size in
    /// bytes. It tends to <c>1.125 · Count</c> for large sets and is proportionally larger for small ones.
    /// </summary>
    public int SlotCount => _fingerprints.Length;

    /// <summary>
    /// Gets the theoretical false-positive probability of the filter: <c>1 / 2^FingerprintBits ≈ 0.39%</c>.
    /// With a well-mixing 64-bit hasher it does not depend on the element count, since the filter is sized
    /// exactly for its element set at build time. A 32-bit-only hasher adds roughly <c>Count / 2^32</c> to the
    /// realized rate, and a degenerate one more; this property reports the fingerprint rate alone.
    /// </summary>
    public double FalsePositiveRate => 1.0 / (1 << FingerprintBits);

    /// <summary>
    /// Gets the storage cost in bits per represented element (<c>SlotCount · 8 / Count</c>): about 9.0 for a
    /// million elements, 9.4 for a hundred thousand, and more for smaller sets. Returns 0 for an empty filter.
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

        ulong h = Mix(KeyHash(item) + _seed);
        uint h0 = (uint)Math.BigMul(h, _segmentCountLength, out _);
        uint h1 = (h0 + _segmentLength) ^ ((uint)(h >> 18) & _segmentLengthMask);
        uint h2 = (h0 + 2 * _segmentLength) ^ ((uint)h & _segmentLengthMask);

        // h0 < segmentCount · segmentLength, and h1 and h2 stay inside the aligned segment one and two further
        // on, so all three are below (segmentCount + 2) · segmentLength, the store's length: the reads cannot
        // leave the array, and the bounds checks the JIT cannot prove away are skipped.
        ref byte store = ref MemoryMarshal.GetArrayDataReference(_fingerprints);
        return Fingerprint(h) == (byte)(Unsafe.Add(ref store, h0) ^ Unsafe.Add(ref store, h1) ^ Unsafe.Add(ref store, h2));
    }

    // ── sizing ───────────────────────────────────────────────────────────────────────────

    // The paper's segment length for arity 3: 2^floor(log_3.33(n) + 2.25), capped. A one-element or empty set
    // uses the smallest segment the formula produces.
    private static int SegmentLengthFor(int size)
    {
        if (size <= 1)
            return 4;

        int exponent = (int)Math.Floor(Math.Log(size) / Math.Log(3.33) + 2.25);
        return 1 << Math.Min(exponent, 18);
    }

    // The number of segments the first slot ranges over. The store holds two more, for the second and third
    // slots of an element whose first slot falls in the last of them. The slot budget is the paper's
    // n · max(1.125, 0.875 + 0.25 · ln(10⁶) / ln n), which a one-element or empty set does not need.
    private static int SegmentCountFor(int size, int segmentLength)
    {
        if (size <= 1)
            return 1;

        double factor = Math.Max(1.125, 0.875 + 0.25 * Math.Log(1_000_000d) / Math.Log(size));
        long capacity = (long)Math.Round(size * factor);
        long segments = (capacity + segmentLength - 1) / segmentLength;
        return (int)Math.Max(1, segments - (Arity - 1));
    }

    // Unreachable in practice: the deduplicating HashSet<ulong> would need tens of gigabytes before the store
    // passed the array limit. The check stays so an oversized set fails loudly instead of wrapping.
    [ExcludeFromCodeCoverage(Justification = "Unreachable: the source HashSet<ulong> runs out of memory long " +
        "before (segmentCount + 2) · segmentLength exceeds Array.MaxLength.")]
    private static int ArrayLengthFor(int segmentCount, int segmentLength)
    {
        long length = (long)(segmentCount + Arity - 1) * segmentLength;
        if (length > Array.MaxLength)
            throw new InvalidOperationException(
                $"A BinaryFuseFilter over this many elements needs {length} slots, more than an array can hold.");
        return (int)length;
    }

    // ── construction ─────────────────────────────────────────────────────────────────────

    // Wraps the retry driver in its non-convergence guard. An ordinary input stalls a seed only occasionally,
    // but the seed schedule is fixed, so an input built to stall every seed in it (one pair of elements per seed
    // whose hashes share all three slots) exhausts the budget and reaches the throw.
    private static void BuildOrThrow(ulong[] keys, int segmentCount, byte[] store, out ulong seed)
    {
        if (!TryBuild(keys, segmentCount, store, out seed))
            throw new InvalidOperationException(
                "BinaryFuseFilter construction failed to converge; the element set could not be peeled.");
    }

    // Retry driver: allocates the peel scratch once, then re-runs the peel under a fresh seed until one
    // succeeds or the budget runs out.
    private static bool TryBuild(ulong[] keys, int segmentCount, byte[] store, out ulong seed)
    {
        int capacity = store.Length;
        int size = keys.Length;

        // The bucket count for the segment sort: the next power of two at or above the segment count, and at
        // least two so the shift that extracts a bucket from a hash stays below 64.
        int blockBits = 1;
        while ((1 << blockBits) < segmentCount)
            blockBits++;

        var scratch = new PeelScratch(
            degree: new int[capacity],
            hashXor: new ulong[capacity],
            queue: new int[capacity],
            order: new ulong[size],
            orderSlot: new byte[size],
            blockStart: new int[1 << blockBits],
            blockBits: blockBits);

        ulong candidate = 0x726b2b9d438b9d4dUL; // deterministic starting seed; reseeded on failure
        for (int attempt = 0; attempt < MaxConstructionAttempts; attempt++)
        {
            if (TryPeel(keys, candidate, segmentCount, store, scratch))
            {
                seed = candidate;
                return true;
            }

            candidate = Mix(candidate + 0x9E3779B97F4A7C15UL);
        }

        seed = 0;
        return false;
    }

    // The peel's working arrays, allocated once per construction and reused across retries.
    private readonly struct PeelScratch(
        int[] degree, ulong[] hashXor, int[] queue, ulong[] order, byte[] orderSlot, int[] blockStart, int blockBits)
    {
        // Per slot: (number of unpeeled keys touching it) << 2, XOR-ed with the position (0, 1 or 2) each of
        // those keys has the slot at. When one key remains, the low two bits name its position.
        public readonly int[] Degree = degree;

        // Per slot: XOR of the hashes of the unpeeled keys touching it, which isolates the last one.
        public readonly ulong[] HashXor = hashXor;

        // Slots waiting to be examined at degree one.
        public readonly int[] Queue = queue;

        // First the hashes sorted by segment, then the peel order: the hash of each peeled key.
        public readonly ulong[] Order = order;

        // The peel order's other half: the position (0, 1 or 2) of the slot each key claimed.
        public readonly byte[] OrderSlot = orderSlot;

        // The counting sort's bucket cursors.
        public readonly int[] BlockStart = blockStart;

        public readonly int BlockBits = blockBits;
    }

    // One peel attempt for a fixed seed. Returns true and fills 'store' on success, false if a 2-core remains.
    private static bool TryPeel(ulong[] keys, ulong seed, int segmentCount, byte[] store, in PeelScratch s)
    {
        int size = keys.Length;
        int capacity = store.Length;
        int segmentLength = capacity / (segmentCount + Arity - 1);
        uint length = (uint)segmentLength;
        uint mask = length - 1;
        ulong countLength = (ulong)segmentCount * length;

        int[] degree = s.Degree;
        ulong[] hashXor = s.HashXor;
        int[] queue = s.Queue;
        ulong[] order = s.Order;
        byte[] orderSlot = s.OrderSlot;
        int[] blockStart = s.BlockStart;
        int shift = 64 - s.BlockBits;

        // Sort the hashes by their top bits, which decide the first slot's segment, so the scatter below walks
        // the store nearly in order. A counting sort: count each bucket, turn the counts into start offsets,
        // then place. The hash is recomputed rather than stored, which is cheaper than another n-long array.
        Array.Clear(blockStart);
        for (int i = 0; i < size; i++)
            blockStart[(int)(Mix(keys[i] + seed) >> shift)]++;

        int offset = 0;
        for (int b = 0; b < blockStart.Length; b++)
        {
            int bucket = blockStart[b];
            blockStart[b] = offset;
            offset += bucket;
        }

        for (int i = 0; i < size; i++)
        {
            ulong h = Mix(keys[i] + seed);
            order[blockStart[(int)(h >> shift)]++] = h;
        }

        // Scatter every key across its three slots.
        Array.Clear(degree);
        Array.Clear(hashXor);
        Span<int> slots = stackalloc int[Arity + 2];
        for (int i = 0; i < size; i++)
        {
            ulong h = order[i];
            Positions(h, countLength, length, mask, slots);
            degree[slots[0]] += 4;
            hashXor[slots[0]] ^= h;
            degree[slots[1]] = (degree[slots[1]] + 4) ^ 1;
            hashXor[slots[1]] ^= h;
            degree[slots[2]] = (degree[slots[2]] + 4) ^ 2;
            hashXor[slots[2]] ^= h;
        }

        // Seed the queue with every degree-one slot.
        int queued = 0;
        for (int i = 0; i < capacity; i++)
        {
            if ((degree[i] >> 2) == 1)
                queue[queued++] = i;
        }

        int peeled = 0;
        while (queued > 0)
        {
            int slot = queue[--queued];
            if ((degree[slot] >> 2) != 1)
                continue; // its lone key was already peeled through another slot

            ulong h = hashXor[slot];
            int found = degree[slot] & 3;
            order[peeled] = h;
            orderSlot[peeled] = (byte)found;
            peeled++;

            // Detach the key from its other two slots; a slot it leaves at degree one becomes peelable. The
            // slot it claimed keeps its degree: no other key touches it, and it is never queued again.
            Positions(h, countLength, length, mask, slots);
            for (int k = 1; k < Arity; k++)
            {
                int position = (found + k) % Arity;
                int other = slots[position];
                if ((degree[other] >> 2) == 2)
                    queue[queued++] = other;
                degree[other] = (degree[other] - 4) ^ position;
                hashXor[other] ^= h;
            }
        }

        if (peeled != size)
            return false;

        // Back-fill in reverse peel order. A key's two other slots were claimed by keys peeled after it, so
        // they are already final when its own slot is written, and the query XOR reproduces its fingerprint.
        for (int i = size - 1; i >= 0; i--)
        {
            ulong h = order[i];
            int found = orderSlot[i];
            Positions(h, countLength, length, mask, slots);
            store[slots[found]] = (byte)(Fingerprint(h) ^ store[slots[found + 1]] ^ store[slots[found + 2]]);
        }

        return true;
    }

    // The three slots of a hash, written as [h0, h1, h2, h0, h1] so the two slots after position p are at
    // p + 1 and p + 2 without a modulo. h0 ranges over the first segmentCount segments; h1 and h2 sit one and
    // two segments further on, each moved within its segment by other bits of the hash. Every segment is
    // aligned, so the XOR never leaves it and the three slots are always distinct.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Positions(ulong h, ulong countLength, uint length, uint mask, Span<int> slots)
    {
        uint h0 = (uint)Math.BigMul(h, countLength, out _);
        uint h1 = (h0 + length) ^ ((uint)(h >> 18) & mask);
        uint h2 = (h0 + 2 * length) ^ ((uint)h & mask);
        slots[0] = (int)h0;
        slots[1] = (int)h1;
        slots[2] = (int)h2;
        slots[3] = (int)h0;
        slots[4] = (int)h1;
    }

    // The 8-bit fingerprint of a mixed hash.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Fingerprint(ulong h) => (byte)(h ^ (h >> 32));

    // The element's stable 64-bit key, independent of the construction seed — the same derivation as
    // XorFilter's. A hasher implementing IHashProvider64<T> supplies it directly; otherwise the 32-bit code
    // is avalanched, which spreads it over 64 bits but leaves the reachable space at 2^32. A null reference
    // maps to a fixed key so the hasher is never called with null.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong KeyHash(T item)
    {
        if (!typeof(T).IsValueType && item is null)
            return Mix(0);

        if (Hash64Source<T, THasher>.IsNative64)
            return Hash64Source<T, THasher>.Hash64(item);

        return Mix((uint)_hasher.Hash(item));
    }

    // Murmur3 fmix64 — a bijective 64-bit avalanche, so distinct keys give distinct hashes under any seed,
    // which the peel requires.
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
