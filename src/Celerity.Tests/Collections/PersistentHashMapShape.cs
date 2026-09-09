using System.Numerics;
using System.Reflection;

namespace Celerity.Tests.Collections;

/// <summary>
/// Walks a <c>PersistentHashMap</c>'s trie by reflection and asserts the shape invariants the type
/// documents but that no black-box assertion can see.
///
/// <para>
/// This exists because of one specific blind spot. The collapse rule — a node left holding a single entry
/// and no children is dissolved into its parent, level by level, all the way back up — is stated in the XML
/// docs, in the API reference and in the ROADMAP, and <b>a map that never collapsed at all would still
/// answer every lookup and every enumeration correctly</b>. The entry would simply sit under a chain of
/// nodes that exist only to reach it: slower, fatter, and invisible to a test that compares entries. That is
/// exactly the class of regression a structural check catches and a behavioural one cannot, which is the
/// same argument <see cref="RopeDifferentialTests"/> makes for walking a rope's nodes rather than trusting
/// its public <c>Depth</c>.
/// </para>
///
/// <para>
/// Reflection over private state is deliberate and follows that precedent. What is checked is the CHAMP
/// canonical form: every bitmap node's payload arrays are exactly as long as its occupancy maps say, no node
/// below the root is a single stranded entry, a collision node holds at least two entries and appears only
/// at the bottom of a full 32-bit descent, and no path is deeper than the structural bound the enumerator's
/// inline stack is sized from.
/// </para>
/// </summary>
internal static class PersistentHashMapShape
{
    // Levels sit at shift 0, 5, 10, 15, 20, 25 and 30; a collision node hangs one below the last of them.
    private const int MaxBitmapDepth = 7;
    private const int MaxDepth = MaxBitmapDepth + 1;

    /// <summary>
    /// Asserts the trie of <paramref name="map"/> is in canonical CHAMP form, and returns the number of
    /// entries it accounts for — which the caller reconciles against <c>Count</c> to prove the walk saw the
    /// whole map. The out-of-band <c>default(TKey)</c> entry is included in that total even though it lives
    /// on the map rather than in the trie, so the two numbers are directly comparable.
    /// </summary>
    internal static int AssertCanonical(object map)
    {
        Type type = map.GetType();

        FieldInfo rootField = type.GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PersistentHashMap no longer has a _root field.");

        FieldInfo defaultKeyField = type.GetField("_hasDefaultKey", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PersistentHashMap no longer has a _hasDefaultKey field.");

        object root = rootField.GetValue(map)
            ?? throw new InvalidOperationException("PersistentHashMap._root was null.");

        int outOfBand = (bool)defaultKeyField.GetValue(map)! ? 1 : 0;
        return AssertNode(root, depth: 0, isRoot: true) + outOfBand;
    }

    private static int AssertNode(object node, int depth, bool isRoot)
    {
        Assert.True(depth < MaxDepth, $"trie is {depth + 1} levels deep, past the structural bound of {MaxDepth}.");

        int dataMap = Get<int>(node, "DataMap");
        int nodeMap = Get<int>(node, "NodeMap");
        Array keys = Get<Array>(node, "Keys");
        Array values = Get<Array>(node, "Values");
        Array nodes = Get<Array>(node, "Nodes");

        Assert.Equal(keys.Length, values.Length);

        bool isCollision = dataMap == 0 && nodeMap == 0 && keys.Length > 0;

        if (isCollision)
        {
            // A collision node can only be reached by consuming all 32 hash bits, and it is only ever built
            // for two or more keys — one left in it is the shape the parent is required to dissolve.
            Assert.Equal(MaxBitmapDepth, depth);
            Assert.True(keys.Length >= 2, $"collision node holds {keys.Length} entries; it should have been dissolved.");
            Assert.Equal(0, nodes.Length);
            return keys.Length;
        }

        // A bitmap node's arrays are exactly as long as its occupancy maps claim; anything else means a
        // popcount-indexed read can land outside the payload the node actually holds.
        Assert.Equal(BitOperations.PopCount((uint)dataMap), keys.Length);
        Assert.Equal(BitOperations.PopCount((uint)nodeMap), nodes.Length);
        Assert.Equal(0, dataMap & nodeMap);

        // The collapse rule. The root is exempt: nothing sits above it to inline into, so a one-entry map is
        // legitimately a root holding one entry.
        if (!isRoot)
        {
            Assert.False(
                keys.Length == 1 && nodes.Length == 0,
                $"a node at depth {depth} holds one entry and no children — it should have been dissolved into its parent.");

            Assert.False(
                keys.Length == 0 && nodes.Length == 0,
                $"a node at depth {depth} is empty and should not exist.");
        }

        int found = keys.Length;
        foreach (object? child in nodes)
        {
            Assert.NotNull(child);
            found += AssertNode(child!, depth + 1, isRoot: false);
        }

        return found;
    }

    private static T Get<T>(object node, string field)
    {
        FieldInfo info = node.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"PersistentHashMap.Node no longer has a {field} field.");

        return (T)info.GetValue(node)!;
    }
}
