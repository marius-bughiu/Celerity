using System.Collections;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// An <b>immutable</b> prefix tree (trie) over <see cref="string"/> keys stored in <b>two bits per node</b>,
/// the build-once counterpart to <see cref="Trie{TValue}"/>. It answers the same prefix questions —
/// <see cref="GetByPrefix(string)"/>, <see cref="TryGetLongestPrefix(string, out string, out TValue)"/>,
/// ordinal-ordered enumeration — from a succinct encoding rather than from a graph of node objects.
/// </summary>
/// <typeparam name="TValue">The type of the stored values.</typeparam>
/// <remarks>
/// <para>
/// <b>Build once, query many.</b> The whole key set goes to the constructor and there is no mutating member:
/// no <c>Add</c>, no <c>Remove</c>, no indexer setter. A caller whose key set changes must build a new
/// <see cref="SuccinctTrie{TValue}"/>, which costs a sort of the keys plus one level-order pass — so this is
/// the wrong type for anything that inserts as it queries. Reach for <see cref="Trie{TValue}"/> there, and
/// snapshot into a <see cref="SuccinctTrie{TValue}"/> once the keys have settled.
/// </para>
/// <para>
/// The BCL ships no trie at all, so the prefix operations have the same standing against
/// <see cref="Dictionary{TKey, TValue}"/> that <see cref="Trie{TValue}"/> does: enumerating every key under a
/// prefix, or finding the longest stored key that is a prefix of a query, are answered from the structure
/// where a dictionary must scan every entry and run <see cref="string.StartsWith(string)"/> on it. What this
/// type adds over <see cref="Trie{TValue}"/> is <b>footprint</b>, not speed. A pointer-based trie spends an
/// object header, a <c>char[]</c>, a <c>Node[]</c>, a child count, a value slot and a flag on every node,
/// which for a large fixed corpus dwarfs the keys themselves; the encoding below spends two bits of tree
/// shape and one <see cref="char"/> of label.
/// </para>
/// <para>
/// The trade runs the other way on query time, and is stated here rather than rounded away: a descent step
/// resolves the child block with an <c>O(log n)</c> <see cref="RankSelectBitVector.Select0(int)"/> where
/// <see cref="Trie{TValue}"/> follows a reference, so exact lookups and prefix walks are several times
/// slower than the pointer-based trie — and, as with any trie, slower than a <see cref="Dictionary{TKey, TValue}"/> on
/// exact keys, which hashes once instead of walking characters. Reach for this type when the index is large,
/// fixed, and its memory is the thing that hurts.
/// </para>
/// <para>
/// <b>Encoding.</b> The tree shape is a <i>level-order unary degree sequence</i> (LOUDS): visiting nodes in
/// breadth-first order, each contributes one <c>1</c> per child followed by a terminating <c>0</c>, so a tree
/// of <c>n</c> nodes is <c>2n - 1</c> bits held in one <see cref="RankSelectBitVector"/> — this type is the
/// composition that primitive's documentation names. Navigation is the textbook pair: node <c>v</c>'s child
/// block runs from <c>Select0(v - 1) + 1</c> to <c>Select0(v)</c>, and the first child's node number is
/// <c>Rank(start) + 1</c> — with the second select replaced by a scan for the next <c>0</c>, which is a
/// single word read for any ordinary branching factor rather than a second search of the index. Edge labels live in one <see cref="char"/> array indexed by node number, with a
/// node's children contiguous and sorted, so a descent step is a binary search over that slice and
/// enumeration is naturally in ascending ordinal order. Which nodes end a key is a second
/// <see cref="RankSelectBitVector"/> over the node numbers, whose <c>Rank</c> indexes a compact value array —
/// so a node that is only a waypoint costs no value slot at all.
/// </para>
/// <para>
/// Keys are compared and ordered by their UTF-16 code units (ordinal), matching <see cref="Trie{TValue}"/>;
/// culture-aware comparison is not applied. The empty string is a valid key. The type holds no mutable state
/// after construction, so instances are safe to share across threads, and enumeration needs no version check
/// because nothing can invalidate it.
/// </para>
/// <para>
/// The complexities stated on the members count each character step as <c>O(log n)</c> in the node count —
/// the cost of the two selects — plus a binary search over the node's branching factor. The
/// <c>O(key length)</c> shorthand used by <see cref="Trie{TValue}"/> does not apply here; the terms are
/// written out where they matter.
/// </para>
/// </remarks>
public sealed class SuccinctTrie<TValue> : IReadOnlyDictionary<string, TValue?>
{
    // The tree shape: 1 per child then a terminating 0, per node in breadth-first order (2n - 1 bits).
    private readonly RankSelectBitVector _louds;

    // One bit per node, set when the node ends a key. Its Rank is the index into _values.
    private readonly RankSelectBitVector _terminal;

    // The edge character leading into each node, indexed by node number. Element 0 is the root, which has no
    // incoming edge; it is never read.
    private readonly char[] _labels;

    // The values of the terminal nodes, in node order — not in key order.
    private readonly TValue[] _values;

    // The longest key, which bounds both the traversal stack and the path buffer an enumerator needs.
    private readonly int _maxKeyLength;

    /// <summary>
    /// Initializes a new <see cref="SuccinctTrie{TValue}"/> holding the entries of <paramref name="entries"/>.
    /// A later duplicate key overwrites the value set by an earlier one, matching
    /// <see cref="Trie{TValue}"/>'s bulk-load constructor.
    /// </summary>
    /// <param name="entries">The key/value pairs to store.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="entries"/> is <c>null</c>, or any key in it is <c>null</c>.
    /// </exception>
    public SuccinctTrie(IEnumerable<KeyValuePair<string, TValue>> entries)
        : this(SortedEntriesOf(entries))
    {
    }

    /// <summary>
    /// Initializes a new <see cref="SuccinctTrie{TValue}"/> from a snapshot of <paramref name="source"/> — the
    /// way to fill a mutable <see cref="Trie{TValue}"/> and then freeze it. Later changes to
    /// <paramref name="source"/> do not affect the snapshot.
    /// </summary>
    /// <param name="source">The trie whose entries are copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public SuccinctTrie(Trie<TValue> source)
        : this(SortedEntriesOf(EntriesOf(source)))
    {
    }

    // Builds the whole encoding in one breadth-first pass over the sorted keys. A node is a contiguous range
    // of keys sharing a prefix: because the keys are in ordinal order, the key equal to that prefix (if any)
    // sorts first in the range, and the ranges of the children partition the rest by the character at `depth`,
    // already in ascending order.
    private SuccinctTrie((string[] Keys, TValue[] Values) sorted)
    {
        string[] keys = sorted.Keys;
        TValue[] keyValues = sorted.Values;

        var pending = new Queue<(int Lo, int Hi, int Depth)>();
        pending.Enqueue((0, keys.Length, 0));

        // Node 0 is the root, whose label slot is never read.
        var labels = new List<char> { '\0' };
        var childBits = new List<int>();
        var terminalNodes = new List<int>();
        var values = new List<TValue>(keys.Length);

        int bitCount = 0;
        int nodeCount = 0;
        int maxKeyLength = 0;

        while (pending.Count > 0)
        {
            (int lo, int hi, int depth) = pending.Dequeue();

            // Node numbers are assigned on dequeue and children are enqueued in order, so the k-th child
            // enqueued is node k — which is exactly what makes `Rank(start) + 1` name the first child.
            int node = nodeCount++;

            int i = lo;
            if (lo < hi && keys[lo].Length == depth)
            {
                terminalNodes.Add(node);
                values.Add(keyValues[lo]);
                if (depth > maxKeyLength)
                    maxKeyLength = depth;
                i = lo + 1;
            }

            while (i < hi)
            {
                char edge = keys[i][depth];
                int groupStart = i;
                do
                {
                    i++;
                }
                while (i < hi && keys[i][depth] == edge);

                labels.Add(edge);
                childBits.Add(bitCount++);
                pending.Enqueue((groupStart, i, depth + 1));
            }

            bitCount++; // the 0 terminating this node's child block
        }

        _louds = new RankSelectBitVector(bitCount, childBits);
        _terminal = new RankSelectBitVector(nodeCount, terminalNodes);
        _labels = labels.ToArray();
        _values = values.ToArray();
        _maxKeyLength = maxKeyLength;
    }

    /// <summary>Gets the number of keys stored in the trie.</summary>
    public int Count => _values.Length;

    /// <summary>
    /// Gets the number of nodes in the encoded tree — one for the root plus one per distinct prefix of the
    /// stored keys. The tree shape occupies <c>2 × NodeCount - 1</c> bits.
    /// </summary>
    public int NodeCount => _labels.Length;

    /// <summary>
    /// Gets the number of bytes the encoding occupies: the two bit vectors with their rank/select indexes, the
    /// edge labels, and the value array.
    /// </summary>
    /// <remarks>
    /// For a <b>reference-type</b> <typeparamref name="TValue"/> the value array holds references, so the
    /// figure is a <i>floor</i> — it counts the pointers, not the objects they reach, the same caveat
    /// <see cref="SparseTable{T, TMonoid}.IndexSizeInBytes"/> carries. The keys themselves are never stored
    /// and cost nothing beyond their labels: a key exists only as a path through the tree.
    /// </remarks>
    public long IndexSizeInBytes
    {
        get
        {
            long loudsWords = ((long)_louds.Length + 63) / 64 * sizeof(ulong);
            long terminalWords = ((long)_terminal.Length + 63) / 64 * sizeof(ulong);
            return loudsWords + _louds.IndexSizeInBytes
                + terminalWords + _terminal.IndexSizeInBytes
                + ((long)_labels.Length * sizeof(char))
                + ((long)_values.Length * Unsafe.SizeOf<TValue>());
        }
    }

    /// <summary>
    /// Gets the value associated with <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key to read.</param>
    /// <returns>The value associated with <paramref name="key"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <exception cref="KeyNotFoundException"><paramref name="key"/> is not present.</exception>
    public TValue this[string key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);
            int node = FindNode(key.AsSpan());
            if (node < 0 || !_terminal.Get(node))
                throw new KeyNotFoundException($"The key '{key}' was not present in the trie.");
            return ValueOf(node);
        }
    }

    /// <summary>Determines whether the trie contains <paramref name="key"/>.</summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is present; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    public bool ContainsKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return ContainsKey(key.AsSpan());
    }

    /// <summary>
    /// Determines whether the trie contains the characters in <paramref name="key"/>, without materializing a
    /// <see cref="string"/> from the span.
    /// </summary>
    /// <param name="key">The characters to locate. An empty span means the key <c>""</c>.</param>
    /// <returns><c>true</c> if the key is present; otherwise <c>false</c>.</returns>
    public bool ContainsKey(ReadOnlySpan<char> key)
    {
        int node = FindNode(key);
        return node >= 0 && _terminal.Get(node);
    }

    /// <summary>Attempts to get the value associated with <paramref name="key"/>.</summary>
    /// <param name="key">The key to locate.</param>
    /// <param name="value">
    /// When this method returns, the associated value if the key was found; otherwise the default value of
    /// <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    public bool TryGetValue(string key, out TValue? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return TryGetValue(key.AsSpan(), out value);
    }

    /// <summary>
    /// Attempts to get the value associated with the characters in <paramref name="key"/>, without
    /// materializing a <see cref="string"/> from the span.
    /// </summary>
    /// <param name="key">The characters to locate. An empty span means the key <c>""</c>.</param>
    /// <param name="value">
    /// When this method returns, the associated value if the key was found; otherwise the default value of
    /// <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>
    public bool TryGetValue(ReadOnlySpan<char> key, out TValue? value)
    {
        int node = FindNode(key);
        if (node >= 0 && _terminal.Get(node))
        {
            value = ValueOf(node);
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Determines whether any stored key starts with <paramref name="prefix"/> (a key equal to the prefix
    /// counts). The empty prefix matches whenever the trie is non-empty.
    /// </summary>
    /// <param name="prefix">The prefix to test.</param>
    /// <returns><c>true</c> if at least one key has <paramref name="prefix"/> as a prefix; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <c>null</c>.</exception>
    public bool ContainsPrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return ContainsPrefix(prefix.AsSpan());
    }

    /// <summary>
    /// Determines whether any stored key starts with the characters in <paramref name="prefix"/> (a key equal
    /// to the prefix counts), without materializing a <see cref="string"/> from the span. The empty span
    /// matches whenever the trie is non-empty.
    /// </summary>
    /// <param name="prefix">The characters to test as a prefix.</param>
    /// <returns><c>true</c> if at least one key has <paramref name="prefix"/> as a prefix; otherwise <c>false</c>.</returns>
    public bool ContainsPrefix(ReadOnlySpan<char> prefix)
    {
        // A node exists only for a prefix at least one key shares, so reaching one is the whole answer; the
        // root of an empty trie is the single node that leads nowhere, and Count separates that case.
        return FindNode(prefix) >= 0 && Count != 0;
    }

    /// <summary>
    /// Enumerates every entry whose key starts with <paramref name="prefix"/> (an entry whose key equals the
    /// prefix is included), in ascending ordinal key order. The empty prefix enumerates the whole trie.
    /// </summary>
    /// <param name="prefix">The prefix to match.</param>
    /// <returns>A lazily evaluated sequence of the matching entries in ascending key order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <c>null</c>.</exception>
    public IEnumerable<KeyValuePair<string, TValue?>> GetByPrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return Enumerate(FindNode(prefix.AsSpan()), prefix);
    }

    /// <summary>
    /// Enumerates the keys that start with <paramref name="prefix"/> (a key equal to the prefix is included),
    /// in ascending ordinal order. The empty prefix enumerates every key.
    /// </summary>
    /// <param name="prefix">The prefix to match.</param>
    /// <returns>A lazily evaluated sequence of the matching keys in ascending order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <c>null</c>.</exception>
    public IEnumerable<string> GetKeysWithPrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return EnumerateKeys(FindNode(prefix.AsSpan()), prefix);
    }

    /// <summary>
    /// Finds the longest stored key that is a prefix of <paramref name="query"/> (a stored key equal to
    /// <paramref name="query"/> qualifies and is the longest possible match).
    /// </summary>
    /// <param name="query">The string whose stored prefixes are searched.</param>
    /// <param name="key">
    /// When this method returns, the longest stored key that is a prefix of <paramref name="query"/>;
    /// otherwise <c>null</c>.
    /// </param>
    /// <param name="value">
    /// When this method returns, the value associated with <paramref name="key"/>; otherwise the default value
    /// of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if any stored key is a prefix of <paramref name="query"/>; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <c>null</c>.</exception>
    public bool TryGetLongestPrefix(string query, out string? key, out TValue? value)
    {
        ArgumentNullException.ThrowIfNull(query);

        int node = 0;
        int bestLength = -1;
        TValue? bestValue = default;

        if (_terminal.Get(0))
        {
            bestLength = 0;
            bestValue = ValueOf(0);
        }

        for (int d = 0; d < query.Length; d++)
        {
            node = Child(node, query[d]);
            if (node < 0)
                break;
            if (_terminal.Get(node))
            {
                bestLength = d + 1;
                bestValue = ValueOf(node);
            }
        }

        if (bestLength < 0)
        {
            key = null;
            value = default;
            return false;
        }

        // Avoid an allocation on the two common cases: an exact match reuses the query string, and the
        // empty-string key needs no slice; only a proper interior prefix is copied.
        key = bestLength == query.Length ? query
            : bestLength == 0 ? string.Empty
            : query.Substring(0, bestLength);
        value = bestValue;
        return true;
    }

    /// <summary>Gets the keys in ascending ordinal order.</summary>
    public IEnumerable<string> Keys => GetKeysWithPrefix(string.Empty);

    /// <summary>Gets the values ordered by their keys' ascending ordinal order.</summary>
    public IEnumerable<TValue?> Values => EnumerateValues(0, string.Empty);

    /// <summary>
    /// Returns an allocation-free struct enumerator that yields every entry in ascending ordinal key order.
    /// Iterating via <c>foreach</c> avoids the state-machine allocation a compiler-generated iterator would
    /// incur (the traversal itself lazily allocates a small stack and path buffer only when the trie has
    /// children to walk). Nothing can modify the trie, so enumeration never has to check a version.
    /// </summary>
    /// <returns>A struct enumerator over the trie's entries in ascending key order.</returns>
    public Enumerator GetEnumerator() => new(this, 0, string.Empty);

    IEnumerator<KeyValuePair<string, TValue?>> IEnumerable<KeyValuePair<string, TValue?>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // The public indexer getter returns the non-null TValue for a nicer caller experience, so the interface's
    // nullable-value getter is provided explicitly, matching the rest of the Celerity dictionary surface.
    TValue? IReadOnlyDictionary<string, TValue?>.this[string key] => this[key];

    // ---- internal machinery ----------------------------------------------------------------------

    // Deduplicates (later key wins, as Trie's bulk load does) and sorts by ordinal, which is the order the
    // level-order build reads the keys in.
    private static (string[] Keys, TValue[] Values) SortedEntriesOf(IEnumerable<KeyValuePair<string, TValue>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var unique = new Dictionary<string, TValue>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, TValue> entry in entries)
        {
            if (entry.Key is null)
                throw new ArgumentNullException("key", "A key in the entries sequence was null.");
            unique[entry.Key] = entry.Value;
        }

        string[] keys = new string[unique.Count];
        unique.Keys.CopyTo(keys, 0);
        Array.Sort(keys, StringComparer.Ordinal);

        TValue[] values = new TValue[keys.Length];
        for (int i = 0; i < keys.Length; i++)
            values[i] = unique[keys[i]];

        return (keys, values);
    }

    private static IEnumerable<KeyValuePair<string, TValue>> EntriesOf(Trie<TValue> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (KeyValuePair<string, TValue?> entry in source)
            yield return new KeyValuePair<string, TValue>(entry.Key, entry.Value!);
    }

    // The first bit of `node`'s child block. Node 0 starts the vector; every other node's block begins one
    // past the 0 that terminated the previous node's block.
    private int ChildBlockStart(int node) => node == 0 ? 0 : _louds.Select0(node - 1) + 1;

    // One past the last bit of `node`'s child block: the position of the 0 that terminates it. Textbook LOUDS
    // names this `Select0(node)`, but the block is a run of 1s starting at `start`, so the terminating 0 is
    // the *next* one — a word scan from a known position rather than a second binary search over the
    // superblock index, and one that reads a single word for any ordinary branching factor.
    private int ChildBlockEnd(int start) => _louds.NextZero(start);

    // The node reached by following `edge` out of `node`, or -1 when there is no such edge. A node's children
    // are contiguous in node number and their labels ascend, so this is a binary search over a label slice.
    private int Child(int node, char edge)
    {
        int start = ChildBlockStart(node);
        int end = ChildBlockEnd(start);
        if (start == end)
            return -1;

        // The 1-bits of the vector are the non-root nodes in node order, so the bit at `start` belongs to the
        // first child and its node number is one past the set bits before it.
        int lo = _louds.Rank(start) + 1;
        int hi = lo + (end - start) - 1;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            char label = _labels[mid];
            if (label == edge)
                return mid;
            if (label < edge)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return -1;
    }

    // Walks `key` from the root and returns the node it ends on, or -1 if the path breaks.
    private int FindNode(ReadOnlySpan<char> key)
    {
        int node = 0;
        for (int i = 0; i < key.Length; i++)
        {
            node = Child(node, key[i]);
            if (node < 0)
                return -1;
        }

        return node;
    }

    private TValue ValueOf(int node) => _values[_terminal.Rank(node)];

    private IEnumerable<KeyValuePair<string, TValue?>> Enumerate(int start, string startKey)
    {
        Enumerator walk = new(this, start, startKey);
        while (walk.MoveNext())
            yield return walk.Current;
    }

    private IEnumerable<string> EnumerateKeys(int start, string startKey)
    {
        Enumerator walk = new(this, start, startKey);
        while (walk.MoveNext())
            yield return walk.Current.Key;
    }

    private IEnumerable<TValue?> EnumerateValues(int start, string startKey)
    {
        Enumerator walk = new(this, start, startKey);
        while (walk.MoveNext())
            yield return walk.Current.Value;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="SuccinctTrie{TValue}"/> subtree that yields entries in ascending
    /// ordinal key order. Because it is a struct, iterating via <c>foreach</c> avoids the allocation a
    /// compiler-generated <c>IEnumerator</c> would incur; it lazily allocates a small traversal stack and a
    /// path buffer only when the start node has children to walk.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<string, TValue?>>
    {
        // Each stack frame is three ints describing one node on the path from the subtree root to the pending
        // one: its first child's node number, how many of its children have been descended into, and how many
        // it has. Children are consecutive node numbers, so the first plus the offset names the next one.
        private const int FrameSize = 3;

        private readonly SuccinctTrie<TValue> _trie;
        private readonly int _start;       // subtree root; its accumulated key is _startKey
        private readonly string _startKey;

        private int[]? _stack;             // allocated on first descent
        private char[]? _path;             // the characters of the key of the node at the top frame
        private int _frames;
        private int _depth;
        private KeyValuePair<string, TValue?> _current;
        private int _phase;                // 0 = not started, 1 = walking, 2 = done

        internal Enumerator(SuccinctTrie<TValue> trie, int start, string startKey)
        {
            _trie = trie;
            _start = start;
            _startKey = startKey;
            _stack = null;
            _path = null;
            _frames = 0;
            _depth = startKey.Length;
            _current = default;
            _phase = 0;
        }

        /// <summary>Gets the entry at the current position of the enumerator.</summary>
        public readonly KeyValuePair<string, TValue?> Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next entry in ascending key order.</summary>
        /// <returns><c>true</c> if the enumerator advanced to a new entry; otherwise <c>false</c>.</returns>
        public bool MoveNext()
        {
            if (_phase == 2)
                return false;

            if (_phase == 0)
            {
                _phase = 1;

                // A prefix that no key shares has no subtree to walk, and neither does an empty trie.
                if (_start < 0)
                {
                    _phase = 2;
                    return false;
                }

                // Only set up the traversal state when there is a subtree to walk, so a leaf result — or an
                // empty trie — allocates nothing.
                PushFrame(_start);

                if (_trie._terminal.Get(_start))
                {
                    _current = new KeyValuePair<string, TValue?>(_startKey, _trie.ValueOf(_start));
                    return true;
                }
            }

            while (_frames > 0)
            {
                int top = (_frames - 1) * FrameSize;
                if (_stack![top + 1] < _stack[top + 2])
                {
                    int child = _stack[top] + _stack[top + 1]++;
                    _path![_depth++] = _trie._labels[child];
                    PushFrame(child);
                    if (_trie._terminal.Get(child))
                    {
                        _current = new KeyValuePair<string, TValue?>(new string(_path, 0, _depth), _trie.ValueOf(child));
                        return true;
                    }
                }
                else
                {
                    // Children exhausted: drop the edge character that led into this node.
                    _frames--;
                    _depth--;
                }
            }

            _phase = 2;
            _current = default;
            return false;
        }

        // Pushes `node`'s child block onto the stack, allocating the traversal buffers the first time a node
        // with children is reached. A childless *start* node needs no frame at all; a childless node reached
        // during the walk still gets one, because popping it is what undoes the `_depth++` that descended
        // into it.
        private void PushFrame(int node)
        {
            int start = _trie.ChildBlockStart(node);
            int end = _trie.ChildBlockEnd(start);
            if (start == end && _stack is null)
                return;

            if (_stack is null)
            {
                // The deepest reachable node is the longest key, so the walk below `_startKey` can never need
                // more frames — or more path characters — than that.
                _stack = new int[((_trie._maxKeyLength - _startKey.Length) + 1) * FrameSize];
                _path = new char[_trie._maxKeyLength];
                _startKey.CopyTo(0, _path, 0, _startKey.Length);
            }

            int frame = _frames++ * FrameSize;
            _stack[frame] = start == end ? 0 : _trie._louds.Rank(start) + 1;
            _stack[frame + 1] = 0;
            _stack[frame + 2] = end - start;
        }

        /// <summary>Resets the enumerator to its initial position, before the first entry.</summary>
        public void Reset()
        {
            _stack = null;
            _path = null;
            _frames = 0;
            _depth = _startKey.Length;
            _current = default;
            _phase = 0;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public readonly void Dispose() { }
    }
}
