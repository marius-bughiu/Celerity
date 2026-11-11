using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Celerity.Collections;

/// <summary>
/// A <b>positional list</b> laid out as a <b>counted B+ tree</b>: inserting or removing at any index is
/// <c>O(log n)</c> plus one bounded in-leaf shift, where <see cref="List{T}"/> shifts every later element and
/// costs <c>O(n)</c>. It is the list for <i>a sequence that keeps changing in the middle</i> — the shape
/// <see cref="List{T}"/> is quadratic to churn and <see cref="LinkedList{T}"/> cannot index.
/// </summary>
/// <typeparam name="T">The type of the elements.</typeparam>
/// <remarks>
/// <para>
/// <b>Positional, not sorted.</b> Despite sharing a layout family with <see cref="BTreeSet{T, TComparer}"/>
/// and <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>, this type keeps elements in the order the
/// caller places them — exactly as <see cref="List{T}"/> does — and never compares them. The tree is keyed
/// by <i>position</i>: every internal node stores where each child's elements start, so reaching index
/// <c>i</c> is a descent comparing positions, never a comparison of elements.
/// </para>
/// <para>
/// <b>Why not the BCL.</b> <see cref="List{T}"/> indexes in <c>O(1)</c> and pays <c>O(n)</c> on every
/// <see cref="List{T}.Insert"/> and <see cref="List{T}.RemoveAt"/> that is not at the end.
/// <see cref="LinkedList{T}"/> edits in <c>O(1)</c> once a node is in hand, but reaching position <c>i</c>
/// is an <c>O(i)</c> walk and it has no indexer at all. <see cref="System.Collections.Immutable.ImmutableList{T}"/>
/// (and its <c>Builder</c>) is the one BCL type that is <c>O(log n)</c> on both halves, but it is an AVL
/// tree with <b>one heap node per element</b>: every read chases a pointer per level of a tree about
/// <c>1.44 log2 n</c> deep, and every element carries a node header, two child pointers, a height and a
/// count. This type keeps up to 128 elements contiguous in each leaf and up to 64 children in each internal
/// node, so the tree is four levels deep at a million elements, against about twenty for the AVL tree, and
/// the elements themselves sit in arrays.
/// </para>
/// <para>
/// <b>Layout.</b> Leaves hold a fixed 128-slot array and a count, and are chained left to right so an
/// in-order walk never touches an internal node. Internal nodes hold up to 64 children and a parallel array
/// of the position each child starts at, which a descent searches with one vector compare rather than a
/// data-dependent scan. An edit descends once, shifts at most one leaf's tail, and shifts the later start
/// positions by one on each level on the way back. A full leaf splits in two; a leaf that falls below a quarter full
/// borrows from or merges with a neighbour, and internal nodes do the same, so every node but the root
/// stays at least a quarter full under <i>any</i> sequence of edits and the depth stays <c>O(log n)</c>.
/// The split point follows the edit: an append keeps the left leaf three quarters full and a prepend keeps
/// the right one so, which is what makes a list built by <see cref="Add"/> about three quarters dense rather
/// than half. The <see cref="BTreeList{T}(IEnumerable{T})"/> constructor builds bottom-up in <c>O(n)</c> to
/// the same three-quarter fill, leaving every leaf room for a quarter of its capacity in inserts before it
/// splits.
/// </para>
/// <para>
/// <b>What this costs you, and it is not nothing.</b> A read through the indexer is a descent where
/// <see cref="List{T}"/> does one array load, so random access is several times slower than a
/// <see cref="List{T}"/>'s (about 12x at 100,000 elements, 4x at a thousand), and a sequence that is only
/// ever appended to and read belongs in one. Enumeration walks the leaf chain and runs about 1.5–1.7x
/// slower than a <see cref="List{T}"/>'s. Memory is in the same range as a <see cref="List{T}"/>'s rather
/// than below it: every leaf carries its full 128-slot array whatever it holds, so even an empty list owns
/// one, and a list at the three-quarter fill spends about a third more than its elements need.
/// </para>
/// <para>
/// <b>Search is linear.</b> <see cref="IndexOf"/>, <see cref="Contains"/> and <see cref="Remove"/> scan
/// leaf by leaf with <see cref="EqualityComparer{T}.Default"/>, exactly as <see cref="List{T}"/>'s do. The
/// tree indexes position, not value.
/// </para>
/// <para>
/// <b>Enumeration.</b> The struct enumerator walks the leaf chain front to back and is invalidated by every
/// structural mutation — <see cref="Add"/>, <see cref="Insert"/>, <see cref="RemoveAt"/>, a successful
/// <see cref="Remove"/>, and a <see cref="Clear"/> that had something to clear. Assignment through the
/// indexer changes an element in place and moves no leaf boundary, so it does <b>not</b> invalidate — the
/// rule <see cref="Deque{T}"/> and <see cref="Rope"/> follow, and the one place this type deliberately
/// differs from <see cref="List{T}"/>, whose indexer setter does. This type is not thread-safe; concurrent
/// callers must synchronize externally.
/// </para>
/// </remarks>
public sealed class BTreeList<T> : IList<T>, IReadOnlyList<T>
{
    // Leaf and branch fan-out. A leaf's in-place shift is at most LeafCapacity elements; a branch's linear
    // count scan is at most BranchCapacity ints, which stay in one or two cache lines' worth of reads.
    private const int LeafCapacity = 128;
    private const int BranchCapacity = 64;

    // The underflow floor. Every node but the root holds at least a quarter of its capacity, so the depth is
    // O(log n) whatever the edit history; splits and the bulk build both leave nodes well above it.
    private const int MinLeaf = LeafCapacity / 4;
    private const int MinBranch = BranchCapacity / 4;

    // The bulk build's fill: three quarters, leaving room for inserts before the first split.
    private const int BulkLeafFill = LeafCapacity * 3 / 4;
    private const int BulkBranchFill = BranchCapacity * 3 / 4;

    private Node _root;
    private Leaf _first;  // the leftmost leaf; merges always fold the right node into the left, so it is stable
    private int _height;  // levels above the leaves: 0 while the root is a leaf
    private int _count;

    // Incremented on every structural mutation so active enumerators can detect it. An indexer set is an
    // in-place element change, not structural, and a Clear() of an empty list or a Remove(T) that finds
    // nothing returns before touching it.
    private int _version;

    /// <summary>Initializes a new, empty list.</summary>
    public BTreeList()
    {
        var leaf = new Leaf();
        _root = leaf;
        _first = leaf;
    }

    /// <summary>
    /// Initializes a new list containing the elements of <paramref name="collection"/> in enumeration order,
    /// built bottom-up in <c>O(n)</c> with every node three quarters full.
    /// </summary>
    /// <param name="collection">The elements to copy into the list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is <c>null</c>.</exception>
    public BTreeList(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var leaf = new Leaf();
        _first = leaf;
        var level = new List<Node> { leaf };
        int count = 0;
        foreach (T item in collection)
        {
            if (leaf.Count == BulkLeafFill)
            {
                var next = new Leaf();
                leaf.Next = next;
                leaf = next;
                level.Add(next);
            }
            leaf.Items[leaf.Count++] = item;
            count++;
        }

        int height = 0;
        while (level.Count > 1)
        {
            BalanceTail(level, height);
            level = BuildParents(level, height);
            height++;
        }

        _root = level[0];
        _height = height;
        _count = count;
    }

    /// <summary>Gets the number of elements in the list.</summary>
    public int Count => _count;

    /// <summary>
    /// Gets the number of node levels from the root to a leaf: <c>1</c> while the root is a single leaf, and
    /// <c>O(log n)</c> under any sequence of edits. A leaf holds up to 128 elements, but a list can be deeper
    /// at that size: the bulk constructor fills leaves to 96, and two leaves merge back into one only once one
    /// drops below 32. Public so the cost of a descent is observable rather than folklore.
    /// </summary>
    public int Depth => _height + 1;

    bool ICollection<T>.IsReadOnly => false;

    /// <summary>
    /// Gets or sets the element at <paramref name="index"/> in <c>O(log n)</c>. Setting an element replaces
    /// it in place and does not invalidate enumerators.
    /// </summary>
    /// <param name="index">The zero-based position.</param>
    /// <returns>The element at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or not less than <see cref="Count"/>.
    /// </exception>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                ThrowIndexOutOfRange(index);
            Leaf leaf = FindLeaf(ref index);
            return leaf.Items[index];
        }
        set
        {
            if ((uint)index >= (uint)_count)
                ThrowIndexOutOfRange(index);
            Leaf leaf = FindLeaf(ref index);
            leaf.Items[index] = value;
        }
    }

    /// <summary>Appends <paramref name="item"/> to the end of the list in <c>O(log n)</c>.</summary>
    /// <param name="item">The element to append.</param>
    public void Add(T item) => InsertCore(_count, item);

    /// <summary>
    /// Inserts <paramref name="item"/> at <paramref name="index"/>, shifting the element there and every later
    /// one up by one position, in <c>O(log n)</c> plus a shift of at most one 128-element leaf.
    /// </summary>
    /// <param name="index">The position to insert at; <see cref="Count"/> appends.</param>
    /// <param name="item">The element to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or greater than <see cref="Count"/>.
    /// </exception>
    public void Insert(int index, T item)
    {
        if ((uint)index > (uint)_count)
            ThrowIndexOutOfRange(index);
        InsertCore(index, item);
    }

    /// <summary>
    /// Removes the element at <paramref name="index"/>, shifting every later one down by one position, in
    /// <c>O(log n)</c> plus a shift of at most one 128-element leaf.
    /// </summary>
    /// <param name="index">The position of the element to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or not less than <see cref="Count"/>.
    /// </exception>
    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_count)
            ThrowIndexOutOfRange(index);

        RemoveFrom(_root, _height, index);
        _count--;
        _version++;

        // A merge one level down can leave a branch root with a single child; the child becomes the root.
        // Merges remove one child per level at a time, so the root can only ever lose one level per removal.
        if (_height > 0 && _root.Count == 1)
        {
            _root = Unsafe.As<Branch>(_root).Children[0];
            _height--;
        }
    }

    /// <summary>
    /// Removes the first occurrence of <paramref name="item"/>, compared with
    /// <see cref="EqualityComparer{T}.Default"/>. The search is a linear <c>O(n)</c> scan, as on
    /// <see cref="List{T}"/>; the removal itself is <c>O(log n)</c>.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns><c>true</c> if an element was removed; <c>false</c> if none was found.</returns>
    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index < 0)
            return false;
        RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Returns the position of the first occurrence of <paramref name="item"/>, compared with
    /// <see cref="EqualityComparer{T}.Default"/>, scanning front to back in <c>O(n)</c>.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns>The zero-based position of the first match, or <c>-1</c> if there is none.</returns>
    public int IndexOf(T item)
    {
        int offset = 0;
        for (Leaf? leaf = _first; leaf is not null; leaf = leaf.Next)
        {
            int i = Array.IndexOf(leaf.Items, item, 0, leaf.Count);
            if (i >= 0)
                return offset + i;
            offset += leaf.Count;
        }
        return -1;
    }

    /// <summary>
    /// Determines whether the list contains <paramref name="item"/>, compared with
    /// <see cref="EqualityComparer{T}.Default"/>. This is a linear <c>O(n)</c> scan.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns><c>true</c> if the element is found; otherwise <c>false</c>.</returns>
    public bool Contains(T item) => IndexOf(item) >= 0;

    /// <summary>
    /// Removes every element, releasing the whole tree. Clearing an already-empty list is a true no-op and
    /// leaves active enumerators valid.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        var leaf = new Leaf();
        _root = leaf;
        _first = leaf;
        _height = 0;
        _count = 0;
        _version++;
    }

    /// <summary>Copies the elements, front to back, into a new array.</summary>
    /// <returns>A new array of length <see cref="Count"/> holding the elements in order.</returns>
    public T[] ToArray()
    {
        if (_count == 0)
            return Array.Empty<T>();

        var array = new T[_count];
        CopyToCore(array, 0);
        return array;
    }

    /// <summary>
    /// Copies the elements, front to back, into <paramref name="array"/> starting at
    /// <paramref name="arrayIndex"/>.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The destination does not have enough room from <paramref name="arrayIndex"/> onward.
    /// </exception>
    public void CopyTo(T[] array, int arrayIndex)
    {
        CopyToGuard.Validate(array, arrayIndex, _count, CopyToGuard.ElementsMessage);
        CopyToCore(array, arrayIndex);
    }

    /// <summary>
    /// Returns an allocation-free struct enumerator that yields the elements front to back. A structural
    /// modification during enumeration makes <see cref="Enumerator.MoveNext"/> throw
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this list.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- descent ------------------------------------------------------------------------------------

    // Descends to the leaf holding position `index` and rewrites `index` to the offset inside it.
    // Requires 0 <= index < _count.
    private Leaf FindLeaf(ref int index)
    {
        Node node = _root;
        for (int h = _height; h > 0; h--)
        {
            var branch = Unsafe.As<Branch>(node);
            int i = ChildContaining(branch, index);
            index -= branch.Offsets[i];
            node = branch.Children[i];
        }
        return Unsafe.As<Leaf>(node);
    }

    // The child of `branch` holding position `index`, which must be below the branch's element count. It is
    // the number of child start offsets at or below `index`, less the first, which is always 0. The count is
    // a branch-free vector compare: the offsets past the last child are padded with int.MaxValue so they never
    // match, which lets the loop read whole vectors. A data-dependent scan here mispredicts on every level of
    // every random read, and that, not the loads, is what a descent would otherwise spend its time on.
    private static int ChildContaining(Branch branch, int index)
    {
        ref int offsets = ref MemoryMarshal.GetArrayDataReference(branch.Offsets);
        Vector128<int> key = Vector128.Create(index);
        Vector128<int> matches = Vector128<int>.Zero;
        int count = branch.Count;
        for (int j = 0; j < count; j += Vector128<int>.Count)
            matches += Vector128.LessThanOrEqual(Vector128.LoadUnsafe(ref offsets, (nuint)j), key);

        // A matching lane is all ones, which is -1.
        return -Vector128.Sum(matches) - 1;
    }

    private void CopyToCore(T[] array, int arrayIndex)
    {
        for (Leaf? leaf = _first; leaf is not null; leaf = leaf.Next)
        {
            Array.Copy(leaf.Items, 0, array, arrayIndex, leaf.Count);
            arrayIndex += leaf.Count;
        }
    }

    // ---- insertion ----------------------------------------------------------------------------------

    private void InsertCore(int index, T item)
    {
        Node? split = InsertInto(_root, _height, index, item);
        if (split is not null)
        {
            // The root split: grow one level, with the old root and its new sibling as the only children.
            var root = new Branch();
            root.Children[0] = _root;
            root.Children[1] = split;
            root.Count = 2;
            Recount(root, _height);
            _root = root;
            _height++;
        }
        _count++;
        _version++;
    }

    // Inserts into the subtree rooted at `node` (`height` levels above the leaves). Returns the new right
    // sibling when the node had to split, or null when it absorbed the element.
    private static Node? InsertInto(Node node, int height, int index, T item)
    {
        if (height == 0)
            return InsertIntoLeaf(Unsafe.As<Leaf>(node), index, item);

        var branch = Unsafe.As<Branch>(node);
        int[] offsets = branch.Offsets;
        // The child holding the element *before* the insert position: a position on a child boundary goes to
        // the end of the left child, and an append (index == subtree size) lands in the last child — a
        // position past the end, which ChildContaining cannot be asked about directly.
        int i = index == 0 ? 0 : ChildContaining(branch, index - 1);

        Node? split = InsertInto(branch.Children[i], height - 1, index - offsets[i], item);
        if (split is null)
        {
            ShiftOffsets(offsets, i + 1, branch.Count, 1);
            return null;
        }

        return InsertChild(branch, i + 1, split, height - 1);
    }

    private static Leaf? InsertIntoLeaf(Leaf leaf, int index, T item)
    {
        if (leaf.Count < LeafCapacity)
        {
            ShiftInsert(leaf.Items, leaf.Count, index, item);
            leaf.Count++;
            return null;
        }

        int keep = SplitPoint(index, LeafCapacity, MinLeaf);
        var right = new Leaf();
        right.Count = SplitInsert(leaf.Items, right.Items, index, item, keep);
        leaf.Count = keep;
        right.Next = leaf.Next;
        leaf.Next = right;
        return right;
    }

    // Inserts `child` (the right half of a child that just split) at `position`. The offsets are rebuilt
    // rather than shifted: a split happens at most once per quarter-leaf of inserts, and a rebuild is one
    // pass over at most BranchCapacity children whose own counts are already right.
    private static Branch? InsertChild(Branch branch, int position, Node child, int childHeight)
    {
        if (branch.Count < BranchCapacity)
        {
            ShiftInsert(branch.Children, branch.Count, position, child);
            branch.Count++;
            Recount(branch, childHeight);
            return null;
        }

        int keep = SplitPoint(position, BranchCapacity, MinBranch);
        var right = new Branch();
        right.Count = SplitInsert(branch.Children, right.Children, position, child, keep);
        branch.Count = keep;
        Recount(branch, childHeight);
        Recount(right, childHeight);
        return right;
    }

    // How many of the capacity + 1 entries a full node keeps on the left when an insert at `index` splits
    // it. An append past the end leaves the right node at the floor and the left three quarters full; a
    // prepend mirrors that; anything else splits evenly. Every outcome leaves both halves at or above `min`.
    private static int SplitPoint(int index, int capacity, int min)
    {
        if (index == capacity)
            return capacity + 1 - min;
        if (index == 0)
            return min;
        return (capacity + 1) / 2;
    }

    // Inserts `value` at `index` into the first `count` slots of `array`, which has room for one more.
    private static void ShiftInsert<TItem>(TItem[] array, int count, int index, TItem value)
    {
        Array.Copy(array, index, array, index + 1, count - index);
        array[index] = value;
    }

    // Inserts `value` at `index` into the full `source`, keeping the first `keep` of the resulting
    // capacity + 1 entries in `source` and moving the rest to the front of the empty `destination`.
    // Returns how many entries `destination` received.
    private static int SplitInsert<TItem>(TItem[] source, TItem[] destination, int index, TItem value, int keep)
    {
        int capacity = source.Length;
        if (index < keep)
        {
            // The new entry lands on the left: hand the right node the old tail from keep - 1 onward, then
            // open the gap inside what is left.
            int moved = capacity - keep + 1;
            Array.Copy(source, keep - 1, destination, 0, moved);
            Array.Copy(source, index, source, index + 1, keep - 1 - index);
            source[index] = value;
            Array.Clear(source, keep, capacity - keep);
            return moved;
        }

        int local = index - keep;
        Array.Copy(source, keep, destination, 0, local);
        destination[local] = value;
        Array.Copy(source, index, destination, local + 1, capacity - index);
        Array.Clear(source, keep, capacity - keep);
        return capacity - keep + 1;
    }

    // Adds `delta` to offsets[from..last], inclusive: every child after the edited one starts one position
    // later or earlier, and so does the subtree's end. Vectorized, because on an edit that splits nothing this
    // loop is most of the work left on each level. It stops short of the int.MaxValue padding, which an
    // increment would wrap.
    private static void ShiftOffsets(int[] offsets, int from, int last, int delta)
    {
        ref int start = ref MemoryMarshal.GetArrayDataReference(offsets);
        Vector128<int> step = Vector128.Create(delta);
        int j = from;
        for (; j + Vector128<int>.Count <= last + 1; j += Vector128<int>.Count)
            (Vector128.LoadUnsafe(ref start, (nuint)j) + step).StoreUnsafe(ref start, (nuint)j);
        for (; j <= last; j++)
            offsets[j] += delta;
    }

    // ---- removal ------------------------------------------------------------------------------------

    private static void RemoveFrom(Node node, int height, int index)
    {
        if (height == 0)
        {
            var leaf = Unsafe.As<Leaf>(node);
            T[] items = leaf.Items;
            int count = --leaf.Count;
            Array.Copy(items, index + 1, items, index, count - index);
            items[count] = default!;
            return;
        }

        var branch = Unsafe.As<Branch>(node);
        int i = ChildContaining(branch, index);
        int[] offsets = branch.Offsets;
        ShiftOffsets(offsets, i + 1, branch.Count, -1);

        Node child = branch.Children[i];
        RemoveFrom(child, height - 1, index - offsets[i]);
        if (child.Count < (height == 1 ? MinLeaf : MinBranch))
            Rebalance(branch, i, height - 1);
    }

    // Restores the floor on child `i` of `branch` by pairing it with a neighbour: the two merge into the
    // left one when they fit in one node, and otherwise split their entries evenly. A non-root branch has at
    // least MinBranch children and a branch root at least two, so a neighbour always exists.
    private static void Rebalance(Branch branch, int i, int childHeight)
    {
        int j = i == 0 ? 0 : i - 1;
        Node left = branch.Children[j];
        Node right = branch.Children[j + 1];
        int total = left.Count + right.Count;
        int capacity = childHeight == 0 ? LeafCapacity : BranchCapacity;

        if (total <= capacity)
        {
            Redistribute(left, right, childHeight, total);
            if (childHeight == 0)
                Unsafe.As<Leaf>(left).Next = Unsafe.As<Leaf>(right).Next;

            int count = branch.Count - 1;
            Node[] children = branch.Children;
            Array.Copy(children, j + 2, children, j + 1, count - j - 1);
            children[count] = null!;
            branch.Count = count;
        }
        else
        {
            Redistribute(left, right, childHeight, total / 2);
        }

        Recount(branch, childHeight);
    }

    // Moves entries across the boundary between two adjacent siblings so `left` ends with `leftTarget`
    // of their combined entries (all of them, for a merge) and `right` with the rest.
    private static void Redistribute(Node left, Node right, int height, int leftTarget)
    {
        int leftCount = left.Count;
        int rightCount = right.Count;
        if (height == 0)
        {
            T[] l = Unsafe.As<Leaf>(left).Items;
            T[] r = Unsafe.As<Leaf>(right).Items;
            if (leftCount < leftTarget)
                MoveLeft(r, rightCount, l, leftCount, leftTarget - leftCount);
            else
                MoveRight(l, leftCount, r, rightCount, leftCount - leftTarget);
        }
        else
        {
            var lb = Unsafe.As<Branch>(left);
            var rb = Unsafe.As<Branch>(right);
            if (leftCount < leftTarget)
                MoveLeft(rb.Children, rightCount, lb.Children, leftCount, leftTarget - leftCount);
            else
                MoveRight(lb.Children, leftCount, rb.Children, rightCount, leftCount - leftTarget);

            lb.Count = leftTarget;
            rb.Count = leftCount + rightCount - leftTarget;
            Recount(lb, height - 1);
            Recount(rb, height - 1);
            return;
        }
        left.Count = leftTarget;
        right.Count = leftCount + rightCount - leftTarget;
    }

    // Appends the first `n` entries of `right` to `left`, closing the gap they leave in `right`.
    private static void MoveLeft<TItem>(TItem[] right, int rightCount, TItem[] left, int leftCount, int n)
    {
        Array.Copy(right, 0, left, leftCount, n);
        Array.Copy(right, n, right, 0, rightCount - n);
        Array.Clear(right, rightCount - n, n);
    }

    // Prepends the last `n` entries of `left` to `right`.
    private static void MoveRight<TItem>(TItem[] left, int leftCount, TItem[] right, int rightCount, int n)
    {
        Array.Copy(right, 0, right, n, rightCount);
        Array.Copy(left, leftCount - n, right, 0, n);
        Array.Clear(left, leftCount - n, n);
    }

    // ---- bulk build ---------------------------------------------------------------------------------

    // The bulk build fills nodes in order, so only the last node on a level can fall below the floor; even
    // it out with its left neighbour, which is at the three-quarter fill, so both end at least three
    // eighths full.
    private static void BalanceTail(List<Node> level, int height)
    {
        Node last = level[level.Count - 1];
        if (last.Count >= (height == 0 ? MinLeaf : MinBranch))
            return;

        Node previous = level[level.Count - 2];
        Redistribute(previous, last, height, (previous.Count + last.Count) / 2);
    }

    private static List<Node> BuildParents(List<Node> level, int childHeight)
    {
        var parents = new List<Node>((level.Count + BulkBranchFill - 1) / BulkBranchFill);
        var branch = new Branch();
        parents.Add(branch);
        foreach (Node child in level)
        {
            if (branch.Count == BulkBranchFill)
            {
                branch = new Branch();
                parents.Add(branch);
            }
            branch.Children[branch.Count++] = child;
        }

        foreach (Node parent in parents)
            Recount(Unsafe.As<Branch>(parent), childHeight);
        return parents;
    }

    // The number of elements under `node`: its own count for a leaf, the end of its last child otherwise.
    private static int SubtreeSize(Node node, int height) =>
        height == 0 ? node.Count : Unsafe.As<Branch>(node).Offsets[node.Count];

    // Rebuilds a branch's offsets from its children's sizes and re-pads every slot past the end. Every
    // structural change to a branch (a child added, merged away or moved to a sibling) ends here, which is
    // what keeps the int.MaxValue padding ChildContaining depends on intact.
    private static void Recount(Branch branch, int childHeight)
    {
        int[] offsets = branch.Offsets;
        Node[] children = branch.Children;
        int count = branch.Count;
        int sum = 0;
        for (int i = 0; i < count; i++)
        {
            offsets[i] = sum;
            sum += SubtreeSize(children[i], childHeight);
        }
        offsets[count] = sum;
        offsets.AsSpan(count + 1).Fill(int.MaxValue);
    }

    private static void ThrowIndexOutOfRange(int index) =>
        throw new ArgumentOutOfRangeException(nameof(index), index, "Index was out of range.");

    // ---- nodes --------------------------------------------------------------------------------------

    // The level a node sits on is tracked by the caller's descent, never by the node, so a node is told
    // apart from its kind by height alone and the casts above are Unsafe.As rather than type checks.
    private abstract class Node
    {
        // Elements in a leaf; children in a branch.
        public int Count;
    }

    private sealed class Leaf : Node
    {
        public readonly T[] Items = new T[LeafCapacity];
        public Leaf? Next;
    }

    private sealed class Branch : Node
    {
        public readonly Node[] Children = new Node[BranchCapacity];

        // Offsets[i] is where child i's elements start within this subtree, Offsets[Count] is the subtree's
        // element count, and every later slot is int.MaxValue. The vector's worth of slots past the capacity
        // lets ChildContaining read whole vectors with no tail loop.
        public readonly int[] Offsets = new int[BranchCapacity + 1 + Vector128<int>.Count];
    }

    /// <summary>
    /// A struct enumerator over a <see cref="BTreeList{T}"/> that walks the leaf chain front to back.
    /// Because it is a struct, iterating via <c>foreach</c> avoids the allocation a compiler-generated
    /// <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly BTreeList<T> _list;
        private readonly int _version;
        private Leaf? _leaf;
        private int _index;
        private T _current;

        internal Enumerator(BTreeList<T> list)
        {
            _list = list;
            _version = list._version;
            _leaf = list._first;
            _index = 0;
            _current = default!;
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly T Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next element.</summary>
        /// <returns><c>true</c> if the enumerator advanced to an element; <c>false</c> past the end.</returns>
        /// <exception cref="InvalidOperationException">The list was modified since the enumerator was created.</exception>
        public bool MoveNext()
        {
            if (_version != _list._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            Leaf? leaf = _leaf;
            while (leaf is not null)
            {
                if (_index < leaf.Count)
                {
                    _current = leaf.Items[_index++];
                    return true;
                }
                leaf = leaf.Next;
                _leaf = leaf;
                _index = 0;
            }

            _current = default!;
            return false;
        }

        /// <summary>Resets the enumerator to its initial position, before the first element.</summary>
        /// <exception cref="InvalidOperationException">The list was modified since the enumerator was created.</exception>
        public void Reset()
        {
            if (_version != _list._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _leaf = _list._first;
            _index = 0;
            _current = default!;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public readonly void Dispose() { }
    }
}
