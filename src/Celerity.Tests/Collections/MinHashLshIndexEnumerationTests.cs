using System.Collections;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Coverage for enumerating a <see cref="MinHashLshIndex{TKey,T,THasher}"/>'s keys: the struct and
/// interface enumerators, skipping removed slots, the concurrent-modification guard and
/// <see cref="IEnumerator.Reset"/>.
/// </summary>
public class MinHashLshIndexEnumerationTests
{
    private static MinHashSketch<int, Int32Murmur3Hasher> Sketch(int start)
        => new(Enumerable.Range(start, 50));

    private static MinHashLshIndex<int, int, Int32Murmur3Hasher> IndexOf(params int[] keys)
    {
        var index = new MinHashLshIndex<int, int, Int32Murmur3Hasher>(0.5);
        foreach (int key in keys)
            index.Add(key, Sketch(key * 100));
        return index;
    }

    [Fact]
    public void Enumerator_ShouldYieldEveryKeyOnce()
    {
        var index = IndexOf(5, 3, 9, 1);

        Assert.Equal(new[] { 1, 3, 5, 9 }, index.OrderBy(k => k));
    }

    [Fact]
    public void Enumerator_ShouldSkipRemovedKeys_AndYieldKeysAddedIntoTheirSlots()
    {
        var index = IndexOf(1, 2, 3, 4);
        index.Remove(2);
        index.Remove(4);

        Assert.Equal(new[] { 1, 3 }, index.OrderBy(k => k));

        index.Add(7, Sketch(700));
        Assert.Equal(new[] { 1, 3, 7 }, index.OrderBy(k => k));
    }

    [Fact]
    public void Enumerator_ShouldYieldNothing_WhenEmpty()
    {
        var index = IndexOf();
        using var enumerator = index.GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.Empty(index);
    }

    [Fact]
    public void NonGenericEnumerator_ShouldIterate()
    {
        var index = IndexOf(10, 20, 30);

        var keys = new List<int>();
        IEnumerator enumerator = ((IEnumerable)index).GetEnumerator();
        while (enumerator.MoveNext())
            keys.Add((int)enumerator.Current);

        Assert.Equal(new[] { 10, 20, 30 }, keys.OrderBy(k => k));
    }

    [Fact]
    public void Reset_ShouldRestartTheEnumeration()
    {
        var index = IndexOf(1, 2);
        var enumerator = index.GetEnumerator();
        while (enumerator.MoveNext())
        {
        }

        enumerator.Reset();

        var keys = new List<int>();
        while (enumerator.MoveNext())
            keys.Add(enumerator.Current);
        Assert.Equal(new[] { 1, 2 }, keys.OrderBy(k => k));
        Assert.Equal(default, enumerator.Current);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("clear")]
    public void MoveNextAndReset_ShouldThrow_WhenTheIndexChanges(string change)
    {
        var index = IndexOf(1, 2);
        var enumerator = index.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        switch (change)
        {
            case "add": index.Add(3, Sketch(300)); break;
            case "remove": index.Remove(2); break;
            default: index.Clear(); break;
        }

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void FailedMutations_ShouldNotInvalidateEnumerators()
    {
        var index = IndexOf(1, 2);
        var enumerator = index.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(index.TryAdd(1, Sketch(999)));
        Assert.False(index.Remove(42));
        index.Query(Sketch(100));

        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }
}
