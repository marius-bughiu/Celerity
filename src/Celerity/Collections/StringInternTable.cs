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
/// <see cref="StringCityHash64Hasher"/>. Supply a different string hasher via the
/// <see cref="StringInternTable{THasher}"/> generic overload.
/// </summary>
public sealed class StringInternTable : StringInternTable<StringCityHash64Hasher>
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
        int capacity = DefaultCapacity,
        float loadFactor = DefaultLoadFactor)
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
/// <strong>The BCL equivalent.</strong>
/// <c>HashSet&lt;string&gt;.GetAlternateLookup&lt;ReadOnlySpan&lt;char&gt;&gt;()</c>, available on
/// every runtime Celerity targets, expresses the same pattern: its <c>TryGetValue</c> returns the
/// stored instance from a span, and a miss materializes the string once for <c>Add</c>. It
/// allocates the same strings this type does. This type packages the pattern as one
/// <see cref="GetOrAdd(ReadOnlySpan{char})"/> call with the hasher as a type parameter. On half-full
/// tables of 64 tokens, with <see cref="StringCityHash64Hasher"/>, the non-generic table's default, it
/// probed faster than that set on mixed, mostly short tokens on Apple Silicon and x64, but slower at some
/// fixed lengths, mostly from 17 characters up: by up to 1.19x on Apple Silicon and 1.61x on x64. For
/// tokens mostly 17 to 64 characters long, try <see cref="StringXxHash3Hasher"/> through
/// <see cref="StringInternTable{THasher}"/>, which probed faster than the default there on both platforms,
/// but not for tokens past 120 characters: at 128 it probed 2.2x slower than the default on Apple Silicon.
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
    protected const int DefaultCapacity = 16;

    /// <summary>
    /// The default load factor of the table if no load factor is specified.
    /// </summary>
    protected const float DefaultLoadFactor = 0.75f;

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
        int capacity = DefaultCapacity,
        float loadFactor = DefaultLoadFactor)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (!(loadFactor > 0f && loadFactor < 1f))
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
        string? interned = Probe(key, out int index);
        if (interned is not null)
            return interned;

        // The miss path is the only one that materializes a string.
        return AddSlow(key, index);
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

        string? interned = Probe(key.AsSpan(), out int index);
        if (interned is not null)
            return interned;

        return _count < _threshold ? Store(key, index) : GrowAndStore(key);
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
        value = Probe(key, out _);
        return value is not null;
    }

    /// <summary>
    /// Determines whether the characters in <paramref name="key"/> are already interned.
    /// </summary>
    /// <param name="key">The characters to look up. An empty span means <c>""</c>.</param>
    /// <returns><c>true</c> if the contents are present; otherwise <c>false</c>.</returns>
    public bool Contains(ReadOnlySpan<char> key) => Probe(key, out _) is not null;

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

    // Returns the interned string with these contents, or null on a miss. Either way index is the
    // slot the characters belong in; on a miss it is vacant, and the caller may write there. A
    // vacant slot always exists because the load factor is < 1. Returning the string itself spares
    // a hit from reading its slot back.
    //
    // The probe walks _slots via Unsafe.Add against a base reference taken at the top, so
    // per-iteration bounds checks disappear; the bound is structural (mask = length - 1).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string? Probe(ReadOnlySpan<char> key, out int index)
    {
        string?[] slots = _slots;
        ref string? slotsRef = ref MemoryMarshal.GetArrayDataReference(slots);
        int mask = slots.Length - 1;
        int i = _hasher.Hash(key) & mask;

        while (true)
        {
            string? slot = Unsafe.Add(ref slotsRef, (nint)(uint)i);
            if (slot is null) { index = i; return null; }
            if (key.SequenceEqual(slot.AsSpan())) { index = i; return slot; }
            i = (i + 1) & mask;
        }
    }

    // Stores value, whose contents Probe just reported absent, in the vacant slot index it returned.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string Store(string value, int index)
    {
        _slots[index] = value;
        _count++;
        _version++;
        return value;
    }

    // An insert that crosses the load factor. Growing moves every entry, so the slot is probed for again.
    //
    // Kept out of line so that a hit inlines exactly one Probe. The JIT inlines Probe into GetOrAdd, with as
    // much of the hasher as its inlining budget allows, and can inline GetOrAdd into a caller's loop. A
    // post-resize re-probe inlined there doubles the probe code at every call site, and the register
    // pressure that comes with it spills the hit path's values to the stack.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private string GrowAndStore(string value)
    {
        Resize();
        Probe(value.AsSpan(), out int index);
        return Store(value, index);
    }

    // GetOrAdd(ReadOnlySpan<char>)'s miss: materialize the string, then store it. Done inline in GetOrAdd,
    // the ToString call added stack traffic to hits (ARM64, .NET 10). GetOrAdd's own body stored and
    // reloaded the key span on every call. Inlined into a caller's loop, it could spill the span or the
    // probe index. Out here a miss pays one more call instead, and none of GetOrAdd's own values are live
    // across it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private string AddSlow(ReadOnlySpan<char> key, int index)
    {
        string value = key.ToString();
        return _count < _threshold ? Store(value, index) : GrowAndStore(value);
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
