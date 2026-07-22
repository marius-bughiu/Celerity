using System.Buffers;
using System.Collections;
using System.Text;

namespace Celerity.Collections;

/// <summary>
/// An ordered <b>prefix tree</b> (trie) mapping <see cref="string"/> keys to values, where every key is
/// stored as a path of characters from a shared root so that keys sharing a prefix share that prefix's nodes.
/// </summary>
/// <typeparam name="TValue">The type of the stored values.</typeparam>
/// <remarks>
/// <para>
/// The BCL ships no trie. <see cref="Dictionary{TKey, TValue}"/> answers an exact-key lookup in <c>O(1)</c>,
/// but it has <b>no efficient prefix operation</b>: enumerating every key that starts with a given prefix, or
/// finding the longest stored key that is a prefix of a query, both force an <c>O(n)</c> scan of the whole
/// dictionary plus a <see cref="string.StartsWith(string)"/> per key. A trie answers those directly from the
/// structure — it is the .NET analogue of a classic prefix tree / radix map.
/// </para>
/// <para>
/// The documented BCL-beating workloads are the <b>prefix operations</b>:
/// <list type="bullet">
/// <item><description>
/// <see cref="GetByPrefix(string)"/> / <see cref="GetKeysWithPrefix(string)"/> yield every entry whose key
/// starts with a prefix in <c>O(prefix length + matches)</c> — autocomplete, typeahead, listing a namespace
/// or route table — where <see cref="Dictionary{TKey, TValue}"/> must scan and filter every entry.
/// </description></item>
/// <item><description>
/// <see cref="TryGetLongestPrefix(string, out string, out TValue)"/> finds the longest stored key that is a
/// prefix of a query in <c>O(query length)</c> — routing tables, tokenizer / dictionary matching,
/// filesystem-style longest-match.
/// </description></item>
/// <item><description>
/// Enumeration yields keys in <b>ascending ordinal order</b> for free, where a
/// <see cref="Dictionary{TKey, TValue}"/> is unordered.
/// </description></item>
/// </list>
/// An exact <see cref="Add(string, TValue)"/> or <see cref="TryGetValue(string, out TValue)"/> walks the key
/// character by character rather than hashing it once, so for pure exact-key workloads a
/// <see cref="Dictionary{TKey, TValue}"/> is competitive or faster — the trie's value is the prefix and
/// ordering operations, not raw exact-lookup speed.
/// </para>
/// <para>
/// Keys are compared and ordered by their UTF-16 code units (ordinal), the same comparison
/// <see cref="Dictionary{TKey, TValue}"/> uses with the ordinal comparer; culture-aware comparison is not
/// applied. The empty string is a valid key. Each node stores its child edges in an array kept sorted by
/// edge character, so a child lookup is a binary search and enumeration is naturally ordered. Removal prunes
/// nodes that no longer lead to a key, so the structure never retains dead paths. This type is not
/// thread-safe; concurrent callers must synchronize externally.
/// </para>
/// <para>
/// The complexities stated on the members (<c>O(key length)</c>, <c>O(prefix length + matches)</c>, and so
/// on) count each character step as <c>O(1)</c>; strictly, navigating one node's children is a binary search,
/// so a character step is <c>O(log b)</c> in that node's branching factor <c>b</c>. For the common
/// bounded-alphabet case <c>b</c> is a small constant, so the simplified length-proportional forms hold; on a
/// pathologically wide alphabet, multiply the character-length terms by <c>log b</c>.
/// </para>
/// </remarks>
public sealed class Trie<TValue> : IReadOnlyDictionary<string, TValue?>
{
    // A trie node. The root carries no incoming edge; every other node is reached by exactly one edge
    // character from its parent. Child edges are held in two parallel arrays kept sorted ascending by
    // _childChars, so IndexOfChild is a binary search and a pre-order walk visits children in ordinal order.
    // Internal (not private) so the public struct Enumerator's constructor can name it as a parameter type
    // without an accessibility-consistency error; it is still not part of the public API surface.
    internal sealed class Node
    {
        // Sorted ascending. ChildChars[i] labels the edge to Children[i]. Only the first ChildCount entries
        // are live. Both arrays start empty and are grown on the first child insert (leaf nodes stay empty).
        public char[] ChildChars = Array.Empty<char>();
        public Node[] Children = Array.Empty<Node>();
        public int ChildCount;

        // The value stored at the key ending exactly here; valid only when HasValue is true.
        public TValue Value = default!;
        public bool HasValue;

        // Binary-searches the child edges for `c`. Returns the child's index if present, otherwise the
        // bitwise complement of the insertion point that would keep the edges sorted (the
        // Array.BinarySearch convention), so an inserting caller can add without re-searching. Callers that
        // only test presence just check `index < 0`.
        public int IndexOfChild(char c)
        {
            int lo = 0;
            int hi = ChildCount - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                char m = ChildChars[mid];
                if (m == c)
                    return mid;
                if (m < c)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }
            return ~lo;
        }

        // Inserts a fresh child under edge `c` at `index` (the insertion point from a prior IndexOfChild
        // miss, i.e. `~IndexOfChild(c)`), keeping the parallel arrays sorted, and returns it.
        public Node AddChildAt(int index, char c)
        {
            if (ChildCount == ChildChars.Length)
            {
                int newCap = ChildChars.Length == 0 ? 2 : ChildChars.Length * 2;
                Array.Resize(ref ChildChars, newCap);
                Array.Resize(ref Children, newCap);
            }

            if (index < ChildCount)
            {
                Array.Copy(ChildChars, index, ChildChars, index + 1, ChildCount - index);
                Array.Copy(Children, index, Children, index + 1, ChildCount - index);
            }

            var child = new Node();
            ChildChars[index] = c;
            Children[index] = child;
            ChildCount++;
            return child;
        }

        // Removes the child at `index`, shifting the tail down so the arrays stay sorted and dense.
        public void RemoveChildAt(int index)
        {
            int tail = ChildCount - index - 1;
            if (tail > 0)
            {
                Array.Copy(ChildChars, index + 1, ChildChars, index, tail);
                Array.Copy(Children, index + 1, Children, index, tail);
            }
            ChildCount--;
            ChildChars[ChildCount] = '\0';
            Children[ChildCount] = null!;
        }
    }

    private readonly Node _root = new();
    private int _count;

    // Bumped on every mutation (add, overwrite, remove, clear) so active enumerators detect concurrent
    // modification and throw, matching the BCL collection contract.
    private int _version;

    /// <summary>Initializes a new, empty trie.</summary>
    public Trie()
    {
    }

    /// <summary>
    /// Initializes a new trie populated with the entries of <paramref name="entries"/>. A later duplicate key
    /// overwrites the value set by an earlier one, matching <see cref="Dictionary{TKey, TValue}"/>'s
    /// collection-initializer-style bulk load via the indexer.
    /// </summary>
    /// <param name="entries">The key/value pairs to insert.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="entries"/> is <c>null</c>, or any key in it is <c>null</c>.
    /// </exception>
    public Trie(IEnumerable<KeyValuePair<string, TValue>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (KeyValuePair<string, TValue> entry in entries)
        {
            if (entry.Key is null)
                throw new ArgumentNullException("key", "A key in the entries sequence was null.");
            Set(entry.Key, entry.Value);
        }
        _version = 0;
    }

    /// <summary>Gets the number of keys stored in the trie.</summary>
    public int Count => _count;

    /// <summary>
    /// Gets or sets the value associated with <paramref name="key"/>. The getter throws when the key is
    /// absent; the setter adds the key or overwrites its existing value.
    /// </summary>
    /// <param name="key">The key to read or write.</param>
    /// <returns>The value associated with <paramref name="key"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <exception cref="KeyNotFoundException">The getter is used and <paramref name="key"/> is not present.</exception>
    public TValue this[string key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);
            Node? node = FindNode(key);
            if (node is null || !node.HasValue)
                throw new KeyNotFoundException($"The key '{key}' was not present in the trie.");
            return node.Value;
        }
        set
        {
            ArgumentNullException.ThrowIfNull(key);
            Set(key, value);
        }
    }

    /// <summary>
    /// Adds <paramref name="key"/> with <paramref name="value"/> to the trie.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is already present.</exception>
    public void Add(string key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!TryInsert(key, value, overwrite: false))
            throw new ArgumentException($"An entry with the key '{key}' already exists.", nameof(key));
    }

    /// <summary>
    /// Attempts to add <paramref name="key"/> with <paramref name="value"/>, leaving an existing entry
    /// unchanged rather than throwing or overwriting.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <returns><c>true</c> if the key was added; <c>false</c> if it was already present.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    public bool TryAdd(string key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return TryInsert(key, value, overwrite: false);
    }

    /// <summary>Determines whether the trie contains <paramref name="key"/>.</summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is present; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    public bool ContainsKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        Node? node = FindNode(key);
        return node is not null && node.HasValue;
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
        Node? node = FindNode(key);
        if (node is not null && node.HasValue)
        {
            value = node.Value;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>Removes <paramref name="key"/> from the trie.</summary>
    /// <param name="key">The key to remove.</param>
    /// <returns><c>true</c> if the key was found and removed; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    public bool Remove(string key) => Remove(key, out _);

    /// <summary>
    /// Removes <paramref name="key"/> from the trie and returns the value it held.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <param name="value">
    /// When this method returns, the removed value if the key was found; otherwise the default value of
    /// <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found and removed; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    public bool Remove(string key, out TValue? value)
    {
        ArgumentNullException.ThrowIfNull(key);

        int len = key.Length;

        // Record the path so we can prune empty nodes on the way back up without parent pointers.
        // path[d] is the node reached after consuming d characters; path[0] is the root. childSlot[d] is the
        // index of path[d + 1] within path[d]'s child arrays — captured on the way down so the prune pass
        // reuses it instead of re-running IndexOfChild per level. Both are rented from the pool so a frequent
        // Remove over long keys allocates nothing per call.
        Node[] path = ArrayPool<Node>.Shared.Rent(len + 1);
        int[] childSlot = len == 0 ? Array.Empty<int>() : ArrayPool<int>.Shared.Rent(len);
        try
        {
            path[0] = _root;
            Node node = _root;
            for (int d = 0; d < len; d++)
            {
                int idx = node.IndexOfChild(key[d]);
                if (idx < 0)
                {
                    value = default;
                    return false;
                }
                childSlot[d] = idx;
                node = node.Children[idx];
                path[d + 1] = node;
            }

            if (!node.HasValue)
            {
                value = default;
                return false;
            }

            value = node.Value;
            node.Value = default!;
            node.HasValue = false;
            _count--;
            _version++;

            // Prune bottom-up: drop any node that now leads to no key (no children, no value). Stop at the
            // first node that must be retained — its ancestors keep it as a child, so they are retained too.
            // Each level's child slot is the one captured during the descent (no node moved since), so no
            // re-search is needed.
            for (int d = len; d >= 1; d--)
            {
                Node child = path[d];
                if (child.ChildCount != 0 || child.HasValue)
                    break;
                path[d - 1].RemoveChildAt(childSlot[d - 1]);
            }

            return true;
        }
        finally
        {
            // Clear the Node[] on return so pooled slots don't root nodes past this call; the int[] holds no
            // references, so it is returned without clearing.
            ArrayPool<Node>.Shared.Return(path, clearArray: true);
            if (len != 0)
                ArrayPool<int>.Shared.Return(childSlot);
        }
    }

    /// <summary>Removes all keys from the trie.</summary>
    public void Clear()
    {
        if (_count == 0 && _root.ChildCount == 0)
            return;

        _root.ChildChars = Array.Empty<char>();
        _root.Children = Array.Empty<Node>();
        _root.ChildCount = 0;
        _root.Value = default!;
        _root.HasValue = false;
        _count = 0;
        _version++;
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
        Node? node = FindNode(prefix);
        // Because removal prunes dead paths, a surviving node either terminates a key or has descendants that
        // do; the only node that can exist while leading to no key is the root of an empty trie.
        return node is not null && (node.HasValue || node.ChildCount != 0);
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
        Node? node = FindNode(prefix);
        // Snapshot the version now (at enumerable creation), matching the BCL contract where a modification
        // between handing out the enumerable and iterating it is detected on the first MoveNext. A missing
        // prefix (node is null) still flows through the version-checked walk, so the empty result honours the
        // same invalidation contract as a matching one.
        return Enumerate(node, prefix, _version);
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
        Node? node = FindNode(prefix);
        return EnumerateKeys(node, prefix, _version);
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

        Node node = _root;
        int bestLength = -1;
        TValue? bestValue = default;

        if (node.HasValue)
        {
            bestLength = 0;
            bestValue = node.Value;
        }

        for (int d = 0; d < query.Length; d++)
        {
            int idx = node.IndexOfChild(query[d]);
            if (idx < 0)
                break;
            node = node.Children[idx];
            if (node.HasValue)
            {
                bestLength = d + 1;
                bestValue = node.Value;
            }
        }

        if (bestLength < 0)
        {
            key = null;
            value = default;
            return false;
        }

        // Avoid an allocation on the two common cases: an exact match (the whole query is the key) reuses the
        // query string, and the empty-string key needs no slice; only a proper interior prefix is copied.
        key = bestLength == query.Length ? query
            : bestLength == 0 ? string.Empty
            : query.Substring(0, bestLength);
        value = bestValue;
        return true;
    }

    /// <summary>Gets the keys in ascending ordinal order.</summary>
    public IEnumerable<string> Keys => GetKeysWithPrefix(string.Empty);

    /// <summary>Gets the values ordered by their keys' ascending ordinal order.</summary>
    public IEnumerable<TValue?> Values => EnumerateValues(_root, _version);

    /// <summary>
    /// Returns an allocation-free struct enumerator that yields every entry in ascending ordinal key order.
    /// Iterating via <c>foreach</c> avoids the state-machine allocation a compiler-generated iterator would
    /// incur (the traversal itself lazily allocates a small stack only when the trie has children to walk). If
    /// the trie is structurally modified during enumeration, <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over the trie's entries in ascending key order.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this, _root, string.Empty, _version);

    IEnumerator<KeyValuePair<string, TValue?>> IEnumerable<KeyValuePair<string, TValue?>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // IReadOnlyDictionary<string, TValue?> indexer: the public indexer getter returns the non-null TValue for a
    // nicer caller experience, so the interface's nullable-value getter is provided explicitly, matching the
    // rest of the Celerity dictionary surface (e.g. CelerityDictionary).
    TValue? IReadOnlyDictionary<string, TValue?>.this[string key] => this[key];

    // ---- internal machinery ----------------------------------------------------------------------

    // Walks the key from the root and returns the node it ends on, or null if the path breaks.
    private Node? FindNode(string key)
    {
        Node node = _root;
        for (int i = 0; i < key.Length; i++)
        {
            int idx = node.IndexOfChild(key[i]);
            if (idx < 0)
                return null;
            node = node.Children[idx];
        }
        return node;
    }

    // Sets a key's value unconditionally (add or overwrite), used by the indexer and the bulk constructor.
    private void Set(string key, TValue value) => TryInsert(key, value, overwrite: true);

    // Walks/creates the path for `key` and stores `value` at its terminal node. When `overwrite` is false and
    // the key already exists, nothing changes and false is returned; otherwise the write happens and true is
    // returned. Count is bumped only when a new key materializes.
    private bool TryInsert(string key, TValue value, bool overwrite)
    {
        Node node = _root;
        for (int i = 0; i < key.Length; i++)
        {
            char c = key[i];
            int idx = node.IndexOfChild(c);
            // One search per character: reuse the miss's insertion point (~idx) instead of re-searching.
            node = idx < 0 ? node.AddChildAt(~idx, c) : node.Children[idx];
        }

        if (node.HasValue)
        {
            if (!overwrite)
                return false;
            node.Value = value;
            _version++;
            return true;
        }

        node.Value = value;
        node.HasValue = true;
        _count++;
        _version++;
        return true;
    }

    // Pre-order DFS from `start` (whose accumulated key is `startKey`) as an IEnumerable, for the lazy
    // prefix / keys streams. It drives the same struct Enumerator that GetEnumerator exposes, so the pre-order
    // traversal and modification-detection live in one place. A null `start` (a missing prefix) yields nothing
    // but still runs the version check, so an empty result honours the same invalidation contract as a
    // non-empty one.
    private IEnumerable<KeyValuePair<string, TValue?>> Enumerate(Node? start, string startKey, int expectedVersion)
    {
        if (expectedVersion != _version)
            ThrowModified();

        if (start is null)
            yield break;

        var e = new Enumerator(this, start, startKey, expectedVersion);
        while (e.MoveNext())
            yield return e.Current;
    }

    // Key-only projection of the pair walk (a plain iterator block, not LINQ): the key string is genuinely
    // needed here, and the discarded KeyValuePair wrapper is a stack struct, so there is nothing to save by
    // duplicating the traversal.
    private IEnumerable<string> EnumerateKeys(Node? start, string startKey, int expectedVersion)
    {
        foreach (KeyValuePair<string, TValue?> pair in Enumerate(start, startKey, expectedVersion))
            yield return pair.Key;
    }

    // Value-only DFS. Unlike Enumerate it never builds the key string (no StringBuilder, no
    // sb.ToString(), no KeyValuePair), so the Values view does zero per-item allocation. It mirrors
    // Enumerate's version-check and stack traversal, minus the key bookkeeping (there is no edge char to
    // append or backtrack when the key is not produced).
    private IEnumerable<TValue?> EnumerateValues(Node? start, int expectedVersion)
    {
        if (expectedVersion != _version)
            ThrowModified();

        if (start is null)
            yield break;

        if (start.HasValue)
        {
            yield return start.Value;
            if (expectedVersion != _version)
                ThrowModified();
        }

        if (start.ChildCount == 0)
            yield break;

        var stack = new Stack<(Node Node, int ChildIndex)>();
        stack.Push((start, 0));

        while (stack.Count > 0)
        {
            (Node node, int ci) = stack.Pop();
            if (ci < node.ChildCount)
            {
                stack.Push((node, ci + 1));

                Node child = node.Children[ci];
                if (child.HasValue)
                {
                    yield return child.Value;
                    if (expectedVersion != _version)
                        ThrowModified();
                }
                stack.Push((child, 0));
            }
        }
    }

    private static void ThrowModified() =>
        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

    /// <summary>
    /// A struct enumerator over a <see cref="Trie{TValue}"/> subtree that yields entries in ascending ordinal
    /// key order. Because it is a struct, iterating via <c>foreach</c> avoids the allocation a
    /// compiler-generated <c>IEnumerator</c> would incur; it lazily allocates a small traversal stack and a
    /// <see cref="StringBuilder"/> only when the start node has children to walk.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<string, TValue?>>
    {
        private readonly Trie<TValue> _trie;
        private readonly Node _start;      // subtree root; its accumulated key is _startKey
        private readonly string _startKey;
        private readonly int _version;

        private Stack<(Node Node, int ChildIndex)>? _stack; // allocated on first descent
        private StringBuilder? _sb;                          // holds the path to the pending node
        private KeyValuePair<string, TValue?> _current;
        private int _phase;                                  // 0 = not started, 1 = walking, 2 = done

        internal Enumerator(Trie<TValue> trie, Node start, string startKey, int version)
        {
            _trie = trie;
            _start = start;
            _startKey = startKey;
            _version = version;
            _stack = null;
            _sb = null;
            _current = default;
            _phase = 0;
        }

        /// <summary>Gets the entry at the current position of the enumerator.</summary>
        public readonly KeyValuePair<string, TValue?> Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next entry in ascending key order.</summary>
        /// <returns><c>true</c> if the enumerator advanced to a new entry; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The trie was modified since the enumerator was created.</exception>
        public bool MoveNext()
        {
            if (_version != _trie._version)
                ThrowModified();

            if (_phase == 2)
                return false;

            if (_phase == 0)
            {
                _phase = 1;

                // Only set up the traversal state when there is a subtree to walk (a leaf or empty trie needs
                // neither), so single-node results allocate nothing.
                if (_start.ChildCount != 0)
                {
                    _sb = new StringBuilder(_startKey);
                    _stack = new Stack<(Node, int)>();
                    _stack.Push((_start, 0));
                }

                if (_start.HasValue)
                {
                    _current = new KeyValuePair<string, TValue?>(_startKey, _start.Value);
                    return true;
                }
            }

            if (_stack is null)
            {
                _phase = 2;
                return false;
            }

            while (_stack.Count > 0)
            {
                (Node node, int ci) = _stack.Pop();
                if (ci < node.ChildCount)
                {
                    // Resume this node at its next child after we finish the subtree we are about to descend.
                    _stack.Push((node, ci + 1));

                    Node child = node.Children[ci];
                    _sb!.Append(node.ChildChars[ci]); // _sb now holds the path to `child`
                    _stack.Push((child, 0));
                    if (child.HasValue)
                    {
                        _current = new KeyValuePair<string, TValue?>(_sb.ToString(), child.Value);
                        return true;
                    }
                }
                else if (node != _start)
                {
                    // Children exhausted: drop the edge char that led into `node`, restoring the parent path.
                    _sb!.Length--;
                }
            }

            _phase = 2;
            return false;
        }

        /// <summary>Resets the enumerator to its initial position, before the first entry.</summary>
        /// <exception cref="InvalidOperationException">The trie was modified since the enumerator was created.</exception>
        public void Reset()
        {
            if (_version != _trie._version)
                ThrowModified();

            _stack = null;
            _sb = null;
            _current = default;
            _phase = 0;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public readonly void Dispose() { }
    }
}
