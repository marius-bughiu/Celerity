using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// <see cref="PersistentHashMap{TKey, TValue, THasher}.Builder"/>: the transient accumulator and, above all,
/// the isolation it owes the maps it hands out.
///
/// <para>
/// <b>Isolation is the thing on trial, not the last answer.</b> The builder's whole reason to exist is that
/// it writes into nodes <i>in place</i> rather than path-copying them, and it is allowed to do that only for
/// nodes no published map can still see. The bookkeeping is an ownership token: a node carries the token of
/// the builder that made it, <c>ToImmutable</c> takes a fresh token, and a node whose token no longer matches
/// is copied instead of written. Get any part of that wrong — forget to refresh the token, or fork a node
/// into the builder's ownership while it still shares a payload array with the node it was forked from — and
/// the current answer is still right. The damage lands on a map handed out <i>earlier</i>, which is why every
/// test here keeps snapshots and re-checks them at the end rather than only asserting on the builder.
/// </para>
/// </summary>
public class PersistentHashMapBuilderTests
{
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    private static PersistentHashMap<int, string, Int32IdentityHasher> EmptyMap =>
        PersistentHashMap<int, string, Int32IdentityHasher>.Empty;

    private static PersistentHashMap<int, string, Int32IdentityHasher>.Builder NewBuilder() => new();

    // ── The basics ───────────────────────────────────────────────────────────────

    [Fact]
    public void NewBuilder_ShouldBeEmpty()
    {
        PersistentHashMap<int, string, Int32IdentityHasher>.Builder builder = NewBuilder();

        Assert.Equal(0, builder.Count);
        Assert.False(builder.ContainsKey(1));
        Assert.False(builder.TryGetValue(1, out string? value));
        Assert.Null(value);
        Assert.Same(EmptyMap, builder.ToImmutable());
    }

    [Fact]
    public void Add_ShouldInsert_AndThrowOnADuplicate()
    {
        PersistentHashMap<int, string, Int32IdentityHasher>.Builder builder = NewBuilder();

        builder.Add(1, "one");

        Assert.Equal(1, builder.Count);
        Assert.Equal("one", builder[1]);

        var ex = Assert.Throws<ArgumentException>(() => builder.Add(1, "uno"));
        Assert.Equal("key", ex.ParamName);
    }

    [Fact]
    public void Add_ShouldLeaveTheBuilderUntouched_WhenItRejectsADuplicate()
    {
        // Regression: Add used to route through the insert-*or-overwrite* path and throw afterwards, so a
        // caller who caught the duplicate-key exception was left holding a builder whose value had already
        // been replaced. Dictionary<,>.Add changes nothing when it throws, and neither does this.
        PersistentHashMap<int, string, Int32IdentityHasher>.Builder builder = NewBuilder();
        builder.Add(1, "one");
        builder.Add(0, "zero");

        Assert.Throws<ArgumentException>(() => builder.Add(1, "uno"));
        Assert.Throws<ArgumentException>(() => builder.Add(0, "naught"));

        Assert.Equal("one", builder[1]);
        Assert.Equal("zero", builder[0]);
        Assert.Equal(2, builder.Count);
        Assert.Equal(2, builder.ToImmutable().Count);
    }

    [Fact]
    public void Add_ShouldLeaveACollisionNodeUntouched_WhenItRejectsADuplicate()
    {
        var builder = new PersistentHashMap<int, string, ConstantIntHasher>.Builder();
        for (int key = 1; key <= 20; key++)
            builder.Add(key, key.ToString());

        Assert.Throws<ArgumentException>(() => builder.Add(7, "seven!"));

        Assert.Equal("7", builder[7]);
        Assert.Equal(20, builder.Count);
    }

    [Fact]
    public void Indexer_ShouldInsertAndOverwrite()
    {
        PersistentHashMap<int, string, Int32IdentityHasher>.Builder builder = NewBuilder();

        builder[1] = "one";
        builder[1] = "uno";

        Assert.Equal(1, builder.Count);
        Assert.Equal("uno", builder[1]);
    }

    [Fact]
    public void Indexer_ShouldThrow_WhenTheKeyIsAbsent()
    {
        Assert.Throws<KeyNotFoundException>(() => NewBuilder()[42]);
    }

    [Fact]
    public void Remove_ShouldReportWhetherTheKeyWasThere()
    {
        PersistentHashMap<int, string, Int32IdentityHasher>.Builder builder = NewBuilder();
        builder.Add(1, "one");

        Assert.False(builder.Remove(2));
        Assert.True(builder.Remove(1));
        Assert.Equal(0, builder.Count);
        Assert.Same(EmptyMap, builder.ToImmutable());
    }

    [Fact]
    public void TheDefaultKey_ShouldBehaveLikeAnyOtherKey()
    {
        PersistentHashMap<int, string, Int32IdentityHasher>.Builder builder = NewBuilder();

        Assert.False(builder.Remove(0));
        Assert.False(builder.ContainsKey(0));
        Assert.False(builder.TryGetValue(0, out string? missing));
        Assert.Null(missing);

        builder.Add(0, "zero");
        Assert.Equal(1, builder.Count);
        Assert.True(builder.ContainsKey(0));
        Assert.Equal("zero", builder[0]);

        builder[0] = "zero";
        Assert.Equal("zero", builder[0]);

        builder[0] = "nil";
        Assert.Equal("nil", builder[0]);
        Assert.Equal(1, builder.Count);

        Assert.Throws<ArgumentException>(() => builder.Add(0, "naught"));

        Assert.True(builder.Remove(0));
        Assert.Equal(0, builder.Count);
    }

    [Fact]
    public void OverwritingWithAnEqualValue_ShouldNotChangeTheCount()
    {
        PersistentHashMap<int, string, Int32IdentityHasher>.Builder builder = NewBuilder();
        builder.Add(1, "one");

        builder[1] = "one";

        Assert.Equal(1, builder.Count);
        Assert.Equal("one", builder[1]);
    }

    // ── Isolation ────────────────────────────────────────────────────────────────

    [Fact]
    public void ToImmutable_ShouldFreezeTheSnapshot_AgainstEveryLaterKindOfWrite()
    {
        PersistentHashMap<int, string, Int32IdentityHasher>.Builder builder = NewBuilder();
        for (int key = 0; key < 2_000; key++)
            builder.Add(key, key.ToString());

        PersistentHashMap<int, string, Int32IdentityHasher> snapshot = builder.ToImmutable();

        // Every write shape the builder has: an overwrite in place, an insert that splits a slot, an insert
        // into a fresh slot, a removal that collapses a node, and the out-of-band slot.
        builder[7] = "seven!";
        builder.Add(2_000, "two thousand");
        builder.Add(1 << 20, "far away");
        Assert.True(builder.Remove(1_999));
        builder[0] = "zero!";

        Assert.Equal(2_000, snapshot.Count);
        for (int key = 0; key < 2_000; key++)
            Assert.Equal(key.ToString(), snapshot[key]);

        Assert.False(snapshot.ContainsKey(2_000));
        Assert.False(snapshot.ContainsKey(1 << 20));

        Assert.Equal(2_001, builder.Count);
        Assert.Equal("seven!", builder[7]);
        Assert.Equal("zero!", builder[0]);
        Assert.False(builder.ContainsKey(1_999));
    }

    [Fact]
    public void EverySnapshotAlongARun_ShouldStaySeparate()
    {
        // Many ToImmutable calls interleaved with writes: each snapshot must keep the map as it stood, which
        // is what fails if the token is refreshed once rather than on every snapshot.
        PersistentHashMap<int, string, Int32IdentityHasher>.Builder builder = NewBuilder();
        var snapshots = new List<(PersistentHashMap<int, string, Int32IdentityHasher> Map, int Size)>();

        for (int key = 0; key < 1_500; key++)
        {
            builder.Add(key, key.ToString());
            if (key % 37 == 0)
                snapshots.Add((builder.ToImmutable(), key + 1));
        }

        // Now rewrite every value, so any node still shared with a snapshot would be visibly corrupted.
        for (int key = 0; key < 1_500; key++)
            builder[key] = $"rewritten-{key}";

        foreach ((PersistentHashMap<int, string, Int32IdentityHasher> map, int size) in snapshots)
        {
            Assert.Equal(size, map.Count);
            for (int key = 0; key < size; key++)
                Assert.Equal(key.ToString(), map[key]);
        }
    }

    [Fact]
    public void ToBuilder_ShouldNotWriteThroughIntoTheSourceMap()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> source = EmptyMap;
        for (int key = 0; key < 1_000; key++)
            source = source.Add(key, key.ToString());

        PersistentHashMap<int, string, Int32IdentityHasher>.Builder builder = source.ToBuilder();
        for (int key = 0; key < 1_000; key += 3)
            builder[key] = "changed";

        for (int key = 1; key < 1_000; key += 7)
            builder.Remove(key);

        Assert.Equal(1_000, source.Count);
        for (int key = 0; key < 1_000; key++)
            Assert.Equal(key.ToString(), source[key]);

        Assert.Equal("changed", builder[3]);
    }

    [Fact]
    public void RemovalsThroughABuilder_ShouldCollapseTheTrieTheSameWayPersistentOnesDo()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> source = EmptyMap;
        for (int key = 0; key < 3_000; key++)
            source = source.Add(key, key.ToString());

        PersistentHashMap<int, string, Int32IdentityHasher>.Builder builder = source.ToBuilder();
        for (int key = 3; key < 3_000; key++)
            Assert.True(builder.Remove(key));

        PersistentHashMap<int, string, Int32IdentityHasher> drained = builder.ToImmutable();

        Assert.Equal(3, drained.Count);
        Assert.Equal("0", drained[0]);
        Assert.Equal("1", drained[1]);
        Assert.Equal("2", drained[2]);
        Assert.Equal(3_000, source.Count);
    }

    [Fact]
    public void ABuilderSeededFromACollisionHeavyMap_ShouldStayIsolated()
    {
        PersistentHashMap<int, string, ConstantIntHasher> source =
            PersistentHashMap<int, string, ConstantIntHasher>.Empty;
        for (int key = 1; key <= 40; key++)
            source = source.Add(key, key.ToString());

        PersistentHashMap<int, string, ConstantIntHasher>.Builder builder = source.ToBuilder();
        builder[20] = "changed";
        Assert.True(builder.Remove(21));
        builder.Add(41, "forty-one");

        Assert.Equal("20", source[20]);
        Assert.Equal("21", source[21]);
        Assert.Equal(40, source.Count);
        Assert.Equal("changed", builder[20]);
        Assert.Equal(40, builder.Count);
    }

    [Fact]
    public void ABuilderStaysUsableAfterToImmutable_AndTheMapsAgreeWithTheirOwnHistory()
    {
        PersistentHashMap<int, string, Int32IdentityHasher>.Builder builder = NewBuilder();
        builder.Add(1, "one");

        PersistentHashMap<int, string, Int32IdentityHasher> first = builder.ToImmutable();
        builder.Add(2, "two");
        PersistentHashMap<int, string, Int32IdentityHasher> second = builder.ToImmutable();
        builder.Add(3, "three");
        PersistentHashMap<int, string, Int32IdentityHasher> third = builder.ToImmutable();

        Assert.Equal(1, first.Count);
        Assert.Equal(2, second.Count);
        Assert.Equal(3, third.Count);
        Assert.False(first.ContainsKey(2));
        Assert.False(second.ContainsKey(3));
    }

    [Fact]
    public void ABuilderThatDrainsToNothing_ShouldProduceTheEmptySingleton()
    {
        PersistentHashMap<int, string, Int32IdentityHasher>.Builder builder = NewBuilder();
        for (int key = 0; key < 200; key++)
            builder.Add(key, key.ToString());

        for (int key = 0; key < 200; key++)
            Assert.True(builder.Remove(key));

        Assert.Same(EmptyMap, builder.ToImmutable());
    }
}
