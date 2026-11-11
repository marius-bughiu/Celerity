using System.Reflection;

namespace Celerity.Tests.Collections;

/// <summary>
/// Walks a <c>PersistentSortedMap</c>'s B+ tree by reflection and asserts the shape invariants the type
/// documents but that no black-box assertion can see.
///
/// <para>
/// The blind spot is the same one <see cref="PersistentHashMapShape"/> exists for. A tree that never
/// rebalanced on removal — leaving a leaf of one entry, or a branch of one child — would still answer every
/// lookup, every bound query and every enumeration correctly; it would just be deeper and fatter than the
/// depth bound the enumerator's inline stack is sized from. So would one whose leaves drifted to different
/// depths. What is checked: every leaf sits at the same depth; every node except the root holds between
/// <see cref="MinFill"/> and <see cref="MaxFill"/> entries or children; a root branch has at least two
/// children; keys ascend strictly within a leaf and across the whole tree; each separator bounds the
/// subtrees on either side of it; and no path is deeper than <see cref="MaxDepth"/>.
/// </para>
/// </summary>
internal static class PersistentSortedMapShape
{
    private const int MaxFill = 32;
    private const int MinFill = MaxFill / 2;
    private const int MaxDepth = 8;

    /// <summary>
    /// Asserts the tree of <paramref name="map"/> is a valid B+ tree under <typeparamref name="TComparer"/>,
    /// and returns the number of entries it accounts for, which the caller reconciles against <c>Count</c>.
    /// </summary>
    internal static int AssertValid<TKey, TComparer>(object map)
        where TComparer : struct, IComparer<TKey>
    {
        Type type = map.GetType();
        FieldInfo rootField = type.GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{type.Name} no longer has a _root field.");

        object root = rootField.GetValue(map)
            ?? throw new InvalidOperationException($"{type.Name}._root was null.");

        int leafDepth = -1;
        bool havePrevious = false;
        TKey previous = default!;
        return AssertNode<TKey, TComparer>(
            root, depth: 0, isRoot: true, ref leafDepth, ref havePrevious, ref previous);
    }

    private static int AssertNode<TKey, TComparer>(
        object node, int depth, bool isRoot, ref int leafDepth, ref bool havePrevious, ref TKey previous)
        where TComparer : struct, IComparer<TKey>
    {
        Assert.True(depth < MaxDepth, $"tree is {depth + 1} levels deep, past the structural bound of {MaxDepth}.");

        int count = Get<int>(node, "Count");
        var keys = Get<TKey[]>(node, "Keys");
        var children = Get<Array?>(node, "Children");

        Assert.True(count <= MaxFill, $"a node at depth {depth} holds {count}, past MaxFill {MaxFill}.");
        if (!isRoot)
            Assert.True(count >= MinFill, $"a node at depth {depth} holds {count}, below MinFill {MinFill}.");

        if (children is null)
        {
            // Every leaf at one depth: the first leaf reached fixes it.
            if (leafDepth < 0)
                leafDepth = depth;

            Assert.Equal(leafDepth, depth);
            Array values = Get<Array>(node, "Values");
            Assert.True(keys.Length >= count && values.Length >= count);

            for (int i = 0; i < count; i++)
            {
                if (havePrevious)
                {
                    Assert.True(
                        default(TComparer).Compare(previous, keys[i]) < 0,
                        $"keys do not ascend strictly: {previous} then {keys[i]}.");
                }

                previous = keys[i];
                havePrevious = true;
            }

            return count;
        }

        if (isRoot)
            Assert.True(count >= 2, $"a root branch holds {count} child; it should have collapsed.");

        Assert.True(keys.Length >= count - 1 && children.Length >= count);

        int found = 0;
        for (int i = 0; i < count; i++)
        {
            object child = children.GetValue(i) ?? throw new InvalidOperationException("null child.");

            // Separator i - 1 is a lower bound on everything under child i: the last key seen before descending
            // into it orders strictly below the separator, and the first key inside orders at or above it.
            if (i > 0 && havePrevious)
            {
                Assert.True(
                    default(TComparer).Compare(previous, keys[i - 1]) < 0,
                    $"separator {keys[i - 1]} does not order above the subtree before it (last key {previous}).");
            }

            int before = found;
            found += AssertNode<TKey, TComparer>(child, depth + 1, isRoot: false, ref leafDepth, ref havePrevious, ref previous);
            Assert.True(found > before, "a child subtree is empty.");

            if (i > 0)
            {
                TKey first = FirstKey<TKey>(child);
                Assert.True(
                    default(TComparer).Compare(keys[i - 1], first) <= 0,
                    $"separator {keys[i - 1]} orders above the first key {first} of the subtree after it.");
            }
        }

        return found;
    }

    private static TKey FirstKey<TKey>(object node)
    {
        while (Get<Array?>(node, "Children") is { } children)
            node = children.GetValue(0)!;

        return Get<TKey[]>(node, "Keys")[0];
    }

    private static T Get<T>(object node, string field)
    {
        FieldInfo info = node.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{node.GetType().FullName} no longer has a {field} field.");

        return (T)info.GetValue(node)!;
    }
}
