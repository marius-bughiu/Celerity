using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Celerity.Collections;

/// <summary>
/// A <b>rope</b>: a balanced tree of bounded character runs, so an edit anywhere in a large block of text
/// costs <c>O(log n)</c> instead of the <c>O(n)</c> that shifting a contiguous buffer costs — the container
/// for <i>text that keeps changing in the middle</i>, which is the one text operation
/// <see cref="System.Text.StringBuilder"/> is linear in.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the library's only mutable text type.</b> <see cref="Trie{TValue}"/>,
/// <see cref="SuffixArray"/> and <see cref="AhoCorasick"/> all <i>search</i> text that does not change;
/// this one <i>edits</i> it and does not search it. Reach for those to find something, and for this to
/// change something.
/// </para>
/// <para>
/// <b>Why not <see cref="System.Text.StringBuilder"/>.</b> A <see cref="System.Text.StringBuilder"/> is a
/// linked list of chunks whose head is the <i>end</i> of the text, which makes appending excellent and
/// everything else linear in the document: <c>Insert</c> and <c>Remove</c> walk the chunk list to reach the
/// position and then shift what follows, and even the indexer walks that list. Ten times the document is ten
/// times the cost of one edit. A rope descends a tree instead, so the cost of an edit is set by the
/// <i>depth</i> of the document rather than its length, and the only characters moved are the ones inside a
/// single leaf.
/// </para>
/// <para>
/// <b>Layout.</b> An AVL tree. Leaves hold a <see cref="char"/> buffer of at most <see cref="ChunkSize"/>
/// code units; internal nodes hold no text and cache their subtree's character count, leaf count and height,
/// so locating a position is a descent comparing indices and never touches a leaf until it arrives. Because a
/// rope owns its nodes exclusively, they are mutated in place rather than rebuilt — <b>an insert whose target
/// leaf has room allocates nothing at all</b> and is one <see cref="Array.Copy(Array, int, Array, int, int)"/>
/// of at most <see cref="ChunkSize"/> characters.
/// </para>
/// <para>
/// <b>Splitting and joining are the operations nothing in the BCL has.</b> <see cref="Split(int)"/> cuts the
/// rope in two and <see cref="AppendAndClear(Rope)"/> joins two back together, both in <c>O(log n)</c>, where
/// every BCL equivalent — <c>string.Concat</c>, <c>Substring</c>, slicing a span and copying — is a full copy.
/// What a split copies is at most the one leaf the cut falls inside, never the document.
/// <see cref="AppendAndClear(Rope)"/> empties its argument, and is named for it: joining is a <i>move</i> of
/// the source's nodes, not a copy of its characters, so leaving the source pointing at buffers this rope now
/// mutates would alias them. A copying join is <c>rope.Append(other.ToString())</c> and costs the copy.
/// </para>
/// <para>
/// <b>Leaves are built with slack, on purpose.</b> A rebuild fills each leaf to three quarters of
/// <see cref="ChunkSize"/> rather than to the brim, because a leaf with no room left splits on the very first
/// character inserted into it — and a rope whose leaves are all full splits one on <i>every</i> edit, turning
/// what should be an in-place <c>memmove</c> into two allocations and a rebalance. The quarter left empty is
/// what buys the allocation-free path its hit rate, and it is the reason a compact rope costs about 2.7 bytes
/// per character rather than 2.
/// </para>
/// <para>
/// <b>Fragmentation is the remaining cost, and it is amortized away.</b> Edits still eventually overflow
/// leaves and split them, driving the average fill down and the tree deeper than the character count warrants.
/// The rope tracks <see cref="LeafCount"/> and rebuilds itself once that passes twice what the length needs.
/// The rebuild is <c>O(n)</c> for the single call that pays it and cannot recur until <c>n / ChunkSize</c>
/// further edits have re-fragmented the tree, which is <c>O(ChunkSize)</c> amortized per edit — the same shape
/// as the growth resize inside <see cref="TimerWheel{TValue}.Schedule"/>. <see cref="TrimExcess"/> forces one,
/// and <see cref="LeafCount"/> and <see cref="Depth"/> are public so the trade is observable rather than
/// folklore.
/// </para>
/// <para>
/// <b>What this costs you, and it is not nothing.</b> Three operations are outright losses and ship as such.
/// <see cref="Append(char)"/> is <see cref="System.Text.StringBuilder"/>'s home turf — a bounds check and a
/// store, against a tree descent — and loses by more than an order of magnitude, so text that is only ever
/// appended to belongs in a <see cref="System.Text.StringBuilder"/>. Random access through the indexer loses
/// too, because a <see cref="System.Text.StringBuilder"/> built from a string is a single chunk and indexes it
/// directly. <see cref="ToString()"/> is a full copy for both and slightly slower here. Memory is higher:
/// every leaf carries a <see cref="ChunkSize"/>-character buffer whatever it holds, so a compact rope is about
/// 2.7 bytes per character and a fragmented one more. <b>What this type is for is editing</b> — insert,
/// remove, split, join — and that is where it wins.
/// </para>
/// <para>
/// <b>Characters, not text elements.</b> Every index is a UTF-16 code unit, exactly as
/// <see cref="System.Text.StringBuilder"/> and <see cref="string"/> index. A rope will happily split a
/// surrogate pair or a combining sequence if that is where it is told to cut; callers who need grapheme
/// boundaries should find them with <see cref="System.Globalization.StringInfo"/> first.
/// </para>
/// <para>
/// Enumeration yields characters in order, and <see cref="GetChunks"/> yields the underlying runs as
/// <see cref="ReadOnlySpan{T}"/> for a zero-copy read path, mirroring
/// <see cref="System.Text.StringBuilder.GetChunks"/>. Both are invalidated by any operation that changes the
/// text. Assigning through the indexer is deliberately <i>not</i> such an operation: it replaces one code unit
/// in place, moves nothing and changes no chunk boundary, so an in-flight enumerator stays valid — the same
/// call this library's dictionaries make when an indexer overwrites an existing key.
/// </para>
/// </remarks>
public sealed class Rope : IReadOnlyList<char>
{
    /// <summary>
    /// The number of characters a leaf holds when the chunk size is not given: 512 code units, one KiB.
    /// </summary>
    /// <remarks>
    /// This is the leaf's <i>capacity</i>; a rebuild fills a leaf to three quarters of it and leaves the rest
    /// as room to edit into. Large enough that the per-leaf node and buffer overhead is a few percent of the
    /// text, small enough that the <see cref="Array.Copy(Array, int, Array, int, int)"/> an in-leaf edit
    /// performs stays inside a handful of cache lines.
    /// </remarks>
    public const int DefaultChunkSize = 512;

    /// <summary>
    /// The smallest chunk size a rope accepts: eight code units.
    /// </summary>
    /// <remarks>
    /// Below this the tree is nearly all internal nodes and the structure costs more than the text it holds.
    /// The floor exists to make that a rejected argument rather than a mysterious memory profile; tests that
    /// want deep trees over short strings use it deliberately.
    /// </remarks>
    public const int MinChunkSize = 8;

    // A leaf holds Text and a used prefix of it; an internal node holds two children and no text. Length,
    // Leaves and Height describe the whole subtree, which is what makes a descent index-only: the walk
    // compares Left.Length against the position it is looking for and never reads a character until it lands.
    private sealed class Node
    {
        public Node? Left;
        public Node? Right;

        // Non-null exactly on a leaf, and that doubles as the node-kind discriminator so no separate flag or
        // type test is needed. The buffer's own length is the leaf's capacity, which is not necessarily
        // ChunkSize: AppendAndClear can move in leaves a rope with a different chunk size allocated.
        public char[]? Text;

        // Characters in this subtree. On a leaf this is the used prefix of Text.
        public int Length;

        // Leaves in this subtree. Only the root's copy is read — it is the fragmentation signal — but it is
        // maintained everywhere because that is what makes it free to read.
        public int Leaves;

        public int Height;
    }

    private readonly int _chunkSize;

    // The number of characters a leaf is filled to when the rope builds or rebuilds one: three quarters of
    // the capacity, so a freshly built leaf has room for an edit and does not have to split on the first one.
    // A rope built with leaves filled to capacity splits one on *every* insertion, which is the difference
    // between an in-place memmove and two allocations plus a rebalance.
    private readonly int _fill;

    private Node? _root;
    private int _length;
    private int _version;

    /// <summary>
    /// Initializes an empty rope with the default chunk size.
    /// </summary>
    public Rope()
        : this(default(ReadOnlySpan<char>), DefaultChunkSize)
    {
    }

    /// <summary>
    /// Initializes an empty rope whose leaves hold at most <paramref name="chunkSize"/> characters.
    /// </summary>
    /// <param name="chunkSize">The maximum number of characters in a leaf.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="chunkSize"/> is less than <see cref="MinChunkSize"/>.
    /// </exception>
    public Rope(int chunkSize)
        : this(default(ReadOnlySpan<char>), chunkSize)
    {
    }

    /// <summary>
    /// Initializes a rope holding <paramref name="text"/>, with the default chunk size.
    /// </summary>
    /// <param name="text">The initial text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public Rope(string text)
        : this(text, DefaultChunkSize)
    {
    }

    /// <summary>
    /// Initializes a rope holding <paramref name="text"/>, whose leaves hold at most
    /// <paramref name="chunkSize"/> characters.
    /// </summary>
    /// <param name="text">The initial text.</param>
    /// <param name="chunkSize">The maximum number of characters in a leaf.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="chunkSize"/> is less than <see cref="MinChunkSize"/>.
    /// </exception>
    public Rope(string text, int chunkSize)
        : this(Chars(text), chunkSize)
    {
    }

    /// <summary>
    /// Initializes a rope holding <paramref name="text"/>, with the default chunk size.
    /// </summary>
    /// <param name="text">The initial text.</param>
    public Rope(ReadOnlySpan<char> text)
        : this(text, DefaultChunkSize)
    {
    }

    /// <summary>
    /// Initializes a rope holding <paramref name="text"/>, whose leaves hold at most
    /// <paramref name="chunkSize"/> characters.
    /// </summary>
    /// <param name="text">The initial text.</param>
    /// <param name="chunkSize">The maximum number of characters in a leaf.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="chunkSize"/> is less than <see cref="MinChunkSize"/>.
    /// </exception>
    /// <remarks>
    /// The tree is built balanced in one pass, with each leaf filled to three quarters of
    /// <paramref name="chunkSize"/>, so a rope constructed from existing text starts in the shape
    /// <see cref="TrimExcess"/> would put it in — including the room to edit into that the remaining quarter
    /// is there to provide.
    /// </remarks>
    public Rope(ReadOnlySpan<char> text, int chunkSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, MinChunkSize);

        _chunkSize = chunkSize;
        _fill = chunkSize - (chunkSize / 4);
        _root = BuildBalanced(text);
        _length = text.Length;
    }

    // The null check for the string-taking constructors. It has to happen before the span conversion, and a
    // constructor cannot run a statement before its this(...) chain, so it runs here instead.
    private static ReadOnlySpan<char> Chars(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.AsSpan();
    }

    /// <summary>
    /// Gets the number of characters in the rope.
    /// </summary>
    public int Length => _length;

    /// <summary>
    /// Gets the maximum number of characters this rope puts in a leaf it allocates.
    /// </summary>
    /// <remarks>
    /// Leaves moved in by <see cref="AppendAndClear(Rope)"/> keep the size the rope that allocated them chose,
    /// until a <see cref="TrimExcess"/> or an automatic rebuild normalizes them.
    /// </remarks>
    public int ChunkSize => _chunkSize;

    /// <summary>
    /// Gets the number of leaves in the tree — the fragmentation signal.
    /// </summary>
    /// <remarks>
    /// A compact rope has <c>ceil(Length / (ChunkSize * 3 / 4))</c> leaves — the three quarters being the fill
    /// a rebuild targets, so that a freshly built leaf has room for an edit. Edits split leaves and push this
    /// above that figure; the rope rebuilds itself once it passes twice it, and <see cref="TrimExcess"/>
    /// forces the rebuild early.
    /// </remarks>
    public int LeafCount => _root?.Leaves ?? 0;

    /// <summary>
    /// Gets the height of the tree: the number of nodes on the longest root-to-leaf path, and so the number of
    /// comparisons an index lookup performs.
    /// </summary>
    /// <remarks>
    /// Zero for an empty rope, one for a rope that fits in a single leaf. The tree is AVL-balanced, so this
    /// stays within about 1.44 log2(<see cref="LeafCount"/>) however the edits arrive.
    /// </remarks>
    public int Depth => _root?.Height ?? 0;

    /// <inheritdoc />
    int IReadOnlyCollection<char>.Count => _length;

    /// <summary>
    /// Gets or sets the character at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based index of the character.</param>
    /// <returns>The character at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or is not less than <see cref="Length"/>.
    /// </exception>
    /// <remarks>
    /// Both halves are a descent of <see cref="Depth"/> index comparisons, against the chunk-list walk
    /// <see cref="System.Text.StringBuilder"/>'s indexer performs. The setter replaces one code unit in place
    /// and moves nothing, so it does not invalidate an in-flight enumerator.
    /// </remarks>
    public char this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _length);

            Node leaf = FindLeaf(_root!, index, out int leafStart);
            return leaf.Text![index - leafStart];
        }

        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _length);

            Node leaf = FindLeaf(_root!, index, out int leafStart);
            leaf.Text![index - leafStart] = value;
        }
    }

    /// <summary>
    /// Appends a character to the end of the rope.
    /// </summary>
    /// <param name="value">The character to append.</param>
    public void Append(char value) => Insert(_length, value);

    /// <summary>
    /// Appends text to the end of the rope.
    /// </summary>
    /// <param name="text">The text to append.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public void Append(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Insert(_length, text.AsSpan());
    }

    /// <summary>
    /// Appends text to the end of the rope.
    /// </summary>
    /// <param name="text">The text to append.</param>
    public void Append(ReadOnlySpan<char> text) => Insert(_length, text);

    /// <summary>
    /// Inserts a character at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based index to insert at; <see cref="Length"/> appends.</param>
    /// <param name="value">The character to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or greater than <see cref="Length"/>.
    /// </exception>
    public void Insert(int index, char value)
    {
        ReadOnlySpan<char> one = stackalloc char[1] { value };
        Insert(index, one);
    }

    /// <summary>
    /// Inserts text at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based index to insert at; <see cref="Length"/> appends.</param>
    /// <param name="text">The text to insert.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or greater than <see cref="Length"/>.
    /// </exception>
    public void Insert(int index, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Insert(index, text.AsSpan());
    }

    /// <summary>
    /// Inserts text at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based index to insert at; <see cref="Length"/> appends.</param>
    /// <param name="text">The text to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or greater than <see cref="Length"/>.
    /// </exception>
    /// <remarks>
    /// <c>O(log n)</c>, and free of allocation entirely when the leaf the insertion lands in has room for
    /// <paramref name="text"/> — which is the ordinary case for the short insertions an editor produces.
    /// Inserting nothing is a no-op that does not invalidate enumerators.
    /// </remarks>
    public void Insert(int index, ReadOnlySpan<char> text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _length);

        if (text.Length == 0)
            return;

        ThrowIfTooLong((long)_length + text.Length);

        _root = _root is null ? BuildBalanced(text) : InsertCore(_root, index, text);
        _length += text.Length;
        _version++;
        RebuildIfFragmented();
    }

    /// <summary>
    /// Removes <paramref name="count"/> characters starting at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based index of the first character to remove.</param>
    /// <param name="count">The number of characters to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> or <paramref name="count"/> is negative, or the range they describe runs past
    /// <see cref="Length"/>.
    /// </exception>
    /// <remarks>
    /// <c>O(log n)</c> whatever <paramref name="count"/> is: whole leaves inside the range are unlinked rather
    /// than copied, and at most the two leaves the range's ends fall inside are compacted. Removing nothing is
    /// a no-op that does not invalidate enumerators.
    /// </remarks>
    public void Remove(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _length);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, _length - index);

        if (count == 0)
            return;

        _root = RemoveCore(_root!, index, count);
        _length -= count;
        _version++;
        RebuildIfFragmented();
    }

    /// <summary>
    /// Removes every character from the rope.
    /// </summary>
    /// <remarks>
    /// Releases the whole tree — there is no retained capacity to reuse, because a rope's storage is its
    /// structure. Clearing an already empty rope is a no-op that does not invalidate enumerators.
    /// </remarks>
    public void Clear()
    {
        if (_root is null)
            return;

        _root = null;
        _length = 0;
        _version++;
    }

    /// <summary>
    /// Rebuilds the tree balanced, with each leaf filled to three quarters of <see cref="ChunkSize"/>, undoing
    /// the fragmentation that editing causes.
    /// </summary>
    /// <remarks>
    /// <c>O(n)</c>, and the rope does this for itself once <see cref="LeafCount"/> passes twice what the
    /// length needs; call it directly after a burst of edits that will not be followed by more, or before
    /// measuring memory. On an empty rope it is a no-op that does not invalidate enumerators.
    /// </remarks>
    public void TrimExcess()
    {
        if (_root is null)
            return;

        Rebuild();
        _version++;
    }

    /// <summary>
    /// Splits the rope at <paramref name="index"/>, truncating this rope to the first
    /// <paramref name="index"/> characters and returning the rest as a new rope.
    /// </summary>
    /// <param name="index">The zero-based index to cut at.</param>
    /// <returns>
    /// A new rope, with this rope's <see cref="ChunkSize"/>, holding the characters from
    /// <paramref name="index"/> onwards.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or greater than <see cref="Length"/>.
    /// </exception>
    /// <remarks>
    /// <c>O(log n)</c>: the tree is cut along one root-to-leaf path and the two sides rejoined, so no
    /// character moves except inside the single leaf the cut falls within. Splitting at <see cref="Length"/>
    /// returns an empty rope and is a no-op that does not invalidate enumerators.
    /// <see cref="AppendAndClear(Rope)"/> is the inverse.
    /// </remarks>
    public Rope Split(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _length);

        var tail = new Rope(_chunkSize);
        if (index == _length)
            return tail;

        SplitCore(_root!, index, out Node? left, out Node? right);
        _root = left;
        tail._root = right;
        tail._length = _length - index;
        _length = index;
        _version++;
        return tail;
    }

    /// <summary>
    /// Moves every character of <paramref name="source"/> onto the end of this rope in <c>O(log n)</c>,
    /// leaving <paramref name="source"/> empty.
    /// </summary>
    /// <param name="source">The rope to move from. It is emptied.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is this rope.</exception>
    /// <remarks>
    /// <para>
    /// This is a <b>move</b>, and the emptying is the point rather than a side effect: joining two ropes
    /// relinks <paramref name="source"/>'s nodes into this tree instead of copying its characters, so the two
    /// ropes would otherwise share buffers that this one goes on to mutate. Emptying the source is what makes
    /// the <c>O(log n)</c> honest. For a join that leaves <paramref name="source"/> intact, pay the copy:
    /// <c>rope.Append(source.ToString())</c>.
    /// </para>
    /// <para>
    /// <b>This is the one mutation that does not run the defragmenting rebuild</b>, which is what makes the
    /// <c>O(log n)</c> unconditional rather than amortized. A join adopts <paramref name="source"/>'s leaves
    /// in whatever shape that rope left them — joining a rope with a much smaller <see cref="ChunkSize"/>
    /// brings in many more leaves than this rope's length warrants — and rebuilding for that here would turn
    /// a node relink into an <c>O(n)</c> copy of the whole document. The fragmentation it can leave behind is
    /// resolved by the next <see cref="Insert(int, ReadOnlySpan{char})"/> or <see cref="Remove"/> that trips
    /// the gate, or by <see cref="TrimExcess"/>. <see cref="Split"/> is silent for the same reason, which
    /// keeps the two halves of the pair symmetric.
    /// </para>
    /// <para>
    /// Moving from an empty rope is a no-op that does not invalidate enumerators on either side.
    /// </para>
    /// </remarks>
    public void AppendAndClear(Rope source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (ReferenceEquals(source, this))
            throw new ArgumentException("A rope cannot be appended to itself.", nameof(source));

        if (source._length == 0)
            return;

        ThrowIfTooLong((long)_length + source._length);

        _root = Join(_root, source._root);
        _length += source._length;
        source._root = null;
        source._length = 0;
        source._version++;
        _version++;
    }

    /// <summary>
    /// Returns the zero-based index of the first occurrence of <paramref name="value"/>, or <c>-1</c>.
    /// </summary>
    /// <param name="value">The character to find.</param>
    /// <returns>The index of the first occurrence, or <c>-1</c> if there is none.</returns>
    public int IndexOf(char value) => IndexOf(value, 0);

    /// <summary>
    /// Returns the zero-based index of the first occurrence of <paramref name="value"/> at or after
    /// <paramref name="startIndex"/>, or <c>-1</c>.
    /// </summary>
    /// <param name="value">The character to find.</param>
    /// <param name="startIndex">The zero-based index to start searching at.</param>
    /// <returns>The index of the first occurrence, or <c>-1</c> if there is none.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="startIndex"/> is negative or greater than <see cref="Length"/>.
    /// </exception>
    /// <remarks>
    /// The search runs a vectorized <see cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, T)"/> over each
    /// leaf rather than reading characters one at a time. Finding a multi-character <i>pattern</i> is not this
    /// type's job: build the text into a <see cref="SuffixArray"/>, or run a set of patterns through
    /// <see cref="AhoCorasick"/>.
    /// </remarks>
    public int IndexOf(char value, int startIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startIndex, _length);

        int position = startIndex;
        while (position < _length)
        {
            Node leaf = FindLeaf(_root!, position, out int leafStart);
            int offset = position - leafStart;
            int found = leaf.Text!.AsSpan(offset, leaf.Length - offset).IndexOf(value);
            if (found >= 0)
                return position + found;

            position = leafStart + leaf.Length;
        }

        return -1;
    }

    /// <summary>
    /// Copies the whole rope into <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">The span to copy into.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is shorter than <see cref="Length"/>.
    /// </exception>
    public void CopyTo(Span<char> destination) => CopyTo(0, destination, _length);

    /// <summary>
    /// Copies <paramref name="count"/> characters starting at <paramref name="index"/> into
    /// <paramref name="destination"/>.
    /// </summary>
    /// <param name="index">The zero-based index of the first character to copy.</param>
    /// <param name="destination">The span to copy into.</param>
    /// <param name="count">The number of characters to copy.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> or <paramref name="count"/> is negative, or the range they describe runs past
    /// <see cref="Length"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is shorter than <paramref name="count"/>.
    /// </exception>
    public void CopyTo(int index, Span<char> destination, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _length);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, _length - index);

        if (destination.Length < count)
            throw new ArgumentException("The destination is too short.", nameof(destination));

        if (count > 0)
            CopyRange(_root!, index, destination, count);
    }

    /// <summary>
    /// Returns the whole rope as a <see cref="string"/>.
    /// </summary>
    /// <returns>The rope's characters.</returns>
    public override string ToString() => ToString(0, _length);

    /// <summary>
    /// Returns <paramref name="count"/> characters starting at <paramref name="index"/> as a
    /// <see cref="string"/>.
    /// </summary>
    /// <param name="index">The zero-based index of the first character.</param>
    /// <param name="count">The number of characters.</param>
    /// <returns>The requested substring.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> or <paramref name="count"/> is negative, or the range they describe runs past
    /// <see cref="Length"/>.
    /// </exception>
    public string ToString(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _length);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, _length - index);

        if (count == 0)
            return string.Empty;

        return string.Create(count, (Rope: this, Index: index), static (span, state) =>
            CopyRange(state.Rope._root!, state.Index, span, span.Length));
    }

    /// <summary>
    /// Returns an enumerator over the rope's underlying character runs, for a read path that copies nothing.
    /// </summary>
    /// <returns>A chunk enumerator, usable directly in a <c>foreach</c>.</returns>
    /// <remarks>
    /// Mirrors <see cref="System.Text.StringBuilder.GetChunks"/>, but yields <see cref="ReadOnlySpan{T}"/>
    /// rather than <see cref="ReadOnlyMemory{T}"/>, so writing a rope out is a sequence of span writes with no
    /// intermediate <see cref="string"/>. The chunk boundaries are an implementation detail and move as the
    /// rope is edited; they are not text boundaries of any kind.
    /// </remarks>
    public ChunkEnumerator GetChunks() => new(this);

    /// <summary>
    /// Returns a struct enumerator over the rope's characters, in order.
    /// </summary>
    /// <returns>An enumerator over the characters.</returns>
    public Enumerator GetEnumerator() => new(this);

    /// <inheritdoc />
    IEnumerator<char> IEnumerable<char>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- tree internals -------------------------------------------------------------------------------

    // The descent every positional operation shares: walk down comparing the position against the left
    // subtree's character count, and report where the leaf that owns the position starts.
    private static Node FindLeaf(Node root, int position, out int leafStart)
    {
        int start = 0;
        Node node = root;
        while (node.Text is null)
        {
            int leftLength = node.Left!.Length;
            if (position - start < leftLength)
            {
                node = node.Left;
            }
            else
            {
                start += leftLength;
                node = node.Right!;
            }
        }

        leafStart = start;
        return node;
    }

    // A leaf's buffer is its capacity, and is normally the chunk size whatever the leaf currently holds. The
    // wider case is reachable only through SplitCore, cutting a leaf that AppendAndClear moved in from a rope
    // with a larger chunk size than this one's.
    private Node NewLeaf(ReadOnlySpan<char> text)
    {
        var buffer = new char[text.Length > _chunkSize ? text.Length : _chunkSize];
        text.CopyTo(buffer);
        return new Node { Text = buffer, Length = text.Length, Leaves = 1, Height = 1 };
    }

    // Builds a balanced tree whose leaves are filled to _fill rather than to capacity, so each one starts with
    // room for an edit. Null for empty text, which is what lets the callers below hand it the empty side of a
    // split without testing for it first.
    private Node? BuildBalanced(ReadOnlySpan<char> text)
    {
        if (text.Length == 0)
            return null;

        if (text.Length <= _fill)
            return NewLeaf(text);

        int leaves = (text.Length + _fill - 1) / _fill;
        int split = (leaves / 2) * _fill;
        var node = new Node { Left = BuildBalanced(text[..split]), Right = BuildBalanced(text[split..]) };
        Update(node);
        return node;
    }

    private static void Update(Node node)
    {
        Node left = node.Left!;
        Node right = node.Right!;
        node.Length = left.Length + right.Length;
        node.Leaves = left.Leaves + right.Leaves;
        node.Height = 1 + (left.Height > right.Height ? left.Height : right.Height);
    }

    private static Node RotateRight(Node node)
    {
        Node pivot = node.Left!;
        node.Left = pivot.Right;
        Update(node);
        pivot.Right = node;
        Update(pivot);
        return pivot;
    }

    private static Node RotateLeft(Node node)
    {
        Node pivot = node.Right!;
        node.Right = pivot.Left;
        Update(node);
        pivot.Left = node;
        Update(pivot);
        return pivot;
    }

    // Restores the AVL invariant at one node whose children are each valid and whose heights differ by at most
    // two. Every caller either satisfies that or goes through Join instead.
    private static Node Balance(Node node)
    {
        Update(node);
        int slope = node.Left!.Height - node.Right!.Height;

        if (slope > 1)
        {
            if (node.Left.Left!.Height < node.Left.Right!.Height)
                node.Left = RotateLeft(node.Left);

            return RotateRight(node);
        }

        if (slope < -1)
        {
            if (node.Right.Right!.Height < node.Right.Left!.Height)
                node.Right = RotateRight(node.Right);

            return RotateLeft(node);
        }

        return node;
    }

    // Concatenates two valid AVL subtrees of any heights. Descends the taller one's facing spine to the point
    // where the heights meet, hangs the shorter one there, and rebalances back up.
    private static Node? Join(Node? left, Node? right)
    {
        if (left is null)
            return right;

        if (right is null)
            return left;

        if (left.Height > right.Height + 1)
        {
            left.Right = Join(left.Right, right);
            return Balance(left);
        }

        if (right.Height > left.Height + 1)
        {
            right.Left = Join(left, right.Left);
            return Balance(right);
        }

        var node = new Node { Left = left, Right = right };
        Update(node);
        return node;
    }

    // Cuts a subtree at a character index. The internal nodes on the cut path are discarded and the pieces
    // rejoined, which is what keeps both sides valid AVL trees.
    private void SplitCore(Node node, int index, out Node? left, out Node? right)
    {
        if (node.Text is not null)
        {
            if (index == 0)
            {
                left = null;
                right = node;
            }
            else if (index == node.Length)
            {
                left = node;
                right = null;
            }
            else
            {
                right = NewLeaf(node.Text.AsSpan(index, node.Length - index));
                node.Length = index;
                left = node;
            }

            return;
        }

        int leftLength = node.Left!.Length;
        if (index <= leftLength)
        {
            SplitCore(node.Left, index, out left, out Node? spill);
            right = Join(spill, node.Right);
        }
        else
        {
            SplitCore(node.Right!, index - leftLength, out Node? spill, out right);
            left = Join(node.Left, spill);
        }
    }

    private Node InsertCore(Node node, int index, ReadOnlySpan<char> text)
    {
        if (node.Text is not null)
        {
            char[] buffer = node.Text;
            if (node.Length + text.Length <= buffer.Length)
            {
                // The allocation-free path, and the common one: the leaf has room, so the insert is a shift of
                // the leaf's tail and a copy, with no node touched above.
                Array.Copy(buffer, index, buffer, index + text.Length, node.Length - index);
                text.CopyTo(buffer.AsSpan(index));
                node.Length += text.Length;
                return node;
            }

            // The leaf overflows. Reuse it for whichever side of the cut is the whole of it, so an append —
            // where the insertion point is the leaf's end — never copies the leaf it lands on.
            Node? head;
            Node? tail;
            if (index == node.Length)
            {
                head = node;
                tail = null;
            }
            else if (index == 0)
            {
                head = null;
                tail = node;
            }
            else
            {
                head = BuildBalanced(buffer.AsSpan(0, index));
                tail = BuildBalanced(buffer.AsSpan(index, node.Length - index));
            }

            return Join(Join(head, BuildBalanced(text)), tail)!;
        }

        int leftLength = node.Left!.Length;
        if (index <= leftLength)
            node.Left = InsertCore(node.Left, index, text);
        else
            node.Right = InsertCore(node.Right!, index - leftLength, text);

        return Balance(node);
    }

    // Returns the subtree with the range gone, or null if nothing is left of it. A range that straddles the
    // two children is removed from each, which can shorten either by any amount, so the two sides are put back
    // together with Join unless their heights are still close enough to reuse this node.
    private static Node? RemoveCore(Node node, int index, int count)
    {
        if (node.Text is not null)
        {
            int end = index + count;
            if (end < node.Length)
                Array.Copy(node.Text, end, node.Text, index, node.Length - end);

            node.Length -= count;
            return node.Length == 0 ? null : node;
        }

        int leftLength = node.Left!.Length;
        Node? left;
        Node? right;
        if (index >= leftLength)
        {
            left = node.Left;
            right = RemoveCore(node.Right!, index - leftLength, count);
        }
        else if (index + count <= leftLength)
        {
            left = RemoveCore(node.Left, index, count);
            right = node.Right;
        }
        else
        {
            int fromLeft = leftLength - index;
            left = RemoveCore(node.Left, index, fromLeft);
            right = RemoveCore(node.Right!, 0, count - fromLeft);
        }

        if (left is null)
            return right;

        if (right is null)
            return left;

        int slope = left.Height - right.Height;
        if (slope is >= -1 and <= 1)
        {
            node.Left = left;
            node.Right = right;
            Update(node);
            return node;
        }

        return Join(left, right);
    }

    private static void CopyRange(Node node, int index, Span<char> destination, int count)
    {
        while (true)
        {
            if (node.Text is not null)
            {
                node.Text.AsSpan(index, count).CopyTo(destination);
                return;
            }

            int leftLength = node.Left!.Length;
            if (index >= leftLength)
            {
                index -= leftLength;
                node = node.Right!;
                continue;
            }

            int fromLeft = leftLength - index;
            if (fromLeft >= count)
            {
                node = node.Left;
                continue;
            }

            CopyRange(node.Left, index, destination[..fromLeft], fromLeft);
            index = 0;
            count -= fromLeft;
            destination = destination[fromLeft..];
            node = node.Right!;
        }
    }

    // The fragmentation gate. Editing splits leaves and never merges them, so the leaf count drifts above what
    // the character count needs; once it is twice that, the tree is rebuilt with leaves filled to _fill again.
    // The constant slack keeps a rope of a few leaves from rebuilding on every other edit.
    private void RebuildIfFragmented()
    {
        int ideal = (_length + _fill - 1) / _fill;
        if (_root is not null && _root.Leaves > (2 * ideal) + 8)
            Rebuild();
    }

    private void Rebuild()
    {
        var buffer = new char[_length];
        CopyRange(_root!, 0, buffer, _length);
        _root = BuildBalanced(buffer);
    }

    [ExcludeFromCodeCoverage(Justification = "Needs a rope within characters of int.MaxValue — four GiB of " +
        "text in the leaves alone — so no test can allocate its way to the guard.")]
    private static void ThrowIfTooLong(long length)
    {
        if (length > int.MaxValue)
            throw new OutOfMemoryException("A rope cannot hold more than int.MaxValue characters.");
    }

    /// <summary>
    /// An enumerator over a <see cref="Rope"/>'s underlying character runs, yielding each as a
    /// <see cref="ReadOnlySpan{T}"/> that aliases the rope's own storage rather than a copy of it.
    /// </summary>
    /// <remarks>
    /// Because it is a struct with its own <c>GetEnumerator</c>, a <c>foreach</c> over
    /// <see cref="Rope.GetChunks"/> allocates nothing.
    /// </remarks>
    public struct ChunkEnumerator
    {
        private readonly Rope _rope;
        private readonly int _version;
        private char[] _chunk;
        private int _start;
        private int _count;
        private int _position;

        internal ChunkEnumerator(Rope rope)
        {
            _rope = rope;
            _version = rope._version;
            _chunk = [];
            _start = 0;
            _count = 0;
            _position = 0;
        }

        /// <summary>
        /// Gets the current run. Empty before the first <see cref="MoveNext"/> and after the last.
        /// </summary>
        public readonly ReadOnlySpan<char> Current => _chunk.AsSpan(_start, _count);

        /// <summary>
        /// Returns this enumerator, so it can be used directly in a <c>foreach</c>.
        /// </summary>
        /// <returns>This enumerator.</returns>
        public readonly ChunkEnumerator GetEnumerator() => this;

        /// <summary>
        /// Advances to the next run.
        /// </summary>
        /// <returns><see langword="true"/> if there was one; otherwise <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">The rope was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _rope._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            if (_position >= _rope._length)
            {
                _chunk = [];
                _start = 0;
                _count = 0;
                return false;
            }

            Node leaf = FindLeaf(_rope._root!, _position, out int leafStart);
            _chunk = leaf.Text!;
            _start = _position - leafStart;
            _count = leaf.Length - _start;
            _position = leafStart + leaf.Length;
            return true;
        }
    }

    /// <summary>
    /// A struct enumerator over a <see cref="Rope"/>'s characters, in order. Because it is a struct, iterating
    /// via <c>foreach</c> avoids the allocation a compiler-generated <c>IEnumerator&lt;char&gt;</c> would
    /// incur.
    /// </summary>
    public struct Enumerator : IEnumerator<char>
    {
        private readonly Rope _rope;
        private readonly int _version;
        private char[] _chunk;
        private int _offset;
        private int _chunkEnd;
        private int _position;
        private char _current;

        internal Enumerator(Rope rope)
        {
            _rope = rope;
            _version = rope._version;
            _chunk = [];
            _offset = 0;
            _chunkEnd = 0;
            _position = 0;
            _current = '\0';
        }

        /// <summary>
        /// Gets the character at the current position.
        /// </summary>
        public readonly char Current => _current;

        /// <inheritdoc />
        readonly object IEnumerator.Current => _current;

        /// <summary>
        /// Advances to the next character.
        /// </summary>
        /// <returns><see langword="true"/> if there was one; otherwise <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">The rope was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _rope._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            if (_offset < _chunkEnd)
            {
                _current = _chunk[_offset++];
                return true;
            }

            if (_position >= _rope._length)
            {
                _current = '\0';
                return false;
            }

            Node leaf = FindLeaf(_rope._root!, _position, out int leafStart);
            _chunk = leaf.Text!;
            _offset = _position - leafStart;
            _chunkEnd = leaf.Length;
            _position = leafStart + leaf.Length;
            _current = _chunk[_offset++];
            return true;
        }

        /// <inheritdoc />
        [ExcludeFromCodeCoverage(Justification = "The interface requires it; a struct enumerator holds no " +
            "disposable state.")]
        public readonly void Dispose()
        {
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">The rope was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _rope._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _chunk = [];
            _offset = 0;
            _chunkEnd = 0;
            _position = 0;
            _current = '\0';
        }
    }
}
