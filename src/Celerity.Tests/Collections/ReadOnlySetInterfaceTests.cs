using System.Collections;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Pins the <see cref="IReadOnlySet{T}"/> surface of the mutable set family
/// (<see cref="CeleritySet{T, THasher}"/>, <see cref="SwissSet{T, THasher}"/>,
/// <see cref="RobinHoodSet{T, THasher}"/>, <see cref="HashCachingSet{T, THasher}"/>,
/// <see cref="PooledCeleritySet{T, THasher}"/>, <see cref="SmallSet{T}"/>,
/// <see cref="IntSet{THasher}"/>, <see cref="LongSet{THasher}"/>, <see cref="SparseSet"/> and
/// <see cref="EnumSet{TEnum}"/>), the read-only mirror of the <see cref="ISet{T}"/> conformance
/// covered by <see cref="SetAlgebraTests"/>.
///
/// <para>
/// <see cref="ISet{T}"/> does not derive from <see cref="IReadOnlySet{T}"/>, so declaring the
/// former buys nothing at a call site typed against the latter — the two have to be declared
/// separately, exactly as BCL <see cref="HashSet{T}"/> does. Every test therefore reaches the
/// set <em>through</em> an <c>IReadOnlySet&lt;T&gt;</c> reference and hands it to
/// <see cref="CountThrough{T}"/>, a method whose parameter is the interface: that call is the
/// scenario the family could not serve before, and it fails to compile if a declaration is
/// dropped.
/// </para>
///
/// <para>
/// Query results are reconciled against a BCL <see cref="HashSet{T}"/> oracle over a table of
/// <c>other</c> shapes — equal, proper superset, proper subset, disjoint, empty, partially
/// overlapping — so each of the six queries is asserted in both its <c>true</c> and its
/// <c>false</c> direction rather than against hardcoded expectations. Rows cover value-type,
/// reference-type and enum elements, and each includes the out-of-band default element
/// (<c>0</c> / <c>null</c>) that these sets store outside their main table.
/// </para>
/// </summary>
public class ReadOnlySetInterfaceTests
{
    // A downstream API whose parameter is IReadOnlySet<T>. Passing a Celerity set to one of
    // these was a hard compile error while the family declared ISet<T> alone.
    private static int CountThrough<T>(IReadOnlySet<T> set) => set.Count;

    // ── CeleritySet ───────────────────────────────────────────────────────────

    [Fact]
    public void CeleritySet_ShouldSatisfyTheReadOnlySetContract_WhenReachedThroughTheInterface()
    {
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
        Fill(set.TryAdd, IntMembers);

        Assert.IsAssignableFrom<IReadOnlySet<int>>(set);
        AssertMatchesOracle(set, IntMembers, IntAbsent);
        AssertEmptyContract<int>(new CeleritySet<int, Int32WangNaiveHasher>(), IntMembers);
    }

    [Fact]
    public void CeleritySet_ShouldSatisfyTheReadOnlySetContract_WhenElementsAreStrings()
    {
        var set = new CeleritySet<string, DefaultHasher<string>>();
        Fill(set.TryAdd, StringMembers);

        Assert.IsAssignableFrom<IReadOnlySet<string>>(set);
        AssertMatchesOracle(set, StringMembers, StringAbsent);
        AssertEmptyContract<string>(new CeleritySet<string, DefaultHasher<string>>(), StringMembers);
    }

    // ── SwissSet ──────────────────────────────────────────────────────────────

    [Fact]
    public void SwissSet_ShouldSatisfyTheReadOnlySetContract_WhenReachedThroughTheInterface()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();
        Fill(set.TryAdd, IntMembers);

        Assert.IsAssignableFrom<IReadOnlySet<int>>(set);
        AssertMatchesOracle(set, IntMembers, IntAbsent);
        AssertEmptyContract<int>(new SwissSet<int, Int32WangNaiveHasher>(), IntMembers);
    }

    // ── RobinHoodSet ──────────────────────────────────────────────────────────

    [Fact]
    public void RobinHoodSet_ShouldSatisfyTheReadOnlySetContract_WhenReachedThroughTheInterface()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
        Fill(set.TryAdd, IntMembers);

        Assert.IsAssignableFrom<IReadOnlySet<int>>(set);
        AssertMatchesOracle(set, IntMembers, IntAbsent);
        AssertEmptyContract<int>(new RobinHoodSet<int, Int32WangNaiveHasher>(), IntMembers);
    }

    // ── HashCachingSet ────────────────────────────────────────────────────────

    [Fact]
    public void HashCachingSet_ShouldSatisfyTheReadOnlySetContract_WhenReachedThroughTheInterface()
    {
        var set = new HashCachingSet<int, Int32WangNaiveHasher>();
        Fill(set.TryAdd, IntMembers);

        Assert.IsAssignableFrom<IReadOnlySet<int>>(set);
        AssertMatchesOracle(set, IntMembers, IntAbsent);
        AssertEmptyContract<int>(new HashCachingSet<int, Int32WangNaiveHasher>(), IntMembers);
    }

    [Fact]
    public void HashCachingSet_ShouldSatisfyTheReadOnlySetContract_WhenElementsAreStrings()
    {
        var set = new HashCachingSet<string, DefaultHasher<string>>();
        Fill(set.TryAdd, StringMembers);

        Assert.IsAssignableFrom<IReadOnlySet<string>>(set);
        AssertMatchesOracle(set, StringMembers, StringAbsent);
        AssertEmptyContract<string>(new HashCachingSet<string, DefaultHasher<string>>(), StringMembers);
    }

    // ── PooledCeleritySet ─────────────────────────────────────────────────────

    [Fact]
    public void PooledCeleritySet_ShouldSatisfyTheReadOnlySetContract_WhenReachedThroughTheInterface()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        Fill(set.TryAdd, IntMembers);

        Assert.IsAssignableFrom<IReadOnlySet<int>>(set);
        AssertMatchesOracle(set, IntMembers, IntAbsent);

        using var empty = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        AssertEmptyContract<int>(empty, IntMembers);
    }

    // ── SmallSet ──────────────────────────────────────────────────────────────

    [Fact]
    public void SmallSet_ShouldSatisfyTheReadOnlySetContract_WhenReachedThroughTheInterface()
    {
        var set = new SmallSet<int>();
        Fill(set.TryAdd, IntMembers);

        Assert.IsAssignableFrom<IReadOnlySet<int>>(set);
        AssertMatchesOracle(set, IntMembers, IntAbsent);
        AssertEmptyContract<int>(new SmallSet<int>(), IntMembers);
    }

    [Fact]
    public void SmallSet_ShouldSatisfyTheReadOnlySetContract_WhenElementsAreStrings()
    {
        var set = new SmallSet<string>();
        Fill(set.TryAdd, StringMembers);

        Assert.IsAssignableFrom<IReadOnlySet<string>>(set);
        AssertMatchesOracle(set, StringMembers, StringAbsent);
        AssertEmptyContract<string>(new SmallSet<string>(), StringMembers);
    }

    // ── IntSet ────────────────────────────────────────────────────────────────

    [Fact]
    public void IntSet_ShouldSatisfyTheReadOnlySetContract_WhenReachedThroughTheInterface()
    {
        var set = new IntSet<Int32WangNaiveHasher>();
        Fill(set.TryAdd, IntMembers);

        Assert.IsAssignableFrom<IReadOnlySet<int>>(set);
        AssertMatchesOracle(set, IntMembers, IntAbsent);
        AssertEmptyContract<int>(new IntSet<Int32WangNaiveHasher>(), IntMembers);
    }

    // ── LongSet ───────────────────────────────────────────────────────────────

    [Fact]
    public void LongSet_ShouldSatisfyTheReadOnlySetContract_WhenReachedThroughTheInterface()
    {
        long[] members = { 0L, 1L, -3L, 40L, 5_000_000_000L };
        var set = new LongSet<Int64WangNaiveHasher>();
        Fill(set.TryAdd, members);

        Assert.IsAssignableFrom<IReadOnlySet<long>>(set);
        AssertMatchesOracle(set, members, 77L);
        AssertEmptyContract<long>(new LongSet<Int64WangNaiveHasher>(), members);
    }

    // ── SparseSet ─────────────────────────────────────────────────────────────

    [Fact]
    public void SparseSet_ShouldSatisfyTheReadOnlySetContract_WhenReachedThroughTheInterface()
    {
        // Universe-bounded, so every element (including the absent probe) must be in range.
        int[] members = { 0, 1, 3, 40, 63 };
        var set = new SparseSet(universe: 64);
        Fill(set.TryAdd, members);

        Assert.IsAssignableFrom<IReadOnlySet<int>>(set);
        AssertMatchesOracle(set, members, 7);
        AssertEmptyContract<int>(new SparseSet(universe: 64), members);
    }

    // ── EnumSet ───────────────────────────────────────────────────────────────

    [Fact]
    public void EnumSet_ShouldSatisfyTheReadOnlySetContract_WhenReachedThroughTheInterface()
    {
        EnumSetColor[] members =
        {
            EnumSetColor.Red, EnumSetColor.Green, EnumSetColor.Blue, EnumSetColor.Cyan,
        };
        var set = new EnumSet<EnumSetColor>();
        Fill(set.TryAdd, members);

        Assert.IsAssignableFrom<IReadOnlySet<EnumSetColor>>(set);
        AssertMatchesOracle(set, members, EnumSetColor.Magenta);
        AssertEmptyContract<EnumSetColor>(new EnumSet<EnumSetColor>(), members);
    }

    // ── Shared fixtures and drivers ───────────────────────────────────────────

    // Includes 0 / null — the element every hashed set stores out-of-band rather than in its
    // main table — so the interface reaches that slot too.
    private static readonly int[] IntMembers = { 0, 1, -3, 40, 999 };
    private const int IntAbsent = 7;

    private static readonly string[] StringMembers = { "alpha", null!, "beta", "gamma" };
    private const string StringAbsent = "delta";

    private static void Fill<T>(Func<T, bool> tryAdd, T[] members)
    {
        foreach (T member in members)
            Assert.True(tryAdd(member));
    }

    private static void AssertMatchesOracle<T>(IReadOnlySet<T> set, T[] members, T absent)
    {
        var oracle = new HashSet<T>(members);
        Assert.Equal(members.Length, oracle.Count); // the fixture must be duplicate-free

        Assert.Equal(oracle.Count, set.Count);
        Assert.Equal(oracle.Count, CountThrough(set));

        foreach (T member in members)
            Assert.True(set.Contains(member));
        Assert.False(set.Contains(absent));

        T[] equal = members;
        T[] properSuperset = members.Concat(new[] { absent }).ToArray();
        T[] properSubset = members.Take(members.Length - 1).ToArray();
        T[] disjoint = { absent };
        T[] empty = Array.Empty<T>();
        T[] overlapping = { members[0], absent };

        foreach (T[] other in new[] { equal, properSuperset, properSubset, disjoint, empty, overlapping })
        {
            Assert.Equal(oracle.IsSubsetOf(other), set.IsSubsetOf(other));
            Assert.Equal(oracle.IsProperSubsetOf(other), set.IsProperSubsetOf(other));
            Assert.Equal(oracle.IsSupersetOf(other), set.IsSupersetOf(other));
            Assert.Equal(oracle.IsProperSupersetOf(other), set.IsProperSupersetOf(other));
            Assert.Equal(oracle.Overlaps(other), set.Overlaps(other));
            Assert.Equal(oracle.SetEquals(other), set.SetEquals(other));
        }

        // The two queries that are false for every `other` a same-shape fixture would produce,
        // spelled out so the table above cannot silently stop covering their true direction.
        Assert.True(set.IsSubsetOf(properSuperset));
        Assert.True(set.IsProperSubsetOf(properSuperset));
        Assert.False(set.SetEquals(properSubset));

        var seen = new List<T>();
        foreach (T item in set)
            seen.Add(item);
        Assert.Equal(oracle.Count, seen.Count);
        Assert.True(oracle.SetEquals(seen));

        var boxed = new List<T>();
        foreach (object? item in (IEnumerable)set)
            boxed.Add((T)item!);
        Assert.Equal(oracle.Count, boxed.Count);
        Assert.True(oracle.SetEquals(boxed));
    }

    private static void AssertEmptyContract<T>(IReadOnlySet<T> set, T[] nonEmpty)
    {
        Assert.Equal(0, set.Count);
        Assert.Equal(0, CountThrough(set));
        Assert.False(set.Contains(nonEmpty[0]));

        Assert.True(set.IsSubsetOf(nonEmpty));
        Assert.True(set.IsProperSubsetOf(nonEmpty));
        Assert.True(set.IsSupersetOf(Array.Empty<T>()));
        Assert.False(set.IsProperSupersetOf(nonEmpty));
        Assert.False(set.Overlaps(nonEmpty));
        Assert.True(set.SetEquals(Array.Empty<T>()));
        Assert.False(set.SetEquals(nonEmpty));

        Assert.Empty(set);
    }
}
