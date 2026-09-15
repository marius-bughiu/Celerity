using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// <see cref="PersistentHashSet{T, THasher}.Builder"/>: the transient accumulator and, above all, the
/// isolation it owes the sets it hands out.
///
/// <para>
/// <b>Isolation is the thing on trial, not the last answer.</b> The builder writes into nodes <i>in
/// place</i> rather than path-copying them, and it is allowed to do that only for nodes no published set can
/// still see. The bookkeeping is an ownership token: a node carries the token of the builder that made it,
/// <c>ToImmutable</c> takes a fresh token, and a node whose token no longer matches is copied instead of
/// written. Get any part of that wrong and the current answer is still right — the damage lands on a set
/// handed out <i>earlier</i>, which is why every test here keeps snapshots and re-checks them at the end
/// rather than only asserting on the builder.
/// </para>
/// </summary>
public class PersistentHashSetBuilderTests
{
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    private static PersistentHashSet<int, Int32IdentityHasher> EmptySet =>
        PersistentHashSet<int, Int32IdentityHasher>.Empty;

    private static PersistentHashSet<int, Int32IdentityHasher>.Builder NewBuilder() => new();

    // ── The basics ───────────────────────────────────────────────────────────────

    [Fact]
    public void NewBuilder_ShouldBeEmpty()
    {
        PersistentHashSet<int, Int32IdentityHasher>.Builder builder = NewBuilder();

        Assert.Equal(0, builder.Count);
        Assert.False(builder.Contains(1));
        Assert.False(builder.Contains(0));
        Assert.Same(EmptySet, builder.ToImmutable());
    }

    [Fact]
    public void Add_ShouldReportWhetherTheElementWasNew()
    {
        PersistentHashSet<int, Int32IdentityHasher>.Builder builder = NewBuilder();

        Assert.True(builder.Add(1));
        Assert.False(builder.Add(1));

        Assert.Equal(1, builder.Count);
        Assert.True(builder.Contains(1));
    }

    [Fact]
    public void Remove_ShouldReportWhetherTheElementWasThere()
    {
        PersistentHashSet<int, Int32IdentityHasher>.Builder builder = NewBuilder();
        builder.Add(1);

        Assert.False(builder.Remove(2));
        Assert.True(builder.Remove(1));
        Assert.Equal(0, builder.Count);
        Assert.Same(EmptySet, builder.ToImmutable());
    }

    [Fact]
    public void TheDefaultElement_ShouldBehaveLikeAnyOtherElement()
    {
        PersistentHashSet<int, Int32IdentityHasher>.Builder builder = NewBuilder();

        Assert.False(builder.Remove(0));
        Assert.False(builder.Contains(0));

        Assert.True(builder.Add(0));
        Assert.False(builder.Add(0));
        Assert.Equal(1, builder.Count);
        Assert.True(builder.Contains(0));

        Assert.True(builder.Remove(0));
        Assert.Equal(0, builder.Count);
        Assert.False(builder.Contains(0));
    }

    // ── Isolation ────────────────────────────────────────────────────────────────

    [Fact]
    public void ToImmutable_ShouldFreezeTheSnapshot_AgainstEveryLaterKindOfWrite()
    {
        PersistentHashSet<int, Int32IdentityHasher>.Builder builder = NewBuilder();
        for (int item = 0; item < 2_000; item++)
            builder.Add(item);

        PersistentHashSet<int, Int32IdentityHasher> snapshot = builder.ToImmutable();

        // Every write shape the builder has: an insert that splits a slot, an insert into a fresh slot, a
        // removal that collapses a node, and the out-of-band element.
        builder.Add(2_000);
        builder.Add(1 << 20);
        Assert.True(builder.Remove(1_999));
        Assert.True(builder.Remove(0));

        Assert.Equal(2_000, snapshot.Count);
        for (int item = 0; item < 2_000; item++)
            Assert.True(snapshot.Contains(item));

        Assert.False(snapshot.Contains(2_000));
        Assert.False(snapshot.Contains(1 << 20));

        Assert.Equal(2_000, builder.Count);
        Assert.False(builder.Contains(1_999));
        Assert.False(builder.Contains(0));
    }

    [Fact]
    public void EverySnapshotAlongARun_ShouldStaySeparate()
    {
        // Many ToImmutable calls interleaved with writes: each snapshot must keep the set as it stood, which is
        // what fails if the token is refreshed once rather than on every snapshot.
        PersistentHashSet<int, Int32IdentityHasher>.Builder builder = NewBuilder();
        var snapshots = new List<(PersistentHashSet<int, Int32IdentityHasher> Set, int Size)>();

        for (int item = 0; item < 1_500; item++)
        {
            builder.Add(item);
            if (item % 37 == 0)
                snapshots.Add((builder.ToImmutable(), item + 1));
        }

        // Now remove every other element, so any node still shared with a snapshot would visibly lose them.
        for (int item = 0; item < 1_500; item += 2)
            Assert.True(builder.Remove(item));

        foreach ((PersistentHashSet<int, Int32IdentityHasher> set, int size) in snapshots)
        {
            Assert.Equal(size, set.Count);
            for (int item = 0; item < size; item++)
                Assert.True(set.Contains(item));

            Assert.Equal(size, PersistentHashMapShape.AssertCanonical(set));
        }
    }

    [Fact]
    public void ToBuilder_ShouldNotWriteThroughIntoTheSourceSet()
    {
        PersistentHashSet<int, Int32IdentityHasher> source = EmptySet;
        for (int item = 0; item < 1_000; item++)
            source = source.Add(item);

        PersistentHashSet<int, Int32IdentityHasher>.Builder builder = source.ToBuilder();
        for (int item = 1_000; item < 1_300; item++)
            builder.Add(item);

        for (int item = 1; item < 1_000; item += 7)
            builder.Remove(item);

        Assert.Equal(1_000, source.Count);
        for (int item = 0; item < 1_000; item++)
            Assert.True(source.Contains(item));

        Assert.False(source.Contains(1_100));
        Assert.True(builder.Contains(1_100));
    }

    [Fact]
    public void RemovalsThroughABuilder_ShouldCollapseTheTrieTheSameWayPersistentOnesDo()
    {
        PersistentHashSet<int, Int32IdentityHasher> source = EmptySet;
        for (int item = 0; item < 3_000; item++)
            source = source.Add(item);

        PersistentHashSet<int, Int32IdentityHasher>.Builder builder = source.ToBuilder();
        for (int item = 3; item < 3_000; item++)
            Assert.True(builder.Remove(item));

        PersistentHashSet<int, Int32IdentityHasher> drained = builder.ToImmutable();

        Assert.Equal(3, drained.Count);
        Assert.True(drained.Contains(0));
        Assert.True(drained.Contains(1));
        Assert.True(drained.Contains(2));
        Assert.Equal(3, PersistentHashMapShape.AssertCanonical(drained));
        Assert.Equal(3_000, source.Count);
    }

    [Fact]
    public void ABuilderSeededFromACollisionHeavySet_ShouldStayIsolated()
    {
        // Every element hashes to 0, so the whole set is one collision node under seven single-child nodes
        // whose element arrays are empty — the fork path that clones nothing on its way down.
        PersistentHashSet<int, ConstantIntHasher> source = PersistentHashSet<int, ConstantIntHasher>.Empty;
        for (int item = 1; item <= 40; item++)
            source = source.Add(item);

        PersistentHashSet<int, ConstantIntHasher>.Builder builder = source.ToBuilder();
        Assert.True(builder.Remove(21));
        Assert.True(builder.Add(41));
        Assert.True(builder.Add(42));
        Assert.False(builder.Add(20));

        Assert.True(source.Contains(21));
        Assert.False(source.Contains(41));
        Assert.Equal(40, source.Count);
        Assert.Equal(41, builder.Count);
        Assert.Equal(41, builder.ToImmutable().Count);
    }

    [Fact]
    public void ABuilderStaysUsableAfterToImmutable_AndTheSetsAgreeWithTheirOwnHistory()
    {
        PersistentHashSet<int, Int32IdentityHasher>.Builder builder = NewBuilder();
        builder.Add(1);

        PersistentHashSet<int, Int32IdentityHasher> first = builder.ToImmutable();
        builder.Add(2);
        PersistentHashSet<int, Int32IdentityHasher> second = builder.ToImmutable();
        builder.Add(3);
        PersistentHashSet<int, Int32IdentityHasher> third = builder.ToImmutable();

        Assert.Equal(1, first.Count);
        Assert.Equal(2, second.Count);
        Assert.Equal(3, third.Count);
        Assert.False(first.Contains(2));
        Assert.False(second.Contains(3));
    }

    [Fact]
    public void ABuilderThatDrainsToNothing_ShouldProduceTheEmptySingleton()
    {
        PersistentHashSet<int, Int32IdentityHasher>.Builder builder = NewBuilder();
        for (int item = 0; item < 200; item++)
            builder.Add(item);

        for (int item = 0; item < 200; item++)
            Assert.True(builder.Remove(item));

        Assert.Same(EmptySet, builder.ToImmutable());
    }
}
