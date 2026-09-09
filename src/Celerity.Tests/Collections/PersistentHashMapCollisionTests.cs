using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// <see cref="PersistentHashMap{TKey, TValue, THasher}"/> under hash collisions.
///
/// <para>
/// A CHAMP trie separates two keys by reading five bits of their hashes at a time, so two keys that share a
/// <i>prefix</i> only cost depth — the merge recurses until the bits differ. Two keys that share the
/// <b>whole 32-bit hash</b> can never be separated, and the trie answers that with a <b>collision node</b>
/// under seven levels of descent, holding every entry with that hash in one flat list. That node is the one
/// shape in the structure that is not a bitmap node, and every operation has a separate path for it: lookup,
/// insert, overwrite and removal. Nothing in the ordinary tests reaches any of them, because no realistic
/// hasher produces a full 32-bit collision on a small key set — so these use hashers built to force one.
/// </para>
///
/// <para>
/// The collapse rule is pinned here too. A collision node that drops to a single entry is dissolved into its
/// parent, and because that parent may then be a single-entry node itself, the dissolution walks back up. A
/// key stranded under seven nodes that exist only to reach it would still be <i>found</i>; what would be
/// wrong is the shape, and the way that shows up is a map that no longer equals the one built from the same
/// surviving keys.
/// </para>
/// </summary>
public class PersistentHashMapCollisionTests
{
    // Distinct keys 8 apart share a hash exactly, so {8, 16, 24} collide fully and so do {1, 9, 17}. The
    // three low bits also mean the whole map lives in eight root slots, which keeps the trie shallow
    // everywhere except under a collision.
    private struct LowThreeBitsHasher : IHashProvider<int>
    {
        public int Hash(int key) => key & 7;
    }

    // Every key in one bucket: a single collision node hanging under seven chained single-child nodes.
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    private struct ConstantStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 0;
    }

    private static PersistentHashMap<int, string, LowThreeBitsHasher> Colliding =>
        PersistentHashMap<int, string, LowThreeBitsHasher>.Empty;

    [Fact]
    public void TwoKeysWithTheSameHash_ShouldBothBeStored()
    {
        PersistentHashMap<int, string, LowThreeBitsHasher> map = Colliding.Add(8, "eight").Add(16, "sixteen");

        Assert.Equal(2, map.Count);
        Assert.Equal("eight", map[8]);
        Assert.Equal("sixteen", map[16]);
    }

    [Fact]
    public void ManyKeysWithTheSameHash_ShouldAllBeStored()
    {
        PersistentHashMap<int, string, ConstantIntHasher> map =
            PersistentHashMap<int, string, ConstantIntHasher>.Empty;

        for (int key = 1; key <= 50; key++)
            map = map.Add(key, key.ToString());

        Assert.Equal(50, map.Count);
        for (int key = 1; key <= 50; key++)
            Assert.Equal(key.ToString(), map[key]);

        Assert.False(map.ContainsKey(51));
        Assert.Equal(50, map.Count());
    }

    [Fact]
    public void LookupOfAnAbsentKey_InACollisionNode_ShouldReturnFalse()
    {
        PersistentHashMap<int, string, LowThreeBitsHasher> map = Colliding.Add(8, "eight").Add(16, "sixteen");

        // 24 hashes into the same collision node but is not in it.
        Assert.False(map.TryGetValue(24, out string? value));
        Assert.Null(value);
    }

    [Fact]
    public void Add_ShouldThrow_OnADuplicateInsideACollisionNode()
    {
        PersistentHashMap<int, string, LowThreeBitsHasher> map = Colliding.Add(8, "eight").Add(16, "sixteen");

        Assert.Throws<ArgumentException>(() => map.Add(16, "XVI"));
    }

    [Fact]
    public void SetItem_InsideACollisionNode_ShouldOverwriteWithoutTouchingTheReceiver()
    {
        PersistentHashMap<int, string, LowThreeBitsHasher> map =
            Colliding.Add(8, "eight").Add(16, "sixteen").Add(24, "twenty-four");

        PersistentHashMap<int, string, LowThreeBitsHasher> updated = map.SetItem(16, "XVI");

        Assert.Equal("XVI", updated[16]);
        Assert.Equal("sixteen", map[16]);
        Assert.Equal(3, updated.Count);
        Assert.Equal("eight", updated[8]);
        Assert.Equal("twenty-four", updated[24]);
    }

    [Fact]
    public void SetItem_InsideACollisionNode_ShouldReturnTheReceiver_WhenTheValueIsAlreadyEqual()
    {
        PersistentHashMap<int, string, LowThreeBitsHasher> map = Colliding.Add(8, "eight").Add(16, "sixteen");

        Assert.Same(map, map.SetItem(16, "sixteen"));
    }

    [Fact]
    public void Remove_FromACollisionNodeOfThree_ShouldLeaveTheOtherTwo()
    {
        PersistentHashMap<int, string, LowThreeBitsHasher> map =
            Colliding.Add(8, "eight").Add(16, "sixteen").Add(24, "twenty-four");

        PersistentHashMap<int, string, LowThreeBitsHasher> without = map.Remove(16);

        Assert.Equal(2, without.Count);
        Assert.False(without.ContainsKey(16));
        Assert.Equal("eight", without[8]);
        Assert.Equal("twenty-four", without[24]);
        Assert.Equal(3, map.Count);
    }

    [Fact]
    public void Remove_OfAKeyAbsentFromACollisionNode_ShouldReturnTheReceiver()
    {
        PersistentHashMap<int, string, LowThreeBitsHasher> map = Colliding.Add(8, "eight").Add(16, "sixteen");

        Assert.Same(map, map.Remove(24));
    }

    [Fact]
    public void Remove_ThatEmptiesACollisionNodeToOne_ShouldDissolveItIntoTheTrie()
    {
        // Both keys sit under a collision node seven levels down. Removing one must not leave the survivor
        // stranded there: the surviving entry is inlined back up, level by level, until it sits beside the
        // keys it shares a root slot with.
        PersistentHashMap<int, string, LowThreeBitsHasher> map = Colliding
            .Add(1, "one")
            .Add(2, "two")
            .Add(8, "eight")
            .Add(16, "sixteen");

        PersistentHashMap<int, string, LowThreeBitsHasher> without = map.Remove(16);

        Assert.Equal(3, without.Count);
        Assert.Equal("eight", without[8]);
        Assert.Equal("one", without[1]);
        Assert.Equal("two", without[2]);

        // The drained map must behave exactly like the one built from the survivors directly.
        PersistentHashMap<int, string, LowThreeBitsHasher> rebuilt =
            Colliding.Add(1, "one").Add(2, "two").Add(8, "eight");

        Assert.Equal(
            rebuilt.OrderBy(entry => entry.Key).ToArray(),
            without.OrderBy(entry => entry.Key).ToArray());
    }

    [Fact]
    public void DrainingACollisionNodeCompletely_ShouldEndAtEmpty()
    {
        PersistentHashMap<int, string, ConstantIntHasher> map =
            PersistentHashMap<int, string, ConstantIntHasher>.Empty;

        for (int key = 1; key <= 20; key++)
            map = map.Add(key, key.ToString());

        for (int key = 1; key <= 20; key++)
            map = map.Remove(key);

        Assert.Same(PersistentHashMap<int, string, ConstantIntHasher>.Empty, map);
    }

    [Fact]
    public void CollidingStringKeys_ShouldRoundTripAlongsideTheNullKey()
    {
        PersistentHashMap<string, int, ConstantStringHasher> map =
            PersistentHashMap<string, int, ConstantStringHasher>.Empty
                .Add(null!, -1)
                .Add("alpha", 1)
                .Add("beta", 2)
                .Add("gamma", 3);

        Assert.Equal(4, map.Count);
        Assert.Equal(-1, map[null!]);
        Assert.Equal(2, map["beta"]);
        Assert.Equal(
            new[] { "alpha", "beta", "gamma" },
            map.Keys.Where(key => key is not null).OrderBy(key => key, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void APrefixCollisionShortOfAFullOne_ShouldSplitRatherThanCollide()
    {
        // Identity hashing: 1 and 33 agree on the low five bits and differ on the next five, so they are
        // separated by a bitmap node one level down rather than by a collision node.
        PersistentHashMap<int, string, Int32IdentityHasher> map =
            PersistentHashMap<int, string, Int32IdentityHasher>.Empty.Add(1, "one").Add(33, "thirty-three");

        Assert.Equal("one", map[1]);
        Assert.Equal("thirty-three", map[33]);
        Assert.Equal(2, map.Count);

        // And removing one of them dissolves the split node again.
        Assert.Equal(1, map.Remove(33).Count);
        Assert.Equal("one", map.Remove(33)[1]);
    }

    [Fact]
    public void ABuilderShouldHandleCollisionNodesToo()
    {
        var builder = new PersistentHashMap<int, string, ConstantIntHasher>.Builder();

        for (int key = 1; key <= 40; key++)
            builder.Add(key, key.ToString());

        PersistentHashMap<int, string, ConstantIntHasher> snapshot = builder.ToImmutable();

        builder[7] = "seven!";
        Assert.True(builder.Remove(9));

        Assert.Equal(40, snapshot.Count);
        Assert.Equal("7", snapshot[7]);
        Assert.Equal("9", snapshot[9]);
        Assert.Equal(39, builder.Count);
        Assert.Equal("seven!", builder[7]);
        Assert.False(builder.ContainsKey(9));
    }
}
