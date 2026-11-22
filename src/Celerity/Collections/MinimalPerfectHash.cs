using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A <strong>build-once, immutable minimal perfect hash function</strong>: it maps each of a fixed set of
/// <c>n</c> keys to its own distinct index in <c>[0, n)</c> while storing <strong>none of the keys</strong> —
/// about 3.7 bits per key at the default <see cref="Gamma"/> — parameterized on a custom
/// <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// The use is a dense slot for each key of a static set: a word list into embedding rows, a URL set into a
/// counter array, a build-time symbol table into a value array. The BCL's answer is a
/// <see cref="Dictionary{TKey, TValue}"/> or <see cref="System.Collections.Frozen.FrozenDictionary{TKey, TValue}"/>
/// from key to <see cref="int"/>, and both store every key, so the index costs the keys plus the table.
/// This type stores a few bits per key instead, so the index for millions of keys stays small enough to
/// sit in cache.
/// </para>
/// <para>
/// <strong>It does not answer membership.</strong> <see cref="GetIndex"/> returns a distinct index in
/// <c>[0, <see cref="Count"/>)</c> for every key of the construction set, but for a key that was
/// <em>not</em> in the set it returns either <c>-1</c> or an arbitrary index in that same range, because no
/// key is stored to compare against. When a lookup can be asked about a non-member, store the key (or a
/// fingerprint of it) at its index and compare, as the usage example in the API reference does.
/// </para>
/// <para>
/// The construction is BBHash (Limasset, Rizk, Chikhi &amp; Peterlongo, <em>"Fast and scalable minimal
/// perfect hashing for massive key sets"</em>, SEA&#160;2017). Level 0 is a bit array of
/// <c>γ·n</c> bits; each key hashes to one position in it, a position hit by exactly one key keeps its bit,
/// and the keys that collided move on to level 1, sized <c>γ·</c>(keys left), and so on until every key has
/// a bit. A key's index is the number of set bits before its own across all levels, read from a popcount
/// index kept per 256 bits. With <c>γ = 2</c> about 61% of keys settle at level 0, so a member lookup reads
/// 1.6 levels on average.
/// </para>
/// <para>
/// Keys whose 64-bit hashes are identical land on the same position at every level and can never be
/// separated. Construction sorts the hashes once to find them: a group of keys that are all equal is one key
/// given more than once, and collapses to a single index; a group holding different keys is a genuine hash
/// collision — near certain past ~100,000 string keys with a 32-bit hasher, which reaches only 2^32 values —
/// and its keys go to a small exact table of the keys, searched by the hash already computed rather than
/// hashed again. Construction never throws on a valid source, and <see cref="Count"/> is the number of
/// distinct keys. A hasher that
/// implements <see cref="IHashProvider64{T}"/> keeps that table empty in practice.
/// </para>
/// <para>
/// Each key is hashed once at construction and once per <see cref="GetIndex"/>. A <c>null</c> reference is
/// mapped to a fixed hash, so the hasher (string hashers throw on <c>null</c>) is never called with it; value-type
/// defaults such as <c>0</c> or <see cref="Guid.Empty"/> are ordinary keys.
/// </para>
/// </remarks>
/// <typeparam name="T">The key type.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing <see cref="IHashProvider{T}"/> so
/// the JIT can devirtualize and inline it. Implementing <see cref="IHashProvider64{T}"/> as well lifts the
/// 2^32 hash-space floor described above.
/// </typeparam>
public class MinimalPerfectHash<T, THasher> where THasher : struct, IHashProvider<T>
{
    /// <summary>The <see cref="Gamma"/> used when none is given: about 3.7 bits per key.</summary>
    public const double DefaultGamma = 2.0;

    /// <summary>The smallest accepted <see cref="Gamma"/>.</summary>
    public const double MinGamma = 1.0;

    /// <summary>The largest accepted <see cref="Gamma"/>.</summary>
    public const double MaxGamma = 10.0;

    // A level is never narrower than one word, so a level holding a handful of keys still has room for them.
    private const long MinLevelBits = 64;

    // Words per popcount-index entry: one cumulative uint per 256 bits, 12.5% on top of the bit arrays.
    private const int RankShift = 2;

    private readonly ulong[] _bits;          // every level's bits, each level starting on a word boundary
    private readonly uint[] _ranks;          // set bits before each 256-bit block
    private readonly Level[] _levels;        // where each level starts in _bits, and its width
    private readonly ulong[] _fallbackHashes; // keys no level could separate: their hashes, sorted...
    private readonly T[] _fallbackKeys;       // ...and the keys, index i holding slot _placed + i
    private readonly int _placed;             // keys the levels answer, which come first in [0, Count)
    private readonly int _count;
    private readonly double _gamma;
    private readonly THasher _hasher;

    /// <summary>
    /// Builds a minimal perfect hash function over the distinct keys of <paramref name="keys"/>, using
    /// <see cref="DefaultGamma"/>.
    /// </summary>
    /// <param name="keys">The key set. Duplicates are collapsed to one index.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keys"/> is <c>null</c>.</exception>
    public MinimalPerfectHash(IEnumerable<T> keys)
        : this(keys, DefaultGamma)
    {
    }

    /// <summary>
    /// Builds a minimal perfect hash function over the distinct keys of <paramref name="keys"/>, sizing each
    /// level at <paramref name="gamma"/> bits per key still to be placed.
    /// </summary>
    /// <param name="keys">The key set. Duplicates are collapsed to one index.</param>
    /// <param name="gamma">
    /// Bits per remaining key at each level, in [<see cref="MinGamma"/>, <see cref="MaxGamma"/>]. Larger
    /// values settle more keys per level — faster construction and fewer levels per lookup — at more bits
    /// per key: about 3.1 bits at 1, 3.7 at 2 and 4.7 at 3, popcount index included.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="keys"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="gamma"/> is not a number in [<see cref="MinGamma"/>, <see cref="MaxGamma"/>].
    /// </exception>
    public MinimalPerfectHash(IEnumerable<T> keys, double gamma)
    {
        ArgumentNullException.ThrowIfNull(keys);
        // Written as a negated range test so NaN, which fails every comparison, is rejected too.
        if (!(gamma >= MinGamma && gamma <= MaxGamma))
            throw new ArgumentOutOfRangeException(nameof(gamma), gamma,
                $"Gamma must be between {MinGamma} and {MaxGamma}.");

        _hasher = default;
        _gamma = gamma;

        T[] source = keys as T[] ?? keys.ToArray();
        int n = source.Length;

        ulong[] hashes = new ulong[n];
        int[] origin = new int[n];
        for (int i = 0; i < n; i++)
        {
            hashes[i] = KeyHash(source[i]);
            origin[i] = i;
        }

        // Group equal hashes. A run of keys that all compare equal is one key with duplicates: keep one.
        // A run holding two different keys is a genuine full-hash collision that no level can separate:
        // its keys go to the exact table, and one "poison" entry (origin -1) stays in the levels so every
        // position that hash lands on is marked taken and no other key claims it. Without the poison entry
        // a lookup of one of those keys could find another key's bit and return that key's index.
        Array.Sort(hashes, origin);
        var comparer = EqualityComparer<T>.Default;
        List<(ulong Hash, int Origin)>? exact = null;
        int remaining = 0;
        int poison = 0;
        for (int i = 0; i < n;)
        {
            int end = i + 1;
            bool allEqual = true;
            while (end < n && hashes[end] == hashes[i])
            {
                allEqual &= comparer.Equals(source[origin[end]], source[origin[i]]);
                end++;
            }

            hashes[remaining] = hashes[i];
            if (allEqual)
            {
                origin[remaining] = origin[i];
            }
            else
            {
                exact ??= new List<(ulong, int)>();
                for (int k = i; k < end; k++)
                    exact.Add((hashes[k], origin[k]));
                origin[remaining] = -1;
                poison++;
            }

            remaining++;
            i = end;
        }

        var levelWords = new List<ulong[]>();
        var levelSizes = new List<ulong>();
        int placed = 0;

        while (remaining > poison)
        {
            int level = levelWords.Count;
            // Sized over the poison entries too: they occupy positions like any key, and a level sized for the
            // real keys alone would fill up with them as the real keys run out.
            ulong size = (ulong)Math.Max(MinLevelBits, (long)Math.Ceiling(gamma * remaining));
            int words = (int)((size + 63) >> 6);
            ulong[] seen = new ulong[words];
            ulong[] collided = new ulong[words];

            for (int i = 0; i < remaining; i++)
            {
                ulong p = Position(hashes[i], level, size);
                ulong bit = 1UL << (int)(p & 63);
                int w = (int)(p >> 6);
                if (origin[i] < 0 || (seen[w] & bit) != 0)
                    collided[w] |= bit;
                seen[w] |= bit;
            }

            // A bit survives when exactly one key hit it; everything else moves on, compacted in place.
            int settled = 0;
            for (int w = 0; w < words; w++)
            {
                seen[w] &= ~collided[w];
                settled += BitOperations.PopCount(seen[w]);
            }

            // Every key left has a distinct hash, so a level that settles none of them is bad luck on a small
            // level, not a stall a further level is doomed to repeat. Stop anyway and answer the few keys left
            // from the exact table: that costs a lookup, never correctness, and bounds the level count.
            if (settled == 0)
                break;

            int next = 0;
            for (int i = 0; i < remaining; i++)
            {
                ulong p = Position(hashes[i], level, size);
                if ((collided[p >> 6] & (1UL << (int)(p & 63))) != 0)
                {
                    hashes[next] = hashes[i];
                    origin[next] = origin[i];
                    next++;
                }
            }

            levelWords.Add(seen);
            levelSizes.Add(size);
            placed += settled;
            remaining = next;
        }

        // Concatenate the levels and build the popcount index over the whole run.
        int totalWords = 0;
        foreach (ulong[] w in levelWords)
            totalWords += w.Length;

        // Padded to whole 256-bit blocks so Rank can read a block's leading words unconditionally.
        _bits = new ulong[(totalWords + 3) & ~3];
        _levels = new Level[levelWords.Count];
        int at = 0;
        for (int l = 0; l < levelWords.Count; l++)
        {
            _levels[l] = new Level((ulong)at << 6, levelSizes[l]);
            levelWords[l].CopyTo(_bits, at);
            at += levelWords[l].Length;
        }

        _ranks = new uint[(totalWords >> RankShift) + 1];
        uint running = 0;
        for (int w = 0; w < totalWords; w++)
        {
            if ((w & ((1 << RankShift) - 1)) == 0)
                _ranks[w >> RankShift] = running;
            running += (uint)BitOperations.PopCount(_bits[w]);
        }

        // Keys a stopped level left behind join the colliding ones. Each was present at every level kept,
        // where its position was marked taken, so no lookup of it can stop at another key's bit.
        for (int i = 0; i < remaining; i++)
        {
            if (origin[i] >= 0)
                (exact ??= new List<(ulong, int)>()).Add((hashes[i], origin[i]));
        }

        // The exact table is a sorted array of the keys' full hashes beside the keys themselves, searched by
        // the hash GetIndex has already computed, so a key answered here costs no second hasher call. Equal
        // keys are folded as they are added, through a set that lives only for construction, so the table is
        // sized by distinct keys rather than occurrences and the fold stays linear however many distinct keys
        // share one hash.
        var fallbackHashes = new List<ulong>();
        var fallbackKeys = new List<T>();
        if (exact is not null)
        {
            exact.Sort(static (a, b) => a.Hash.CompareTo(b.Hash));
            var folded = new HashSet<T>(comparer);
            foreach ((ulong hash, int from) in exact)
            {
                if (folded.Add(source[from]))
                {
                    fallbackHashes.Add(hash);
                    fallbackKeys.Add(source[from]);
                }
            }
        }

        _fallbackHashes = fallbackHashes.ToArray();
        _fallbackKeys = fallbackKeys.ToArray();
        _placed = placed;
        _count = placed + _fallbackKeys.Length;
    }

    /// <summary>Gets the number of distinct keys, which is also the size of the index range <c>[0, Count)</c>.</summary>
    public int Count => _count;

    /// <summary>Gets the bits per remaining key each level was sized at.</summary>
    public double Gamma => _gamma;

    /// <summary>Gets the number of bit-array levels the construction used.</summary>
    public int LevelCount => _levels.Length;

    /// <summary>
    /// Gets the number of distinct keys answered from the small exact table rather than the levels: keys that
    /// share their full 64-bit hash with a different key, and the rare key left over when a small level
    /// settles nothing. Zero for a 64-bit hasher in practice.
    /// </summary>
    public int FallbackCount => _fallbackKeys.Length;

    /// <summary>
    /// Gets the storage cost of the level bit arrays and their popcount index, in bits per key. Returns 0 for
    /// an empty function. The exact fallback table, empty in practice, is not included.
    /// </summary>
    public double BitsPerKey =>
        _count == 0 ? 0d : (_bits.LongLength * 64.0 + _ranks.LongLength * 32.0) / _count;

    /// <summary>
    /// Returns the index of <paramref name="key"/>: distinct for every key of the construction set and in
    /// <c>[0, <see cref="Count"/>)</c>.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>
    /// For a key of the construction set, its index in <c>[0, Count)</c>. For any other key, either <c>-1</c>
    /// or an arbitrary index in <c>[0, Count)</c> that belongs to some other key — this function stores no
    /// keys, so it cannot tell. Verify against the key stored at the index when non-members can occur.
    /// </returns>
    public int GetIndex(T key)
    {
        ulong h = KeyHash(key);
        Level[] levels = _levels;

        for (int level = 0; level < levels.Length; level++)
        {
            ref readonly Level l = ref levels[level];
            ulong p = l.Offset + Position(h, level, l.Size);
            int w = (int)(p >> 6);
            ulong word = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_bits), w);
            int shift = (int)(p & 63);
            if (((word >> shift) & 1) != 0)
                return Rank(w, word, shift);
        }

        return _fallbackKeys.Length == 0 ? -1 : FallbackIndex(key, h);
    }

    // Kept out of line so the level walk above stays small: the table is empty in practice. Finds the run of
    // entries carrying the key's hash by binary search, then compares keys within it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int FallbackIndex(T key, ulong hash)
    {
        ulong[] hashes = _fallbackHashes;
        int lo = 0;
        int hi = hashes.Length;
        while (lo < hi)
        {
            int mid = (int)((uint)(lo + hi) >> 1);
            if (hashes[mid] < hash)
                lo = mid + 1;
            else
                hi = mid;
        }

        var comparer = EqualityComparer<T>.Default;
        for (int i = lo; i < hashes.Length && hashes[i] == hash; i++)
        {
            if (comparer.Equals(_fallbackKeys[i], key))
                return _placed + i;
        }

        return -1;
    }

    // Set bits before bit `shift` of word `w`: the block's cumulative count, the whole words before `w`
    // in its 256-bit block, and the low bits of `w` itself.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Rank(int w, ulong word, int shift)
    {
        // Branch-free: the block's three leading words are always read and masked off when they are not
        // before `w`, so the rank costs the same whatever the word's place in its block. A loop over the
        // 0–3 words would mispredict on that data-dependent trip count.
        ref ulong block = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_bits), w & ~3);
        int k = w & 3;
        ulong m1 = 0UL - (ulong)(k > 0 ? 1 : 0);
        ulong m2 = 0UL - (ulong)(k > 1 ? 1 : 0);
        ulong m3 = 0UL - (ulong)(k > 2 ? 1 : 0);
        uint rank = _ranks[w >> RankShift]
            + (uint)BitOperations.PopCount(block & m1)
            + (uint)BitOperations.PopCount(Unsafe.Add(ref block, 1) & m2)
            + (uint)BitOperations.PopCount(Unsafe.Add(ref block, 2) & m3)
            + (uint)BitOperations.PopCount(word & ((1UL << shift) - 1));
        return (int)rank;
    }

    // The key's position in a level of `size` bits, mapped onto [0, size) with the high half of a 64x64
    // multiply rather than a modulo. Level 0 uses the key hash as it is, which is already avalanched, and
    // so saves a mix on the ~61% of lookups that end there; every later level reseeds it through fmix64, so
    // keys that collided at one level scatter independently at the next.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Position(ulong hash, int level, ulong size)
    {
        ulong x = level == 0 ? hash : Mix(hash + (ulong)level * 0x9E3779B97F4A7C15UL);
        return Math.BigMul(x, size, out _);
    }

    // The key's 64-bit hash, independent of level. A hasher implementing IHashProvider64<T> supplies it
    // directly; a 32-bit code is avalanched with fmix64, which spreads it but cannot add entropy. A null
    // reference maps to a fixed hash so the hasher is never called with null. Both tests are JIT-time
    // constants, so each instantiation compiles to one straight-line path.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong KeyHash(T key)
    {
        if (!typeof(T).IsValueType && key is null)
            return Mix(0);

        if (Hash64Source<T, THasher>.IsNative64)
            return Hash64Source<T, THasher>.Hash64(key);

        return Mix((uint)_hasher.Hash(key));
    }

    // Murmur3 fmix64: a bijective 64-bit avalanche.
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

    // One level of the cascade: its first bit in the shared array and its width in bits.
    private readonly struct Level(ulong offset, ulong size)
    {
        public readonly ulong Offset = offset;
        public readonly ulong Size = size;
    }
}
