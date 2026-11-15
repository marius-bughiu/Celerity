using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// The transient <see cref="PersistentSortedSet{T, TComparer}.Builder"/>: its own surface, and the ownership
/// rule that makes it safe — a builder edits in place only the nodes it created, copies a node shared with a
/// published set before writing to it, and stops owning everything the moment <c>ToImmutable</c> hands a set
/// out.
///
/// <para>
/// The isolation tests are the point. A builder seeded from a set walks through that set's nodes; a write that
/// edited one in place instead of copying it would leave the builder correct and corrupt the set it came from,
/// and a snapshot taken mid-run would change under later writes. Both sides of every write are therefore
/// checked — and checked with value-type <i>and</i> reference-type elements, since the vacated-slot clearing the
/// in-place path does only runs for the latter.
/// </para>
/// </summary>
public class PersistentSortedSetBuilderTests
{
    private const int LargeCount = 5_000;

    private static PersistentSortedSet<int, DefaultComparer<int>> BuildRange(int count) =>
        new(Enumerable.Range(0, count));

    [Fact]
    public void NewBuilder_ShouldBeEmpty()
    {
        var builder = new PersistentSortedSet<int, DefaultComparer<int>>.Builder();

        Assert.Equal(0, builder.Count);
        Assert.False(builder.Contains(0));
        Assert.Same(PersistentSortedSet<int, DefaultComparer<int>>.Empty, builder.ToImmutable());
    }

    [Fact]
    public void ToBuilder_ShouldBeSeededWithTheSetsElements()
    {
        PersistentSortedSet<int, DefaultComparer<int>>.Builder builder = BuildRange(100).ToBuilder();

        Assert.Equal(100, builder.Count);
        Assert.True(builder.Contains(99));
        Assert.False(builder.Contains(100));
    }

    [Fact]
    public void Add_ShouldReportWhetherTheElementWasNew()
    {
        var builder = new PersistentSortedSet<int, DefaultComparer<int>>.Builder();

        Assert.True(builder.Add(5));
        Assert.False(builder.Add(5));
        Assert.Equal(1, builder.Count);
    }

    [Fact]
    public void Remove_ShouldReportWhetherTheElementWasThere()
    {
        PersistentSortedSet<int, DefaultComparer<int>>.Builder builder = BuildRange(10).ToBuilder();

        Assert.True(builder.Remove(3));
        Assert.False(builder.Remove(3));
        Assert.False(builder.Remove(42));
        Assert.Equal(9, builder.Count);
    }

    [Fact]
    public void ToBuilder_ShouldNotWriteThroughIntoTheSourceSet()
    {
        PersistentSortedSet<int, DefaultComparer<int>> source = BuildRange(LargeCount);
        PersistentSortedSet<int, DefaultComparer<int>>.Builder builder = source.ToBuilder();

        for (int i = 0; i < LargeCount; i += 3)
            builder.Remove(i);
        for (int i = LargeCount; i < LargeCount + 500; i++)
            builder.Add(i);

        Assert.Equal(Enumerable.Range(0, LargeCount), source);
        Assert.Equal(LargeCount, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(source));
    }

    [Fact]
    public void ToBuilder_ShouldNotWriteThroughIntoTheSourceSet_WhenItsFirstWriteIsARebalance()
    {
        // A set of exactly 33 is a root over two minimal leaves of 16 and 17, so removing from the left leaf
        // underflows it at once and the builder's very first write is a merge of two shared nodes.
        PersistentSortedSet<int, DefaultComparer<int>> source = BuildRange(33);
        PersistentSortedSet<int, DefaultComparer<int>>.Builder builder = source.ToBuilder();

        Assert.True(builder.Remove(0));
        Assert.True(builder.Remove(1));

        Assert.Equal(Enumerable.Range(0, 33), source);
        Assert.Equal(Enumerable.Range(2, 31), builder.ToImmutable());
    }

    [Fact]
    public void ToImmutable_ShouldFreezeTheSnapshot_AgainstEveryLaterKindOfWrite()
    {
        PersistentSortedSet<int, DefaultComparer<int>>.Builder builder = BuildRange(LargeCount).ToBuilder();
        PersistentSortedSet<int, DefaultComparer<int>> snapshot = builder.ToImmutable();

        // Inserts that split, removals that borrow and merge, at both ends and in the middle.
        for (int i = -500; i < 0; i++)
            builder.Add(i);
        for (int i = 0; i < LargeCount; i += 2)
            builder.Remove(i);
        builder.Add(LargeCount * 2);

        Assert.Equal(Enumerable.Range(0, LargeCount), snapshot);
        Assert.Equal(LargeCount, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(snapshot));
    }

    [Fact]
    public void EverySnapshotAlongARun_ShouldStaySeparate()
    {
        var builder = new PersistentSortedSet<int, DefaultComparer<int>>.Builder();
        var snapshots = new List<(PersistentSortedSet<int, DefaultComparer<int>> Set, int Count)>();

        for (int i = 0; i < 2_000; i++)
        {
            builder.Add(i);
            if (i % 250 == 0)
                snapshots.Add((builder.ToImmutable(), i + 1));
        }

        foreach ((PersistentSortedSet<int, DefaultComparer<int>> set, int count) in snapshots)
            Assert.Equal(Enumerable.Range(0, count), set);
    }

    [Fact]
    public void ABuilderThatDrainsToNothing_ShouldProduceTheEmptySingleton_AndStayUsable()
    {
        PersistentSortedSet<int, DefaultComparer<int>>.Builder builder = BuildRange(100).ToBuilder();
        for (int i = 0; i < 100; i++)
            builder.Remove(i);

        Assert.Same(PersistentSortedSet<int, DefaultComparer<int>>.Empty, builder.ToImmutable());

        builder.Add(7);
        Assert.Equal([7], builder.ToImmutable());
        Assert.Empty(PersistentSortedSet<int, DefaultComparer<int>>.Empty);
    }

    [Fact]
    public void LargeRuns_WithReferenceTypeElements_ShouldStayValid()
    {
        // Reference-type elements take the in-place path's slot-clearing branch, which value types skip.
        var builder = new PersistentSortedSet<string, DefaultComparer<string>>.Builder();
        string[] items = Enumerable.Range(0, LargeCount).Select(i => i.ToString("D5")).ToArray();

        foreach (string item in items)
            builder.Add(item);
        PersistentSortedSet<string, DefaultComparer<string>> full = builder.ToImmutable();

        for (int i = 0; i < LargeCount; i++)
        {
            if (i % 3 != 0)
                Assert.True(builder.Remove(items[i]));
        }

        PersistentSortedSet<string, DefaultComparer<string>> thinned = builder.ToImmutable();

        Assert.Equal(items, full);
        Assert.Equal(items.Where((_, i) => i % 3 == 0), thinned);
        Assert.Equal(thinned.Count, PersistentSortedMapShape.AssertValid<string, DefaultComparer<string>>(thinned));

        for (int i = 0; i < LargeCount; i += 3)
            builder.Remove(items[i]);

        Assert.Equal(0, builder.Count);
        Assert.Same(PersistentSortedSet<string, DefaultComparer<string>>.Empty, builder.ToImmutable());
    }
}
