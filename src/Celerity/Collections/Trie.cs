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
/// </remarks>
public sealed class Trie<TValue> : IReadOnlyDictionary<string, TValue>
{
    // A trie node. The root carries no incoming edge; every other node is reached by exactly one edge
    // character from its parent. Child edges are held in two parallel arrays kept sorted ascending by
    // _childChars, so IndexOfChild is a binary search and a pre-order walk visits children in ordinal order.
    private sealed class Node
    {
        // Sorted ascending. ChildChars[i] labels the edge to Children[i]. Only the first ChildCount entries
        // are live. Both arrays start empty and are grown on the first child insert (leaf nodes stay empty).
        public char[] ChildChars = Array.Empty<char>();
        public Node[] Children = Array.Empty<Node>();
        public int ChildCount;

        // The value stored at the key ending exactly here; valid only when HasValue is true.
        public TValue Value = default!;
        public bool HasValue;

        // Binary-searches the child edges for `c`, returning its index or -1 if absent.
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
            return -1;
        }

        // Inserts a fresh child under edge `c`, keeping the parallel arrays sorted, and returns it. The
        // caller must have established via IndexOfChild that `c` is not already present.
        public Node AddChild(char c)
        {
            int lo = 0;
            int hi = ChildCount - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (ChildChars[mid] < c)
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }
            // `lo` is now the insertion point that preserves ascending order.

            if (ChildCount == ChildChars.Length)
            {
                int newCap = ChildChars.Length == 0 ? 2 : ChildChars.Length * 2;
                Array.Resize(ref ChildChars, newCap);
                Array.Resize(ref Children, newCap);
            }

            if (lo < ChildCount)
            {
                Array.Copy(ChildChars, lo, ChildChars, lo + 1, ChildCount - lo);
                Array.Copy(Children, lo, Children, lo + 1, ChildCount - lo);
            }

            var child = new Node();
            ChildChars[lo] = c;
            Children[lo] = child;
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
                throw new ArgumentNullException(nameof(entries), "A key in the entries sequence was null.");
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
    public bool TryGetValue(string key, out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        Node? node = FindNode(key);
        if (node is not null && node.HasValue)
        {
            value = node.Value;
            return true;
        }
        value = default!;
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
        // path[d] is the node reached after consuming d characters; path[0] is the root.
        var path = new Node[len + 1];
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

        // Prune bottom-up: drop any node that now leads to no key (no children, no value). Stop at the first
        // node that must be retained — its ancestors keep it as a child, so they are retained too.
        for (int d = len; d >= 1; d--)
        {
            Node child = path[d];
            if (child.ChildCount != 0 || child.HasValue)
                break;
            Node parent = path[d - 1];
            parent.RemoveChildAt(parent.IndexOfChild(key[d - 1]));
        }

        return true;
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
    public IEnumerable<KeyValuePair<string, TValue>> GetByPrefix(string prefix)
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
    public IEnumerable<string> GetKeysWithPrefix(string prefix) => GetByPrefix(prefix).Select(pair => pair.Key);

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
    public IEnumerable<TValue> Values => Enumerate(_root, string.Empty, _version).Select(pair => pair.Value);

    /// <summary>
    /// Returns an enumerator that yields every entry in ascending ordinal key order. Enumeration allocates a
    /// small traversal stack; if the trie is modified during enumeration,
    /// <see cref="IEnumerator.MoveNext"/> throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>An enumerator over the trie's entries in ascending key order.</returns>
    public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator() => Enumerate(_root, string.Empty, _version).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

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
            node = idx < 0 ? node.AddChild(c) : node.Children[idx];
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

    // Pre-order DFS from `start` (whose accumulated key is `startKey`), yielding each terminating node's entry
    // in ascending ordinal order. Uses an explicit stack and a single reused StringBuilder rather than
    // recursion, so a pathologically long key cannot overflow the call stack. `expectedVersion` is the
    // snapshot taken when the enumerable was created; a mutation since then is detected before the first item
    // and after each yield, so it surfaces on the very first MoveNext (BCL-style), not one item late. A null
    // `start` (a missing prefix) yields nothing but still runs the version check, so the empty result carries
    // the same invalidation contract as a non-empty one.
    private IEnumerable<KeyValuePair<string, TValue>> Enumerate(Node? start, string startKey, int expectedVersion)
    {
        if (expectedVersion != _version)
            ThrowModified();

        if (start is null)
            yield break;

        if (start.HasValue)
        {
            yield return new KeyValuePair<string, TValue>(startKey, start.Value);
            if (expectedVersion != _version)
                ThrowModified();
        }

        var sb = new StringBuilder(startKey);
        var stack = new Stack<(Node Node, int ChildIndex)>();
        stack.Push((start, 0));

        while (stack.Count > 0)
        {
            (Node node, int ci) = stack.Pop();
            if (ci < node.ChildCount)
            {
                // Resume this node at its next child after we finish the subtree we are about to descend.
                stack.Push((node, ci + 1));

                Node child = node.Children[ci];
                sb.Append(node.ChildChars[ci]); // sb now holds the path to `child`
                if (child.HasValue)
                {
                    yield return new KeyValuePair<string, TValue>(sb.ToString(), child.Value);
                    if (expectedVersion != _version)
                        ThrowModified();
                }
                stack.Push((child, 0));
            }
            else if (node != start)
            {
                // Children exhausted: drop the edge character that led into `node`, restoring the parent path.
                sb.Length--;
            }
        }
    }

    private static void ThrowModified() =>
        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
}
