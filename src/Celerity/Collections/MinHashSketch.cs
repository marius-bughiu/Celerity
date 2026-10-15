using System.Runtime.CompilerServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A fixed-size, mergeable sketch of a set that estimates the <em>Jaccard similarity</em>
/// between two sets — <c>|A ∩ B| / |A ∪ B|</c> — without storing either of them,
/// parameterized on a custom <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// MinHash answers "how much do these two sets overlap?" in a fixed amount of memory that
/// does <strong>not</strong> grow with the sets. The exact answer needs both sets in full —
/// two <see cref="HashSet{T}"/>s and an <c>O(|A|)</c> intersection count, paid again for
/// every pair compared — while a sketch is <c>m = 2^precision</c> registers (2&#160;KB at the
/// default precision of 8), and comparing two sketches is an <c>O(m)</c> pass over their
/// registers no matter how large the sets were. That is what makes near-duplicate detection
/// over many documents tractable: build one sketch per document once, then compare sketches.
/// </para>
/// <para>
/// This is <em>one-permutation</em> MinHash (Li, Owen &amp; Zhang, 2012). Each element is
/// hashed once to 64 bits; the top <see cref="Precision"/> bits route it to one of the
/// <c>m</c> registers, and the register keeps the smallest value of the remaining bits it has
/// seen. Two sets that share an element produce the same minimum in that element's register
/// whenever it is the smallest there in both, which happens with probability equal to the
/// Jaccard similarity. <see cref="EstimateJaccard"/> returns the fraction of registers that
/// match among those that are non-empty in at least one of the two sketches — the estimator
/// that paper proves unbiased. Adding an element costs one hasher call and one compare,
/// rather than the <c>m</c> hash evaluations of the classic <c>m</c>-permutation form.
/// </para>
/// <para>
/// The standard error is about <c>sqrt(J · (1 − J) / m)</c> once the sets are large enough
/// to fill every register (a few times <c>m</c> elements), which is at most
/// <see cref="StandardError"/>, the value at <c>J = 0.5</c>. Smaller sets leave registers
/// empty, and the estimate then rests on the registers that are filled, so its error grows
/// toward <c>sqrt(J · (1 − J) / k)</c> for <c>k</c> filled registers. Like the other sketches
/// it estimates what the hashes see: two distinct elements whose 64-bit hashes are equal are
/// the same element to the sketch.
/// </para>
/// <para>
/// Hashing follows <see cref="HyperLogLog{T,THasher}"/>: a hasher implementing
/// <see cref="IHashProvider64{T}"/> supplies all 64 bits directly, and a 32-bit-only hasher
/// has its code avalanched to 64 bits by the SplitMix64 finalizer — a bijection, so it
/// reaches 2^32 distinct hashes and two elements collide only when their 32-bit codes do.
/// There is no empty-slot sentinel among the elements, so <c>default(T)</c> is hashed and
/// counted like any other element; a <c>null</c> reference is mapped to a fixed hash so the
/// hasher is never invoked with <c>null</c>.
/// </para>
/// <para>
/// The sketch is add-only: it cannot remove an element or say whether a specific element
/// was added. Two sketches built with the same precision can be merged with
/// <see cref="UnionWith"/>, which leaves exactly the sketch the combined stream would have
/// produced.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the set's elements.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class MinHashSketch<T, THasher> where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The default register-index precision used when a constructor does not specify one:
    /// 8, giving <c>m = 256</c> eight-byte registers (2&#160;KB) and a standard error of at
    /// most about 0.031.
    /// </summary>
    public const int DefaultPrecision = 8;

    /// <summary>The minimum supported precision (<c>m = 16</c> registers).</summary>
    public const int MinPrecision = 4;

    /// <summary>The maximum supported precision (<c>m = 65536</c> registers).</summary>
    public const int MaxPrecision = 16;

    // A register's value is the low (64 - precision) bits of a hash, so with precision >= 4
    // every stored value is below 2^60 and the all-ones word can never be one: it marks a
    // register no element has reached.
    private const ulong EmptyRegister = ulong.MaxValue;

    private readonly ulong[] _registers;
    private readonly int _precision;     // p
    private readonly ulong _valueMask;   // low (64 - p) bits
    private readonly THasher _hasher;

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="MinHashSketch{T,THasher}"/> class
    /// with the specified register-index precision.
    /// </summary>
    /// <param name="precision">
    /// The number of bits of each element's hash used to select a register. The sketch
    /// allocates <c>2^precision</c> eight-byte registers, so larger values cost more memory
    /// but lower the standard error (at most ≈ <c>0.5 / sqrt(2^precision)</c>). Must be
    /// between <see cref="MinPrecision"/> and <see cref="MaxPrecision"/> inclusive.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="precision"/> is outside the inclusive range
    /// [<see cref="MinPrecision"/>, <see cref="MaxPrecision"/>].
    /// </exception>
    public MinHashSketch(int precision = DefaultPrecision)
    {
        if (precision < MinPrecision || precision > MaxPrecision)
            throw new ArgumentOutOfRangeException(nameof(precision), precision,
                $"Precision must be between {MinPrecision} and {MaxPrecision} inclusive.");

        _precision = precision;
        _valueMask = ulong.MaxValue >> precision;
        _registers = new ulong[1 << precision];
        _registers.AsSpan().Fill(EmptyRegister);
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MinHashSketch{T,THasher}"/> class with
    /// the specified precision and populated with the elements of <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The elements to add. Duplicates do not change the sketch.</param>
    /// <param name="precision">The register-index precision; see the primary constructor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="precision"/> is outside the inclusive range
    /// [<see cref="MinPrecision"/>, <see cref="MaxPrecision"/>].
    /// </exception>
    public MinHashSketch(IEnumerable<T> source, int precision = DefaultPrecision)
        : this(NullCheckedPrecision(source, precision))
    {
        foreach (T item in source)
            Add(item);
    }

    // Runs as part of the chained-ctor argument expression so the null check beats the
    // primary ctor's precision validation: a null source must surface as
    // ArgumentNullException, not ArgumentOutOfRangeException, even when the caller also
    // passed an out-of-range precision.
    private static int NullCheckedPrecision(IEnumerable<T> source, int precision)
    {
        ArgumentNullException.ThrowIfNull(source);
        return precision;
    }

    /// <summary>
    /// Gets the register-index precision (<c>p</c>) the sketch was constructed with.
    /// </summary>
    public int Precision => _precision;

    /// <summary>
    /// Gets the number of registers (<c>m = 2^precision</c>).
    /// </summary>
    public int RegisterCount => _registers.Length;

    /// <summary>
    /// Gets the largest standard error of <see cref="EstimateJaccard"/> for sets large enough
    /// to fill every register, ≈ <c>0.5 / sqrt(m)</c>: the value of
    /// <c>sqrt(J · (1 − J) / m)</c> at <c>J = 0.5</c>, where it peaks. The error is smaller
    /// for similarities near 0 or 1, and larger for sets too small to fill the registers.
    /// </summary>
    public double StandardError => 0.5 / Math.Sqrt(_registers.Length);

    /// <summary>
    /// Gets a value indicating whether no element has been added since construction or the
    /// last <see cref="Clear"/>.
    /// </summary>
    /// <remarks>This is an <c>O(m)</c> scan of the registers.</remarks>
    public bool IsEmpty => !_registers.AsSpan().ContainsAnyExcept(EmptyRegister);

    /// <summary>
    /// Adds an element to the sketch, lowering the register its hash routes to when the
    /// element's value is the smallest that register has seen.
    /// </summary>
    /// <param name="item">The element to add. Adding an element already represented does not
    /// change the sketch.</param>
    public void Add(T item)
    {
        ulong hash = Hash64(item);

        // Top `precision` bits select the register; the remaining bits are the value.
        int register = (int)(hash >> (64 - _precision));
        ulong value = hash & _valueMask;

        ref ulong slot = ref _registers[register];
        if (value < slot)
            slot = value;
    }

    /// <summary>
    /// Estimates the Jaccard similarity <c>|A ∩ B| / |A ∪ B|</c> between the set this sketch
    /// summarizes and the set <paramref name="other"/> summarizes.
    /// </summary>
    /// <param name="other">The sketch to compare against. Must have the same
    /// <see cref="Precision"/>. Left unmodified.</param>
    /// <returns>
    /// An estimate in <c>[0, 1]</c>. It is <c>0</c> whenever exactly one of the sketches is
    /// empty, and <c>1</c> when both are — two empty sets are identical, and the choice
    /// keeps a sketch's similarity to itself at <c>1</c> in every state.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="other"/> has a different precision, so the two register arrays are
    /// not compatible.
    /// </exception>
    /// <remarks>
    /// This is an <c>O(m)</c> pass over both register arrays, independent of the size of
    /// either set. Registers that are empty in both sketches carry no information and are
    /// left out of the denominator; the result is the share of the remaining registers whose
    /// minima match. The estimate is symmetric.
    /// </remarks>
    public double EstimateJaccard(MinHashSketch<T, THasher> other)
    {
        ThrowIfIncompatible(other);

        ReadOnlySpan<ulong> registers = _registers;
        ReadOnlySpan<ulong> otherRegisters = other._registers.AsSpan(0, registers.Length);

        int matched = 0;
        int bothEmpty = 0;
        for (int i = 0; i < registers.Length; i++)
        {
            ulong a = registers[i];
            if (a == otherRegisters[i])
            {
                if (a == EmptyRegister)
                    bothEmpty++;
                else
                    matched++;
            }
        }

        int informative = registers.Length - bothEmpty;
        return informative == 0 ? 1d : (double)matched / informative;
    }

    /// <summary>
    /// Merges another sketch into this one in place, so this sketch afterwards summarizes
    /// the <em>union</em> of both sets. Both sketches must have been constructed with the
    /// same <see cref="Precision"/>.
    /// </summary>
    /// <param name="other">The sketch to merge in. Left unmodified.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="other"/> has a different precision, so the two register arrays are
    /// not compatible.
    /// </exception>
    /// <remarks>
    /// The merge takes the per-register minimum, which is exactly the register state the
    /// combined stream would have produced, so the merged sketch carries no error beyond
    /// that of a sketch built directly from the union.
    /// </remarks>
    public void UnionWith(MinHashSketch<T, THasher> other)
    {
        ThrowIfIncompatible(other);

        Span<ulong> registers = _registers;
        ReadOnlySpan<ulong> otherRegisters = other._registers.AsSpan(0, registers.Length);
        for (int i = 0; i < registers.Length; i++)
        {
            if (otherRegisters[i] < registers[i])
                registers[i] = otherRegisters[i];
        }
    }

    /// <summary>
    /// Resets the sketch to empty, clearing every register. The precision and register
    /// count are preserved.
    /// </summary>
    public void Clear() => _registers.AsSpan().Fill(EmptyRegister);

    private void ThrowIfIncompatible(MinHashSketch<T, THasher> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other._precision != _precision)
            throw new ArgumentException("The two sketches must have the same precision to be combined.", nameof(other));
    }

    // Produces the 64-bit hash the register index and value are derived from, exactly as
    // HyperLogLog does: an IHashProvider64<T> hasher's own result is used as-is, a 32-bit
    // code is avalanched by the SplitMix64 finalizer, and a null reference maps to a fixed
    // hash so the hasher is never invoked with null. Both guards are JIT-time constants.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong Hash64(T item)
    {
        if (!typeof(T).IsValueType && item is null)
            return Mix64(0);

        if (Hash64Source<T, THasher>.IsNative64)
            return Hash64Source<T, THasher>.Hash64(item);

        return Mix64((uint)_hasher.Hash(item));
    }

    // SplitMix64 finalizer seeded with the 32-bit base hash widened by the golden-ratio
    // increment, so even a zero base hash avalanches to a well-distributed 64-bit value.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mix64(uint x)
    {
        ulong z = x + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
