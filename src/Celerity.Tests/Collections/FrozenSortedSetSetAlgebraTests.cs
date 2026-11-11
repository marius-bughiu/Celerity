using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// The six <see cref="IReadOnlySet{T}"/> members of <see cref="FrozenSortedSet{T}"/>, each checked against the
/// answer <see cref="HashSet{T}"/> gives for the same two key sets.
///
/// <para>
/// Four of the six (<c>SetEquals</c>, <c>IsSubsetOf</c>, <c>IsProperSubsetOf</c>, <c>IsProperSupersetOf</c>)
/// reduce <c>other</c> to its sorted distinct keys and take a different path to get there depending on what
/// <c>other</c> is: another <see cref="FrozenSortedSet{T}"/> is used as it stands, anything else is copied,
/// sorted and deduplicated. <c>IsSupersetOf</c> and <c>Overlaps</c> stream <c>other</c> and may stop early. So
/// every pair below is asked through four shapes of <c>other</c> — a frozen set, an array that repeats keys, a
/// lazy sequence and a <see cref="HashSet{T}"/> — since a repeat in <c>other</c> is exactly what a cardinality
/// comparison gets wrong when the dedupe is skipped.
/// </para>
/// </summary>
public class FrozenSortedSetSetAlgebraTests
{
    private static readonly int[][] Shapes =
    [
        [],
        [1],
        [2],
        [1, 2, 3],
        [1, 2, 3, 4],
        [2, 3],
        [5, 6],
        [int.MinValue, 0, int.MaxValue],
        [int.MaxValue],
        [.. Enumerable.Range(0, 300)],
        [.. Enumerable.Range(0, 150).Select(i => i * 2)],
        [.. Enumerable.Range(0, 300).Where(i => i != 137)],
    ];

    public static TheoryData<int, int> Pairs()
    {
        var data = new TheoryData<int, int>();
        for (int a = 0; a < Shapes.Length; a++)
        {
            for (int b = 0; b < Shapes.Length; b++)
                data.Add(a, b);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void SetAlgebra_ShouldMatchHashSet_ForEveryShapeOfOther(int left, int right)
    {
        AssertAlgebraMatchesHashSet(Shapes[left], Shapes[right]);
    }

    [Fact]
    public void SetAlgebra_ShouldThrow_WhenOtherIsNull()
    {
        var set = new FrozenSortedSet<int>([1, 2, 3]);

        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => set.SetEquals(null!)).ParamName);
        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => set.IsSubsetOf(null!)).ParamName);
        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => set.IsProperSubsetOf(null!)).ParamName);
        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => set.IsSupersetOf(null!)).ParamName);
        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => set.IsProperSupersetOf(null!)).ParamName);
        Assert.Equal("other", Assert.Throws<ArgumentNullException>(() => set.Overlaps(null!)).ParamName);
    }

    [Fact]
    public void SetAlgebra_ShouldTreatTwoEmptySetsAsEqualSubsetsThatDoNotOverlap()
    {
        var empty = new FrozenSortedSet<int>([]);

        Assert.True(empty.SetEquals(empty));
        Assert.True(empty.IsSubsetOf([]));
        Assert.False(empty.IsProperSubsetOf(empty));
        Assert.True(empty.IsSupersetOf([]));
        Assert.False(empty.IsProperSupersetOf([]));
        Assert.False(empty.Overlaps(empty));
    }

    [Fact]
    public void SetAlgebra_ShouldAcceptItselfAsOther()
    {
        var set = new FrozenSortedSet<int>(Enumerable.Range(0, 500));

        Assert.True(set.SetEquals(set));
        Assert.True(set.IsSubsetOf(set));
        Assert.False(set.IsProperSubsetOf(set));
        Assert.True(set.IsSupersetOf(set));
        Assert.False(set.IsProperSupersetOf(set));
        Assert.True(set.Overlaps(set));
    }

    [Fact]
    public void SetEquals_ShouldIgnoreRepeatsInOther()
    {
        var set = new FrozenSortedSet<long>([1, 2, 3]);

        Assert.True(set.SetEquals([3, 3, 2, 1, 1, 1]));
        Assert.False(set.IsProperSubsetOf([3, 3, 2, 1, 1, 1]));
        Assert.True(set.IsProperSupersetOf([1, 1, 2, 2]));
        Assert.False(set.IsProperSupersetOf([1, 1, 2, 2, 3, 3]));
    }

    [Fact]
    public void SetAlgebra_ShouldWork_ForAScalarKeyType()
    {
        // Int128 searches each node with the scalar loop, which Contains (and so IsSupersetOf and Overlaps)
        // goes through.
        var set = new FrozenSortedSet<Int128>([Int128.MinValue, 0, Int128.MaxValue]);

        Assert.True(set.IsSupersetOf([Int128.MaxValue, Int128.MinValue]));
        Assert.False(set.IsSupersetOf([Int128.One]));
        Assert.True(set.Overlaps([Int128.One, Int128.MaxValue]));
        Assert.True(set.IsProperSubsetOf(new FrozenSortedSet<Int128>([Int128.MinValue, 0, 1, Int128.MaxValue])));
    }

    /// <summary>
    /// Asks all six members of a set built from <paramref name="left"/> about <paramref name="right"/>, in four
    /// shapes, and checks each answer against <see cref="HashSet{T}"/>.
    /// </summary>
    /// <param name="left">The keys of the set under test.</param>
    /// <param name="right">The keys of <c>other</c>.</param>
    internal static void AssertAlgebraMatchesHashSet(int[] left, int[] right)
    {
        var set = new FrozenSortedSet<int>(left);
        var oracle = new HashSet<int>(left);

        IEnumerable<int>[] others =
        [
            new FrozenSortedSet<int>(right),
            [.. right, .. right],
            right.Select(static x => x),
            new HashSet<int>(right),
        ];

        foreach (IEnumerable<int> other in others)
        {
            Assert.Equal(oracle.SetEquals(right), set.SetEquals(other));
            Assert.Equal(oracle.IsSubsetOf(right), set.IsSubsetOf(other));
            Assert.Equal(oracle.IsProperSubsetOf(right), set.IsProperSubsetOf(other));
            Assert.Equal(oracle.IsSupersetOf(right), set.IsSupersetOf(other));
            Assert.Equal(oracle.IsProperSupersetOf(right), set.IsProperSupersetOf(other));
            Assert.Equal(oracle.Overlaps(right), set.Overlaps(other));
        }
    }
}
