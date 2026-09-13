using System.Collections;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration of <see cref="PersistentHashMap{TKey, TValue, THasher}"/>: the struct enumerator, the boxed
/// generic and non-generic paths, and the key / value views.
///
/// <para>
/// The map is immutable, so there is no version check to test and no way to invalidate an enumerator — which
/// is exactly what makes the <i>other</i> contract worth pinning. The walk is a stack of trie nodes held
/// inline in the struct, so the states a caller can observe are: before the first <c>MoveNext</c>, mid-walk,
/// and after the last — and the last of those must be sticky. An enumerator that reseeds itself at the root
/// when asked for one more element after the end would enumerate the whole map twice, which is what a naive
/// "depth below zero means not started" sentinel does.
/// </para>
///
/// <para>
/// The out-of-band <c>default(TKey)</c> entry is yielded first and is the one entry that lives outside the
/// trie, so every enumeration path has to splice it in separately. Order beyond that is unspecified, so the
/// assertions here sort.
/// </para>
/// </summary>
public class PersistentHashMapEnumerationTests
{
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    private static PersistentHashMap<int, string, Int32IdentityHasher> EmptyMap =>
        PersistentHashMap<int, string, Int32IdentityHasher>.Empty;

    [Fact]
    public void EmptyMap_ShouldYieldNothing()
    {
        Assert.Empty(EmptyMap);
        Assert.Empty(EmptyMap.Keys);
        Assert.Empty(EmptyMap.Values);
    }

    [Fact]
    public void Enumeration_ShouldYieldEveryEntryExactlyOnce()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = Build(1_000);

        var seen = new List<KeyValuePair<int, string?>>();
        foreach (KeyValuePair<int, string?> entry in map)
            seen.Add(entry);

        Assert.Equal(map.Count, seen.Count);
        Assert.Equal(map.Count, seen.Select(entry => entry.Key).Distinct().Count());
        Assert.Equal(Enumerable.Range(0, 1_000), seen.Select(entry => entry.Key).OrderBy(key => key));

        foreach (KeyValuePair<int, string?> entry in seen)
            Assert.Equal(entry.Key.ToString(), entry.Value);
    }

    [Fact]
    public void Enumeration_ShouldYieldTheDefaultKeyEntryFirst()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = Build(20);

        using IEnumerator<KeyValuePair<int, string?>> enumerator =
            ((IEnumerable<KeyValuePair<int, string?>>)map).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.Key);
        Assert.Equal("0", enumerator.Current.Value);
    }

    [Fact]
    public void Enumeration_ShouldWalkACollisionNode()
    {
        PersistentHashMap<int, string, ConstantIntHasher> map =
            PersistentHashMap<int, string, ConstantIntHasher>.Empty;

        for (int key = 1; key <= 30; key++)
            map = map.Add(key, key.ToString());

        Assert.Equal(Enumerable.Range(1, 30), map.Select(entry => entry.Key).OrderBy(key => key));
    }

    [Fact]
    public void Current_ShouldBeDefault_BeforeTheFirstMoveNextAndAfterTheLast()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(1, "one");

        PersistentHashMap<int, string, Int32IdentityHasher>.Enumerator enumerator = map.GetEnumerator();
        Assert.Equal(default, enumerator.Current);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current.Key);

        Assert.False(enumerator.MoveNext());
        Assert.Equal(default, enumerator.Current);
    }

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_OnceTheWalkIsOver()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = Build(40);

        PersistentHashMap<int, string, Int32IdentityHasher>.Enumerator enumerator = map.GetEnumerator();
        int yielded = 0;
        while (enumerator.MoveNext())
            yielded++;

        Assert.Equal(map.Count, yielded);

        // A restart here would silently double every enumeration of the map.
        for (int i = 0; i < 5; i++)
            Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldRestartTheWalk_IncludingTheDefaultKeyEntry()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = Build(40);

        PersistentHashMap<int, string, Int32IdentityHasher>.Enumerator enumerator = map.GetEnumerator();
        while (enumerator.MoveNext())
        {
        }

        enumerator.Reset();
        Assert.Equal(default, enumerator.Current);

        var seen = new List<int>();
        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Key);

        Assert.Equal(Enumerable.Range(0, 40), seen.OrderBy(key => key));
    }

    [Fact]
    public void Dispose_ShouldBeANoOp()
    {
        PersistentHashMap<int, string, Int32IdentityHasher>.Enumerator enumerator = Build(4).GetEnumerator();

        enumerator.Dispose();
        enumerator.Dispose();

        Assert.True(enumerator.MoveNext());
    }

    [Fact]
    public void NonGenericEnumeration_ShouldYieldEveryEntry()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = Build(50);

        var seen = new List<int>();
        IEnumerator enumerator = ((IEnumerable)map).GetEnumerator();
        while (enumerator.MoveNext())
            seen.Add(((KeyValuePair<int, string?>)enumerator.Current!).Key);

        Assert.Equal(Enumerable.Range(0, 50), seen.OrderBy(key => key));
    }

    [Fact]
    public void KeyCollection_ShouldYieldEveryKey_ThroughAllThreeEnumeratorPaths()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = Build(60);

        var direct = new List<int>();
        foreach (int key in map.Keys)
            direct.Add(key);

        var boxed = new List<int>();
        using (IEnumerator<int> enumerator = ((IEnumerable<int>)map.Keys).GetEnumerator())
        {
            while (enumerator.MoveNext())
                boxed.Add(enumerator.Current);
        }

        var nonGeneric = new List<int>();
        IEnumerator plain = ((IEnumerable)map.Keys).GetEnumerator();
        while (plain.MoveNext())
            nonGeneric.Add((int)plain.Current!);

        Assert.Equal(Enumerable.Range(0, 60), direct.OrderBy(key => key));
        Assert.Equal(direct.OrderBy(key => key), boxed.OrderBy(key => key));
        Assert.Equal(direct.OrderBy(key => key), nonGeneric.OrderBy(key => key));
        Assert.Equal(map.Count, map.Keys.Count);
    }

    [Fact]
    public void ValueCollection_ShouldYieldEveryValue_ThroughAllThreeEnumeratorPaths()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = Build(60);

        var direct = new List<string?>();
        foreach (string? value in map.Values)
            direct.Add(value);

        var boxed = new List<string?>();
        using (IEnumerator<string?> enumerator = ((IEnumerable<string?>)map.Values).GetEnumerator())
        {
            while (enumerator.MoveNext())
                boxed.Add(enumerator.Current);
        }

        var nonGeneric = new List<string?>();
        IEnumerator plain = ((IEnumerable)map.Values).GetEnumerator();
        while (plain.MoveNext())
            nonGeneric.Add((string?)plain.Current);

        string?[] expected = Enumerable.Range(0, 60).Select(value => (string?)value.ToString()).ToArray();
        Assert.Equal(expected.OrderBy(value => value, StringComparer.Ordinal), direct.OrderBy(v => v, StringComparer.Ordinal));
        Assert.Equal(direct.OrderBy(v => v, StringComparer.Ordinal), boxed.OrderBy(v => v, StringComparer.Ordinal));
        Assert.Equal(direct.OrderBy(v => v, StringComparer.Ordinal), nonGeneric.OrderBy(v => v, StringComparer.Ordinal));
        Assert.Equal(map.Count, map.Values.Count);
    }

    [Fact]
    public void KeyAndValueEnumerators_ShouldSupportResetAndDispose()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = Build(10);

        PersistentHashMap<int, string, Int32IdentityHasher>.KeyCollection.Enumerator keys =
            map.Keys.GetEnumerator();
        Assert.True(keys.MoveNext());
        keys.Reset();
        Assert.True(keys.MoveNext());
        Assert.Equal(0, keys.Current);
        keys.Dispose();

        PersistentHashMap<int, string, Int32IdentityHasher>.ValueCollection.Enumerator values =
            map.Values.GetEnumerator();
        Assert.True(values.MoveNext());
        values.Reset();
        Assert.True(values.MoveNext());
        Assert.Equal("0", values.Current);
        values.Dispose();
    }

    [Fact]
    public void KeyAndValueEnumerators_ShouldExposeCurrentThroughTheNonGenericInterface()
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap.Add(5, "five");

        IEnumerator keys = ((IEnumerable)map.Keys).GetEnumerator();
        Assert.True(keys.MoveNext());
        Assert.Equal(5, keys.Current);

        IEnumerator values = ((IEnumerable)map.Values).GetEnumerator();
        Assert.True(values.MoveNext());
        Assert.Equal("five", values.Current);
    }

    // Keys 0..count-1, so the out-of-band default-key slot is always populated.
    private static PersistentHashMap<int, string, Int32IdentityHasher> Build(int count)
    {
        PersistentHashMap<int, string, Int32IdentityHasher> map = EmptyMap;
        for (int key = 0; key < count; key++)
            map = map.Add(key, key.ToString());

        return map;
    }
}
