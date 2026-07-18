using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Coverage for <see cref="DisjointSet{T}"/> enumeration: insertion-order iteration over both the struct
/// and interface enumerators, the concurrent-modification guard, <see cref="IEnumerator.Reset"/>, and the
/// snapshot semantics of <see cref="DisjointSet{T}.GetComponents"/>.
/// </summary>
public class DisjointSetEnumerationTests
{
    [Fact]
    public void Enumerator_YieldsElementsInInsertionOrder()
    {
        var ds = new DisjointSet<int>();
        ds.Add(5);
        ds.Add(3);
        ds.Union(3, 9); // adds 9 after 3
        ds.Add(1);

        Assert.Equal(new[] { 5, 3, 9, 1 }, ds.ToList());
    }

    [Fact]
    public void Enumeration_IsUnaffectedByFindPathCompression()
    {
        // Find only rewrites internal parent pointers (path halving); it must not reorder or invalidate
        // enumeration, and it does not bump the structural version.
        var ds = new DisjointSet<int>();
        for (int i = 0; i < 6; i++)
            ds.Add(i);
        for (int i = 1; i < 6; i++)
            ds.Union(0, i);

        var e = ds.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal(0, e.Current);

        // A Find in the middle of enumeration compresses paths but must not throw.
        Assert.Equal(ds.Find(5), ds.Find(3));

        var rest = new List<int> { e.Current };
        while (e.MoveNext())
            rest.Add(e.Current);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, rest);
    }

    [Fact]
    public void NonGenericEnumerator_Iterates()
    {
        var ds = new DisjointSet<int>(new[] { 10, 20, 30 });

        var items = new List<int>();
        IEnumerator e = ((IEnumerable)ds).GetEnumerator();
        while (e.MoveNext())
            items.Add((int)e.Current!);

        Assert.Equal(new[] { 10, 20, 30 }, items);
    }

    [Fact]
    public void Enumerator_Reset_RestartsIteration()
    {
        var ds = new DisjointSet<int>(new[] { 1, 2, 3 });

        IEnumerator<int> e = ((IEnumerable<int>)ds).GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.True(e.MoveNext());
        e.Reset();
        Assert.True(e.MoveNext());
        Assert.Equal(1, e.Current);
    }

    [Fact]
    public void Enumerator_ThrowsWhenAddDuringEnumeration()
    {
        var ds = new DisjointSet<int>(new[] { 1, 2, 3 });

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (int _ in ds)
                ds.Add(4);
        });
    }

    [Fact]
    public void Enumerator_ThrowsWhenUnionMergesDuringEnumeration()
    {
        var ds = new DisjointSet<int>(new[] { 1, 2, 3 });

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (int _ in ds)
                ds.Union(1, 2); // an effective merge bumps the version
        });
    }

    [Fact]
    public void Enumerator_ThrowsWhenClearDuringEnumeration()
    {
        var ds = new DisjointSet<int>(new[] { 1, 2, 3 });

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (int _ in ds)
                ds.Clear();
        });
    }

    [Fact]
    public void GetComponents_IsSnapshot_NotAffectedByLaterUnion()
    {
        var ds = new DisjointSet<int>();
        ds.Union(1, 2);
        ds.Add(3);

        var before = ds.GetComponents();
        Assert.Equal(2, before.Count);

        ds.Union(2, 3); // merge after the snapshot

        Assert.Equal(2, before.Count); // the earlier snapshot is unchanged
        Assert.Single(ds.GetComponents()); // a fresh call reflects the merge
    }
}
