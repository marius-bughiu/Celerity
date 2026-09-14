using System.Collections;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration of <see cref="PersistentHashSet{T, THasher}"/>: the struct enumerator and the boxed generic
/// and non-generic paths.
///
/// <para>
/// The set is immutable, so there is no version check to test and no way to invalidate an enumerator — which
/// is exactly what makes the <i>other</i> contract worth pinning. The walk is a stack of trie nodes held
/// inline in the struct, so the states a caller can observe are: before the first <c>MoveNext</c>, mid-walk,
/// and after the last — and the last of those must be sticky. An enumerator that reseeds itself at the root
/// when asked for one more element after the end would enumerate the whole set twice.
/// </para>
///
/// <para>
/// The out-of-band <c>default(T)</c> element is yielded first and is the one element that lives outside the
/// trie, so every enumeration path has to splice it in separately. Order beyond that is unspecified, so the
/// assertions here sort.
/// </para>
/// </summary>
public class PersistentHashSetEnumerationTests
{
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    private static PersistentHashSet<int, Int32IdentityHasher> EmptySet =>
        PersistentHashSet<int, Int32IdentityHasher>.Empty;

    [Fact]
    public void EmptySet_ShouldYieldNothing()
    {
        Assert.Empty(EmptySet);

        PersistentHashSet<int, Int32IdentityHasher>.Enumerator enumerator = EmptySet.GetEnumerator();
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Enumeration_ShouldYieldEveryElementExactlyOnce()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = Build(1_000);

        var seen = new List<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Equal(set.Count, seen.Count);
        Assert.Equal(Enumerable.Range(0, 1_000), seen.OrderBy(item => item));
    }

    [Fact]
    public void Enumeration_ShouldYieldTheDefaultElementFirst()
    {
        PersistentHashSet<string, StringFnV1AHasher> set =
            PersistentHashSet<string, StringFnV1AHasher>.Empty.Add("a").Add("b").Add(null!);

        using IEnumerator<string> enumerator = ((IEnumerable<string>)set).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Null(enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.NotNull(enumerator.Current);
    }

    [Fact]
    public void Enumeration_ShouldYieldOnlyTheDefaultElement_WhenTheTrieIsEmpty()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(0);

        Assert.Equal(new[] { 0 }, set.ToArray());
    }

    [Fact]
    public void Enumeration_ShouldWalkACollisionNode()
    {
        PersistentHashSet<int, ConstantIntHasher> set = PersistentHashSet<int, ConstantIntHasher>.Empty;

        for (int item = 1; item <= 30; item++)
            set = set.Add(item);

        Assert.Equal(Enumerable.Range(1, 30), set.OrderBy(item => item));
    }

    [Fact]
    public void Current_ShouldBeDefault_BeforeTheFirstMoveNextAndAfterTheLast()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet.Add(7);

        PersistentHashSet<int, Int32IdentityHasher>.Enumerator enumerator = set.GetEnumerator();
        Assert.Equal(0, enumerator.Current);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(7, enumerator.Current);

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_OnceTheWalkIsOver()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = Build(40);

        PersistentHashSet<int, Int32IdentityHasher>.Enumerator enumerator = set.GetEnumerator();
        int yielded = 0;
        while (enumerator.MoveNext())
            yielded++;

        Assert.Equal(set.Count, yielded);

        // A restart here would silently double every enumeration of the set.
        for (int i = 0; i < 5; i++)
            Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldRestartTheWalk_IncludingTheDefaultElement()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = Build(40);

        PersistentHashSet<int, Int32IdentityHasher>.Enumerator enumerator = set.GetEnumerator();
        while (enumerator.MoveNext())
        {
        }

        enumerator.Reset();
        Assert.Equal(0, enumerator.Current);

        var seen = new List<int>();
        while (enumerator.MoveNext())
            seen.Add(enumerator.Current);

        Assert.Equal(Enumerable.Range(0, 40), seen.OrderBy(item => item));
    }

    [Fact]
    public void Dispose_ShouldBeANoOp()
    {
        PersistentHashSet<int, Int32IdentityHasher>.Enumerator enumerator = Build(4).GetEnumerator();

        enumerator.Dispose();
        enumerator.Dispose();

        Assert.True(enumerator.MoveNext());
    }

    [Fact]
    public void NonGenericEnumeration_ShouldYieldEveryElement()
    {
        PersistentHashSet<int, Int32IdentityHasher> set = Build(50);

        var seen = new List<int>();
        IEnumerator enumerator = ((IEnumerable)set).GetEnumerator();
        while (enumerator.MoveNext())
            seen.Add((int)enumerator.Current!);

        Assert.Equal(Enumerable.Range(0, 50), seen.OrderBy(item => item));
    }

    // Elements 0..count-1, so the out-of-band default element is always present.
    private static PersistentHashSet<int, Int32IdentityHasher> Build(int count)
    {
        PersistentHashSet<int, Int32IdentityHasher> set = EmptySet;
        for (int item = 0; item < count; item++)
            set = set.Add(item);

        return set;
    }
}
