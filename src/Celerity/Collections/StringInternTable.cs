using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;
using Celerity.Primitives;

namespace Celerity.Collections;

/// <summary>
/// A canonicalizing table of <see cref="string"/>s that is probed with a
/// <see cref="ReadOnlySpan{T}"/> of <see cref="char"/> and allocates a
/// <see cref="string"/> <em>only on a miss</em>, using
/// <see cref="StringFnV1AFullHasher"/>. Supply a different string hasher via the
/// <see cref="StringInternTable{THasher}"/> generic overload.
/// </summary>
public sealed class StringInternTable : StringInternTable<StringFnV1AFullHasher>
{
    /// <summary>
    /// Initializes a new <see cref="StringInternTable"/> with the specified capacity
    /// and load factor.
    /// </summary>
    /// <param name="capacity">The initial capacity, rounded up to the next power of two.</param>
    /// <param name="loadFactor">
    /// The fraction of the table that can be filled before it grows.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/> is not
    /// in the open interval (0, 1).
    /// </exception>
    public StringInternTable(
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
        : base(capacity, loadFactor)
    {
    }
}

/// <summary>
/// A canonicalizing table of <see cref="string"/>s that is probed with a
/// <see cref="ReadOnlySpan{T}"/> of <see cref="char"/> and allocates a
/// <see cref="string"/> <em>only on a miss</em>.
/// </summary>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing both
/// <see cref="IHashProvider{T}"/> over <see cref="string"/> and
/// <see cref="ISpanHashProvider"/>, so the JIT can devirtualize and inline it and so the
/// span and string probes agree.
/// </typeparam>
/// <remarks>
/// <para>
/// <strong>The workload.</strong> A parser walking a 10M-cell CSV or a log stream holds each
/// token as a slice of its input buffer. If the token set is small — say a hundred distinct
/// column values — it wants a hundred <see cref="string"/>s, not ten million. Feeding each
/// slice to <see cref="GetOrAdd(ReadOnlySpan{char})"/> returns the one canonical instance and
/// materializes a <see cref="string"/> only the first time a token is seen. Downstream
/// reference equality then works, and the GC never sees the other 9,999,900 copies.
/// </para>
/// <para>
/// <strong>Why the BCL cannot do this before .NET 9.</strong>
/// <c>HashSet&lt;string&gt;.TryGetValue</c> takes a <see cref="string"/>, so you must
/// <em>allocate the string before you can discover you already had it</em> — the allocation
/// this type exists to avoid. .NET 9's
/// <c>Dictionary&lt;string,V&gt;.GetAlternateLookup&lt;ReadOnlySpan&lt;char&gt;&gt;()</c>
/// closes that gap on .NET 9+; this type works the same way on <c>net8.0</c>, which is
/// Celerity's floor, and stays available on all three target frameworks.
/// </para>
/// <para>
/// <strong>Not <see cref="string.Intern(string)"/>.</strong> The runtime intern pool is
/// process-wide, never collected for the life of the process, and — decisively — still
/// requires a <see cref="string"/> to hand it. A <see cref="StringInternTable{THasher}"/> is
/// an ordinary object: its scope is yours, <see cref="Clear"/> releases everything it holds,
/// and dropping the table drops the interned strings with it.
/// </para>
/// <para>
/// Keys are compared ordinally, matching <see cref="EqualityComparer{T}.Default"/> for
/// <see cref="string"/>. The empty string is an ordinary entry (an empty span means <c>""</c>);
/// <c>null</c> is not storable, and the string overloads reject it. The type is
/// single-threaded and does not guarantee enumeration order.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var interned = new StringInternTable();
///
/// foreach (ReadOnlySpan&lt;char&gt; cell in Cells(line))
/// {
///     string token = interned.GetOrAdd(cell);   // allocates only the first time
///     Consume(token);
/// }
/// </code>
/// </example>
public class StringInternTable<THasher> : IReadOnlyCollection<string>
    where THasher : struct, IHashProvider<string>, ISpanHashProvider
{
    /// <summary>
    /// The default initial capacity of the table if no capacity is specified.
    /// </summary>
    protected const int DEFAULT_CAPACITY = 16;

    /// <summary>
    /// The default load factor of the table if no load factor is specified.
    /// </summary>
    protected const float DEFAULT_LOAD_FACTOR = 0.75f;

    private string?[] _slots;
    private int _count;
    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

    // Incremented on every structural mutation so active enumerators can detect
    // concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new <see cref="StringInternTable{THasher}"/> with the specified
    /// capacity and load factor.
    /// </summary>
    /// <param name="capacity">The initial capacity, rounded up to the next power of two.</param>
    /// <param name="loadFactor">
    /// The fraction of the table that can be filled before it grows.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/> is not
    /// in the open interval (0, 1).
    /// </exception>
    public StringInternTable(
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = FastUtils.NextPowerOfTwo(capacity);

        _slots = new string?[size];
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
    }

    /// <summary>
    /// Gets the number of distinct strings the table has interned.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Returns the canonical <see cref="string"/> for the characters in
    /// <paramref name="key"/>, allocating one <em>only</em> if those characters are not
    /// already interned.
    /// </summary>
    /// <param name="key">The characters to canonicalize. An empty span means <c>""</c>.</param>
    /// <returns>
    /// The interned instance. Two calls with equal contents return the same reference, so
    /// callers may compare the results with <see cref="object.ReferenceEquals"/>.
    /// </returns>
    public string GetOrAdd(ReadOnlySpan<char> key)
    {
        int index = Probe(key, out bool wasEmpty);
        if (!wasEmpty)
            return _slots[index]!;

        // The miss path is the only one that materializes a string.
        string materialized = key.ToString();

        if (_count >= _threshold)
        {
            Resize();
            index = Probe(key, out _);
        }

        _slots[index] = materialized;
        _count++;
        _version++;
        return materialized;
    }

    /// <summary>
    /// Returns the canonical <see cref="string"/> for <paramref name="key"/>, interning
    /// <paramref name="key"/> itself if its contents are not already present.
    /// </summary>
    /// <param name="key">The string to canonicalize.</param>
    /// <returns>
    /// The interned instance — <paramref name="key"/> itself if it was the first with those
    /// contents, otherwise the instance already held.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <remarks>
    /// This overload never allocates a <see cref="string"/>: on a miss the supplied instance
    /// becomes the canonical one. (An insert that crosses the load factor still grows the
    /// backing array, as any hash table does.) It is the shape to use when you already hold a
    /// <see cref="string"/> and want to collapse duplicates.
    /// </remarks>
    public string GetOrAdd(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        int index = Probe(key.AsSpan(), out bool wasEmpty);
        if (!wasEmpty)
            return _slots[index]!;

        if (_count >= _threshold)
        {
            Resize();
            index = Probe(key.AsSpan(), out _);
        }

        _slots[index] = key;
        _count++;
        _version++;
        return key;
    }

    /// <summary>
    /// Looks up the characters in <paramref name="key"/> without interning them.
    /// </summary>
    /// <param name="key">The characters to look up. An empty span means <c>""</c>.</param>
    /// <param name="value">
    /// When this method returns, the interned instance if the contents were already present;
    /// otherwise <c>null</c>. The table is not modified either way.
    /// </param>
    /// <returns><c>true</c> if the contents were already interned; otherwise <c>false</c>.</returns>
    public bool TryGet(ReadOnlySpan<char> key, out string? value)
    {
        int index = Probe(key, out bool wasEmpty);
        if (wasEmpty)
        {
            value = null;
            return false;
        }

        value = _slots[index];
        return true;
    }

    /// <summary>
    /// Determines whether the characters in <paramref name="key"/> are already interned.
    /// </summary>
    /// <param name="key">The characters to look up. An empty span means <c>""</c>.</param>
    /// <returns><c>true</c> if the contents are present; otherwise <c>false</c>.</returns>
    public bool Contains(ReadOnlySpan<char> key)
    {
        Probe(key, out bool wasEmpty);
        return !wasEmpty;
    }

    /// <summary>
    /// Determines whether <paramref name="key"/>'s contents are already interned.
    /// </summary>
    /// <param name="key">The string to look up.</param>
    /// <returns><c>true</c> if the contents are present; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    public bool Contains(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Contains(key.AsSpan());
    }

    /// <summary>
    /// Drops every interned string. The backing capacity is preserved.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        Array.Clear(_slots, 0, _slots.Length);
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Returns an allocation-free enumerator over the interned strings. The order is
    /// unspecified and may change across versions. If the table is modified during
    /// enumeration, <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this table.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<string> IEnumerable<string>.GetEnumerator() => new Enumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

    /// <summary>
    /// A struct enumerator over a <see cref="StringInternTable{THasher}"/>. Because it is a
    /// struct, iterating it via <c>foreach</c> avoids the allocation a compiler-generated
    /// <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<string>
    {
        private readonly StringInternTable<THasher> _table;
        private readonly int _version;
        private int _index;
        private string _current;

        internal Enumerator(StringInternTable<THasher> table)
        {
            _table = table;
            _version = table._version;
            _index = -1;
            _current = null!;
        }

        /// <summary>Gets the string at the current position of the enumerator.</summary>
        public string Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next interned string.</summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new entry; <c>false</c> if it has
        /// passed the end of the table.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// The table was modified since the enumerator was created.
        /// </exception>
        public bool MoveNext()
        {
            if (_version != _table._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            string?[] slots = _table._slots;
            while (++_index < slots.Length)
            {
                string? slot = slots[_index];
                if (slot is not null)
                {
                    _current = slot;
                    return true;
                }
            }

            _current = null!;
            return false;
        }

        /// <summary>Resets the enumerator to its initial position, before the first entry.</summary>
        /// <exception cref="InvalidOperationException">
        /// The table was modified since the enumerator was created.
        /// </exception>
        public void Reset()
        {
            if (_version != _table._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _index = -1;
            _current = null!;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public void Dispose() { }
    }

    // Returns the slot the characters belong in. wasEmpty is true when the slot is vacant
    // (a miss — the caller may write there) and false when it already holds a string with
    // these contents. A vacant slot always exists because the load factor is < 1.
    //
    // The probe walks _slots via Unsafe.Add against a base reference taken at the top, so
    // per-iteration bounds checks disappear; the bound is structural (mask = length - 1).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Probe(ReadOnlySpan<char> key, out bool wasEmpty)
    {
        string?[] slots = _slots;
        ref string? slotsRef = ref MemoryMarshal.GetArrayDataReference(slots);
        int mask = slots.Length - 1;
        int index = _hasher.Hash(key) & mask;

        while (true)
        {
            string? slot = Unsafe.Add(ref slotsRef, (nint)(uint)index);
            if (slot is null) { wasEmpty = true; return index; }
            if (key.SequenceEqual(slot.AsSpan())) { wasEmpty = false; return index; }
            index = (index + 1) & mask;
        }
    }

    // Rehashes every interned string into a table of twice the size. Entries are known to be
    // distinct, so the reinsert loop only has to find the first vacant slot.
    private void Resize()
    {
        int newSize = FastUtils.DoubleCapacity(_slots.Length);
        int mask = newSize - 1;
        string?[] oldSlots = _slots;
        string?[] newSlots = new string?[newSize];

        for (int i = 0; i < oldSlots.Length; i++)
        {
            string? slot = oldSlots[i];
            if (slot is null)
                continue;

            int index = _hasher.Hash(slot) & mask;
            while (newSlots[index] is not null)
                index = (index + 1) & mask;

            newSlots[index] = slot;
        }

        _slots = newSlots;
        _threshold = (int)(newSize * _loadFactor);
    }
}
