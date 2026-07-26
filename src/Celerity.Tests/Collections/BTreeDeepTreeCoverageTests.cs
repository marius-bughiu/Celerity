using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// The B-tree paths that only appear once the tree is <b>three levels deep</b>, plus the two degenerate
/// entry points that a populated tree never reaches.
///
/// <para>
/// <b>The deep-tree deletions.</b> <c>RemoveFromInternal</c> replaces a deleted internal key with its
/// in-order predecessor (the rightmost key of the left subtree) or successor (the leftmost key of the right
/// subtree), and walks down to find it with <c>while (!cursor.IsLeaf) cursor = cursor.Children![…]</c>. In a
/// two-level tree that flanking child <i>is</i> a leaf, so the walk body never executes and the descent is
/// never actually exercised. With <c>MinDegree == 16</c> a node holds at most 31 keys, so a third level only
/// appears past roughly a thousand entries — which is why these tests build a tree of several thousand and
/// then delete every entry. Deleting all of them (rather than a chosen few) is what guarantees the deletion
/// eventually lands on an internal key whose flanking child is itself internal, in both the predecessor and
/// the successor direction, at more than one depth.
/// </para>
///
/// <para>
/// Each deletion is reconciled against a BCL oracle rather than merely counted: a descent that stopped one
/// level too early would still leave the right <i>number</i> of entries while silently promoting the wrong
/// key, and only an order-sensitive comparison catches that.
/// </para>
///
/// <para>
/// <b>The empty-tree range seek.</b> <c>SeekLowerBound</c> is written as <c>while (node is not null)</c> but
/// every populated path leaves through the <c>found || node.IsLeaf</c> return, so the loop condition only
/// fails when the seek starts from a null root — i.e. when <c>EnumerateRange</c> is called on an empty tree.
/// </para>
/// </summary>
public class BTreeDeepTreeCoverageTests
{
    // Comfortably past MinDegree * MaxKeys (16 * 31), so the tree is three levels deep before any deletion.
    private const int DeepCount = 5000;

    // Fixed seed: a deletion order that exposes a descent bug should reproduce exactly, not once in ten runs.
    private const int Seed = 20260726;

    private static int[] ShuffledKeys(int count, int seed)
    {
        var keys = new int[count];
        for (int i = 0; i < count; i++)
            keys[i] = i;

        var rng = new Random(seed);
        for (int i = count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (keys[i], keys[j]) = (keys[j], keys[i]);
        }

        return keys;
    }

    [Fact]
    public void Remove_ShouldPromoteTheCorrectSeparator_WhenTheFlankingChildIsItselfInternal()
    {
        var set = new BTreeSet<int>();
        var oracle = new SortedSet<int>();

        foreach (int k in ShuffledKeys(DeepCount, Seed))
        {
            set.Add(k);
            oracle.Add(k);
        }

        Assert.Equal(oracle.Count, set.Count);

        // Delete in a different order from the insert order, so the deletions land on separators sitting at
        // several depths rather than repeatedly on the same one.
        int[] removalOrder = ShuffledKeys(DeepCount, Seed + 1);
        for (int i = 0; i < removalOrder.Length; i++)
        {
            int k = removalOrder[i];
            Assert.True(set.Remove(k));
            oracle.Remove(k);

            // Full order-sensitive reconciliation periodically (and over the last few hundred deletions, where
            // the merges and borrows are densest); a cheap check every step in between.
            if (i % 250 == 0 || i > DeepCount - 300)
            {
                Assert.Equal(oracle, set);
                if (oracle.Count > 0)
                {
                    Assert.Equal(oracle.Min, set.Min);
                    Assert.Equal(oracle.Max, set.Max);
                }
            }
            else
            {
                Assert.Equal(oracle.Count, set.Count);
                Assert.False(set.Contains(k));
            }
        }

        Assert.Empty(set);
        Assert.False(set.Remove(0));
    }

    [Fact]
    public void Remove_ShouldPromoteTheCorrectEntry_WhenTheFlankingChildIsItselfInternal()
    {
        var map = new BTreeDictionary<int, string>();

        // The oracle's value type is nullable to match BTreeDictionary's TValue? surface, so Min / Max compare
        // without a nullability-mismatch conversion.
        var oracle = new SortedDictionary<int, string?>();

        foreach (int k in ShuffledKeys(DeepCount, Seed))
        {
            map.Add(k, $"v{k}");
            oracle.Add(k, $"v{k}");
        }

        Assert.Equal(oracle.Count, map.Count);

        int[] removalOrder = ShuffledKeys(DeepCount, Seed + 1);
        for (int i = 0; i < removalOrder.Length; i++)
        {
            int k = removalOrder[i];

            // The value travels with the promoted key, so a descent that promoted the wrong entry would
            // desynchronize key and value even when the key order stayed plausible.
            Assert.True(map.Remove(k, out string? removed));
            Assert.Equal($"v{k}", removed);
            oracle.Remove(k);

            if (i % 250 == 0 || i > DeepCount - 300)
            {
                Assert.Equal(oracle.Keys, map.Keys);
                Assert.Equal(oracle.Values, map.Values);
                if (oracle.Count > 0)
                {
                    Assert.Equal(oracle.First(), map.Min);
                    Assert.Equal(oracle.Last(), map.Max);
                }
            }
            else
            {
                Assert.Equal(oracle.Count, map.Count);
                Assert.False(map.ContainsKey(k));
            }
        }

        Assert.Empty(map);
    }

    [Fact]
    public void EnumerateRange_ShouldYieldNothing_WhenTheSetIsEmpty()
    {
        var set = new BTreeSet<int>();

        Assert.Empty(set.EnumerateRange(0, 100));

        // Still empty after a fill-and-drain, which nulls the root back out through a different path than
        // "never populated".
        set.Add(5);
        set.Remove(5);
        Assert.Empty(set.EnumerateRange(0, 100));

        set.Add(42);
        set.Clear();
        Assert.Empty(set.EnumerateRange(0, 100));

        // And the enumerator is still usable once elements come back.
        set.Add(7);
        Assert.Equal(new[] { 7 }, set.EnumerateRange(0, 100));
    }

    [Fact]
    public void EnumerateRange_ShouldYieldNothing_WhenTheDictionaryIsEmpty()
    {
        var map = new BTreeDictionary<int, string>();

        Assert.Empty(map.EnumerateRange(0, 100));

        map.Add(5, "five");
        map.Remove(5);
        Assert.Empty(map.EnumerateRange(0, 100));

        map.Add(42, "forty-two");
        map.Clear();
        Assert.Empty(map.EnumerateRange(0, 100));

        map.Add(7, "seven");
        Assert.Equal(new[] { KeyValuePair.Create(7, (string?)"seven") }, map.EnumerateRange(0, 100));
    }

    /// <summary>
    /// <c>ICollection&lt;KeyValuePair&gt;.Contains</c> is a pair lookup, not a key lookup: it must match on the
    /// value too, and must short-circuit without consulting the value when the key is absent entirely. Only
    /// the fully-matching case was exercised, leaving the short-circuit arm untaken.
    /// </summary>
    [Fact]
    public void ICollectionContains_ShouldMatchOnKeyAndValue_WhenEitherDiffers()
    {
        var map = new BTreeDictionary<int, string> { { 1, "one" }, { 2, "two" } };
        ICollection<KeyValuePair<int, string?>> collection = map;

        // Each result is bound to a local before asserting: `Assert.Contains(pair, collection)` would walk the
        // enumerator and never call the method under test (and would trip xUnit2017 the other way round).
        bool matchingPair = collection.Contains(KeyValuePair.Create(1, (string?)"one"));
        Assert.True(matchingPair);

        // Key present, value different — the second operand decides.
        bool wrongValue = collection.Contains(KeyValuePair.Create(1, (string?)"uno"));
        Assert.False(wrongValue);

        // Key absent — the first operand short-circuits and the value is never compared.
        bool absentKey = collection.Contains(KeyValuePair.Create(99, (string?)"one"));
        Assert.False(absentKey);

        // Null values participate in the comparison rather than being treated as "any".
        map.Add(3, null);
        bool matchingNull = collection.Contains(KeyValuePair.Create(3, (string?)null));
        Assert.True(matchingNull);
        bool nullVersusValue = collection.Contains(KeyValuePair.Create(3, (string?)"three"));
        Assert.False(nullVersusValue);
    }
}
