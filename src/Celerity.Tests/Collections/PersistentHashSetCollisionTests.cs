using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// <see cref="PersistentHashSet{T, THasher}"/> under hash collisions.
///
/// <para>
/// A CHAMP trie separates two elements by reading five bits of their hashes at a time, so two elements that
/// share a <i>prefix</i> only cost depth. Two that share the <b>whole 32-bit hash</b> can never be separated,
/// and the trie answers that with a <b>collision node</b> under seven levels of descent, holding every
/// element with that hash in one flat list. That node is the one shape in the structure that is not a bitmap
/// node, and every operation has a separate path for it: membership, insert and removal. Nothing in the
/// ordinary tests reaches any of them, because no realistic hasher produces a full 32-bit collision on a
/// small element set — so these use hashers built to force one.
/// </para>
///
/// <para>
/// The collapse rule is pinned here too, structurally: a collision node that drops to a single element is
/// dissolved into its parent, and the dissolution walks back up. A stranded element would still be
/// <i>found</i>, so the check is <see cref="PersistentHashMapShape.AssertCanonical"/>, not a lookup.
/// </para>
/// </summary>
public class PersistentHashSetCollisionTests
{
    // Distinct elements 8 apart share a hash exactly, so {8, 16, 24} collide fully and so do {1, 9, 17}.
    private struct LowThreeBitsHasher : IHashProvider<int>
    {
        public int Hash(int key) => key & 7;
    }

    // Every element in one bucket: a single collision node hanging under seven chained single-child nodes.
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    private struct ConstantStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 0;
    }

    private static PersistentHashSet<int, LowThreeBitsHasher> Colliding =>
        PersistentHashSet<int, LowThreeBitsHasher>.Empty;

    [Fact]
    public void TwoElementsWithTheSameHash_ShouldBothBeStored()
    {
        PersistentHashSet<int, LowThreeBitsHasher> set = Colliding.Add(8).Add(16);

        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(8));
        Assert.True(set.Contains(16));
        Assert.Equal(2, PersistentHashMapShape.AssertCanonical(set));
    }

    [Fact]
    public void ManyElementsWithTheSameHash_ShouldAllBeStored()
    {
        PersistentHashSet<int, ConstantIntHasher> set = PersistentHashSet<int, ConstantIntHasher>.Empty;

        for (int item = 1; item <= 50; item++)
            set = set.Add(item);

        Assert.Equal(50, set.Count);
        for (int item = 1; item <= 50; item++)
            Assert.True(set.Contains(item));

        Assert.False(set.Contains(51));
        Assert.Equal(Enumerable.Range(1, 50), set.OrderBy(item => item));
    }

    [Fact]
    public void Add_OfAnElementAlreadyInACollisionNode_ShouldReturnTheReceiver()
    {
        PersistentHashSet<int, LowThreeBitsHasher> set = Colliding.Add(8).Add(16).Add(24);

        Assert.Same(set, set.Add(16));
    }

    [Fact]
    public void TryGetValue_InsideACollisionNode_ShouldReturnTheStoredInstance_OrTheArgument()
    {
        string stored = new('b', 4);
        PersistentHashSet<string, ConstantStringHasher> set =
            PersistentHashSet<string, ConstantStringHasher>.Empty.Add("a").Add(stored).Add("c");

        Assert.True(set.TryGetValue(new string('b', 4), out string actual));
        Assert.Same(stored, actual);

        string probe = new('z', 2);
        Assert.False(set.TryGetValue(probe, out string missing));
        Assert.Same(probe, missing);
    }

    [Fact]
    public void Remove_FromACollisionNodeOfThree_ShouldLeaveTheOtherTwo()
    {
        PersistentHashSet<int, LowThreeBitsHasher> set = Colliding.Add(8).Add(16).Add(24);

        PersistentHashSet<int, LowThreeBitsHasher> without = set.Remove(16);

        Assert.Equal(2, without.Count);
        Assert.False(without.Contains(16));
        Assert.True(without.Contains(8));
        Assert.True(without.Contains(24));
        Assert.Equal(3, set.Count);
        Assert.Equal(2, PersistentHashMapShape.AssertCanonical(without));
    }

    [Fact]
    public void Remove_OfAnElementAbsentFromACollisionNode_ShouldReturnTheReceiver()
    {
        PersistentHashSet<int, LowThreeBitsHasher> set = Colliding.Add(8).Add(16);

        Assert.Same(set, set.Remove(24));
    }

    [Fact]
    public void Remove_ThatEmptiesACollisionNodeToOne_ShouldDissolveItIntoTheTrie()
    {
        PersistentHashSet<int, LowThreeBitsHasher> set = Colliding.Add(1).Add(2).Add(8).Add(16);

        PersistentHashSet<int, LowThreeBitsHasher> without = set.Remove(16);

        Assert.Equal(new[] { 1, 2, 8 }, without.OrderBy(item => item).ToArray());

        // Behaviour alone cannot see this: a survivor stranded under the seven-node chain the collision node
        // hung from would answer every lookup and every enumeration identically.
        Assert.Equal(without.Count, PersistentHashMapShape.AssertCanonical(without));
        Assert.Equal(set.Count, PersistentHashMapShape.AssertCanonical(set));
    }

    [Fact]
    public void DrainingACollisionHeavySetOneElementAtATime_ShouldKeepTheTrieCanonicalThroughout()
    {
        PersistentHashSet<int, LowThreeBitsHasher> set = Colliding;
        for (int item = 1; item <= 60; item++)
            set = set.Add(item);

        Assert.Equal(set.Count, PersistentHashMapShape.AssertCanonical(set));

        for (int item = 1; item <= 60; item++)
        {
            set = set.Remove(item);
            Assert.Equal(set.Count, PersistentHashMapShape.AssertCanonical(set));
        }

        Assert.Same(PersistentHashSet<int, LowThreeBitsHasher>.Empty, set);
    }

    [Fact]
    public void DrainingACollisionNodeCompletely_ShouldEndAtEmpty()
    {
        PersistentHashSet<int, ConstantIntHasher> set = PersistentHashSet<int, ConstantIntHasher>.Empty;

        for (int item = 1; item <= 20; item++)
            set = set.Add(item);

        for (int item = 1; item <= 20; item++)
            set = set.Remove(item);

        Assert.Same(PersistentHashSet<int, ConstantIntHasher>.Empty, set);
    }

    [Fact]
    public void CollidingStringElements_ShouldRoundTripAlongsideTheNullElement()
    {
        PersistentHashSet<string, ConstantStringHasher> set =
            PersistentHashSet<string, ConstantStringHasher>.Empty
                .Add(null!)
                .Add("alpha")
                .Add("beta")
                .Add("gamma");

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(null!));
        Assert.True(set.Contains("beta"));
        Assert.Equal(
            new[] { "alpha", "beta", "gamma" },
            set.Where(item => item is not null).OrderBy(item => item, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void APrefixCollisionShortOfAFullOne_ShouldSplitRatherThanCollide()
    {
        // Identity hashing: 1 and 33 agree on the low five bits and differ on the next five, so they are
        // separated by a bitmap node one level down rather than by a collision node.
        PersistentHashSet<int, Int32IdentityHasher> set = PersistentHashSet<int, Int32IdentityHasher>.Empty.Add(1).Add(33);

        Assert.True(set.Contains(1));
        Assert.True(set.Contains(33));
        Assert.Equal(2, set.Count);

        PersistentHashSet<int, Int32IdentityHasher> without = set.Remove(33);
        Assert.Equal(1, without.Count);
        Assert.True(without.Contains(1));
        Assert.Equal(1, PersistentHashMapShape.AssertCanonical(without));
    }

    [Fact]
    public void ABuilderShouldHandleCollisionNodesToo()
    {
        var builder = new PersistentHashSet<int, ConstantIntHasher>.Builder();

        for (int item = 1; item <= 40; item++)
            builder.Add(item);

        PersistentHashSet<int, ConstantIntHasher> snapshot = builder.ToImmutable();

        Assert.True(builder.Remove(9));
        Assert.False(builder.Remove(9));
        Assert.True(builder.Add(99));

        Assert.Equal(40, snapshot.Count);
        Assert.True(snapshot.Contains(9));
        Assert.False(snapshot.Contains(99));
        Assert.Equal(40, builder.Count);
        Assert.False(builder.Contains(9));
        Assert.Equal(40, PersistentHashMapShape.AssertCanonical(builder.ToImmutable()));
    }

    [Fact]
    public void SetAlgebra_ShouldWorkAcrossCollisionNodes()
    {
        PersistentHashSet<int, LowThreeBitsHasher> left = Colliding.Add(8).Add(16).Add(24).Add(1);

        Assert.Equal(new[] { 1, 8, 16, 24, 32 }, left.Union([32, 16]).OrderBy(item => item).ToArray());
        Assert.Equal(new[] { 1, 24 }, left.Except([8, 16]).OrderBy(item => item).ToArray());
        Assert.Equal(new[] { 8, 16 }, left.Intersect([16, 8, 40]).OrderBy(item => item).ToArray());
        Assert.Equal(new[] { 1, 24, 40 }, left.SymmetricExcept([8, 16, 40]).OrderBy(item => item).ToArray());
    }
}
