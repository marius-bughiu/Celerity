using System.Collections;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// The cross-collection contract for <see cref="IDictionary{TKey, TValue}"/> — the mutable BCL
/// dictionary interface, the mirror of the <see cref="ISet{T}"/> / <see cref="IReadOnlySet{T}"/>
/// work on the set family.
///
/// <para>
/// The dictionaries have implemented <see cref="IReadOnlyDictionary{TKey, TValue}"/> since 1.1.0
/// (pinned by <see cref="ReadOnlyDictionaryInterfaceTests"/>), but <see cref="IDictionary{TKey, TValue}"/>
/// does not derive from it and the BCL <c>Dictionary&lt;,&gt;</c> implements both — so an ordinary
/// API taking <c>IDictionary&lt;string, V&gt;</c> was a hard compile error against every Celerity
/// dictionary except <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>.
/// </para>
///
/// <para>
/// Every member is driven <b>through the interface</b> and checked against a
/// <c>Dictionary&lt;,&gt;</c> oracle, so the assertions describe the BCL contract rather than the
/// forwarders that implement it: the throwing duplicate <c>Add</c>, <c>Contains</c> /
/// <c>Remove</c> over a <see cref="KeyValuePair{TKey, TValue}"/> matching on the <i>pair</i> rather
/// than the key alone, <c>CopyTo</c>'s argument guards, and read-only <c>Keys</c> / <c>Values</c>
/// views whose mutators throw <see cref="NotSupportedException"/> exactly as
/// <c>Dictionary&lt;,&gt;.KeyCollection</c> does.
/// </para>
///
/// <para>
/// Nullable annotation note, matching <see cref="ReadOnlyDictionaryInterfaceTests"/>: the
/// dictionaries implement <c>IDictionary&lt;TKey, TValue?&gt;</c>, but <c>TValue?</c> for an
/// unconstrained <c>TValue</c> is <c>TValue</c> itself at the IL level, so the tests use
/// <c>IDictionary&lt;int, string?&gt;</c>.
/// </para>
///
/// <para>
/// <b>Out of scope.</b> <see cref="FrozenCelerityDictionary{TValue, THasher}"/> is immutable by
/// construction, <see cref="LruCache{TKey, TValue, THasher}"/> evicts on insert (so <c>Add</c>
/// could silently drop an unrelated entry, which the interface's contract does not allow) and
/// <see cref="CelerityMultiMap{TKey, TValue, THasher}"/> holds many values per key. All three keep
/// the read-only interface only. <see cref="Trie{TValue}"/> is a genuine one-value-per-key mutable
/// dictionary and could join, but its <c>Keys</c> / <c>Values</c> are lazy
/// <see cref="IEnumerable{T}"/> traversals rather than the counted struct views the rest of the
/// family exposes, so widening them to <see cref="ICollection{T}"/> is a design change rather than
/// a forwarder — left for its own issue.
/// </para>
/// </summary>
public class DictionaryInterfaceTests
{
    // The one scenario the issue is about: a method whose parameter type is the BCL interface.
    // Every call below was a compile error before this change.
    private static int SumCountThroughInterface<TKey, TValue>(IDictionary<TKey, TValue> dictionary)
        where TKey : notnull
        => dictionary.Count;

    // ---------------- the shared contract ----------------

    /// <summary>
    /// Drives one (empty) dictionary through the whole <see cref="IDictionary{TKey, TValue}"/>
    /// surface, mirroring every mutation into a <c>Dictionary&lt;,&gt;</c> oracle.
    /// </summary>
    /// <param name="subject">An empty dictionary, viewed through the interface under test.</param>
    /// <param name="keys">Three distinct keys. <c>keys[0]</c> should be the type's out-of-band
    /// default/zero key where it has one, so the interface members cover that slot too.</param>
    /// <param name="values">Three distinct values.</param>
    /// <param name="absentKey">A key never inserted.</param>
    /// <param name="absentValue">A value never inserted.</param>
    private static void AssertIDictionaryContract<TKey, TValue>(
        IDictionary<TKey, TValue> subject,
        TKey[] keys,
        TValue[] values,
        TKey absentKey,
        TValue absentValue)
        where TKey : notnull
    {
        var oracle = new Dictionary<TKey, TValue>();

        Assert.False(subject.IsReadOnly);
        Assert.Empty(subject);

        // Add(TKey, TValue) keeps the throwing duplicate semantics of Dictionary<,>; TryAdd stays
        // the non-throwing path on the concrete type.
        subject.Add(keys[0], values[0]);
        oracle.Add(keys[0], values[0]);
        Assert.Throws<ArgumentException>(() => subject.Add(keys[0], values[1]));
        Assert.Throws<ArgumentException>(() => oracle.Add(keys[0], values[1]));

        // ICollection<KeyValuePair<,>>.Add is the same insert, spelled as a pair.
        subject.Add(new KeyValuePair<TKey, TValue>(keys[1], values[1]));
        oracle.Add(keys[1], values[1]);

        // The interface indexer, inserting and then overwriting.
        subject[keys[2]] = values[2];
        oracle[keys[2]] = values[2];
        subject[keys[2]] = values[0];
        oracle[keys[2]] = values[0];

        Assert.Equal(oracle.Count, subject.Count);
        Assert.Equal(oracle.Count, SumCountThroughInterface(subject));

        foreach (KeyValuePair<TKey, TValue> entry in oracle)
        {
            Assert.Equal(entry.Value, subject[entry.Key]);
            Assert.True(subject.ContainsKey(entry.Key));
            Assert.True(subject.TryGetValue(entry.Key, out TValue? found));
            Assert.Equal(entry.Value, found);
        }

        Assert.False(subject.ContainsKey(absentKey));
        Assert.False(subject.TryGetValue(absentKey, out _));
        Assert.Throws<KeyNotFoundException>(() => subject[absentKey]);

        // Contains(KVP) matches on the pair: absent key, present key with a stale value, exact hit.
        Assert.False(subject.Contains(new KeyValuePair<TKey, TValue>(absentKey, values[0])));
        Assert.False(subject.Contains(new KeyValuePair<TKey, TValue>(keys[0], values[1])));
        Assert.True(subject.Contains(new KeyValuePair<TKey, TValue>(keys[0], values[0])));

        // Enumeration through both the generic and the non-generic interface.
        AssertSameMultiset(oracle, subject);

        var boxed = new List<KeyValuePair<TKey, TValue>>();
        IEnumerator nonGeneric = ((IEnumerable)subject).GetEnumerator();
        while (nonGeneric.MoveNext())
            boxed.Add((KeyValuePair<TKey, TValue>)nonGeneric.Current!);
        Assert.Equal(oracle.Count, boxed.Count);

        AssertReadOnlyView(subject.Keys, oracle.Keys, keys[0], absentKey);
        AssertReadOnlyView(subject.Values, oracle.Values, values[0], absentValue);

        AssertCopyToContract(subject, oracle.Count);

        // Remove(KVP): absent key, then a present key carrying a stale value — neither may remove
        // anything — then the exact pair.
        Assert.False(subject.Remove(new KeyValuePair<TKey, TValue>(absentKey, values[0])));
        Assert.False(subject.Remove(new KeyValuePair<TKey, TValue>(keys[1], values[0])));
        Assert.Equal(oracle.Count, subject.Count);
        Assert.True(subject.Remove(new KeyValuePair<TKey, TValue>(keys[1], values[1])));
        oracle.Remove(keys[1]);
        Assert.Equal(oracle.Count, subject.Count);
        Assert.False(subject.ContainsKey(keys[1]));

        // Remove(TKey).
        Assert.True(subject.Remove(keys[0]));
        oracle.Remove(keys[0]);
        Assert.False(subject.Remove(absentKey));
        Assert.Equal(oracle.Count, subject.Count);
        AssertSameMultiset(oracle, subject);

        subject.Clear();
        Assert.Empty(subject);
        Assert.False(subject.ContainsKey(keys[2]));
    }

    private static void AssertSameMultiset<TKey, TValue>(
        Dictionary<TKey, TValue> oracle,
        IDictionary<TKey, TValue> subject)
        where TKey : notnull
    {
        var seen = new List<KeyValuePair<TKey, TValue>>();
        foreach (KeyValuePair<TKey, TValue> entry in subject)
            seen.Add(entry);

        Assert.Equal(oracle.Count, seen.Count);
        foreach (KeyValuePair<TKey, TValue> entry in seen)
        {
            Assert.True(oracle.TryGetValue(entry.Key, out TValue? expected));
            Assert.Equal(expected, entry.Value);
        }
    }

    /// <summary>
    /// The <c>Keys</c> / <c>Values</c> views are read-only <see cref="ICollection{T}"/>s: they
    /// report their count, answer <c>Contains</c>, copy out, and throw from every mutator.
    /// </summary>
    private static void AssertReadOnlyView<T>(
        ICollection<T> view,
        IEnumerable<T> expected,
        T present,
        T absent)
    {
        var expectedItems = new List<T>(expected);

        Assert.True(view.IsReadOnly);
        Assert.Equal(expectedItems.Count, view.Count);
        Assert.True(view.Contains(present));
        Assert.False(view.Contains(absent));

        var seen = new List<T>();
        foreach (T item in view)
            seen.Add(item);
        Assert.Equal(expectedItems.Count, seen.Count);
        foreach (T item in seen)
            Assert.Contains(item, expectedItems);

        Assert.Throws<ArgumentNullException>(() => view.CopyTo(null!, 0));
        var buffer = new T[expectedItems.Count + 1];
        Assert.Throws<ArgumentOutOfRangeException>(() => view.CopyTo(buffer, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => view.CopyTo(buffer, buffer.Length + 1));
        Assert.Throws<ArgumentException>(() => view.CopyTo(new T[expectedItems.Count - 1], 0));

        view.CopyTo(buffer, 1);
        for (int i = 1; i < buffer.Length; i++)
            Assert.Contains(buffer[i], expectedItems);

        Assert.Throws<NotSupportedException>(() => view.Add(present));
        Assert.Throws<NotSupportedException>(() => view.Clear());
        Assert.Throws<NotSupportedException>(() => view.Remove(present));

        // Nothing above may have mutated the underlying dictionary.
        Assert.Equal(expectedItems.Count, view.Count);
    }

    private static void AssertCopyToContract<TKey, TValue>(IDictionary<TKey, TValue> subject, int count)
        where TKey : notnull
    {
        Assert.Throws<ArgumentNullException>(() => subject.CopyTo(null!, 0));
        var buffer = new KeyValuePair<TKey, TValue>[count + 1];
        Assert.Throws<ArgumentOutOfRangeException>(() => subject.CopyTo(buffer, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => subject.CopyTo(buffer, buffer.Length + 1));
        Assert.Throws<ArgumentException>(() => subject.CopyTo(new KeyValuePair<TKey, TValue>[count - 1], 0));

        subject.CopyTo(buffer, 1);
        for (int i = 1; i < buffer.Length; i++)
        {
            Assert.True(subject.TryGetValue(buffer[i].Key, out TValue? value));
            Assert.Equal(value, buffer[i].Value);
        }

        // Copying to an exactly sized array at index 0 is the common case and must not throw.
        subject.CopyTo(new KeyValuePair<TKey, TValue>[count], 0);
    }

    private static readonly int[] IntKeys = [0, 1, 2];
    private static readonly long[] LongKeys = [0L, 1L, 2L];
    private static readonly string[] Values = ["alpha", "beta", "gamma"];

    // ---------------- one row per dictionary ----------------

    [Fact]
    public void CelerityDictionary_ShouldHonourIDictionary()
    {
        IDictionary<int, string?> subject = new CelerityDictionary<int, string, Int32WangNaiveHasher>();

        AssertIDictionaryContract(subject, IntKeys, Values, absentKey: 99, absentValue: "omega");
    }

    [Fact]
    public void SwissDictionary_ShouldHonourIDictionary()
    {
        IDictionary<int, string?> subject = new SwissDictionary<int, string, Int32WangNaiveHasher>();

        AssertIDictionaryContract(subject, IntKeys, Values, absentKey: 99, absentValue: "omega");
    }

    [Fact]
    public void RobinHoodDictionary_ShouldHonourIDictionary()
    {
        IDictionary<int, string?> subject = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();

        AssertIDictionaryContract(subject, IntKeys, Values, absentKey: 99, absentValue: "omega");
    }

    [Fact]
    public void HashCachingDictionary_ShouldHonourIDictionary()
    {
        IDictionary<int, string?> subject = new HashCachingDictionary<int, string, Int32WangNaiveHasher>();

        AssertIDictionaryContract(subject, IntKeys, Values, absentKey: 99, absentValue: "omega");
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldHonourIDictionary()
    {
        using var dictionary = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>();
        IDictionary<int, string?> subject = dictionary;

        AssertIDictionaryContract(subject, IntKeys, Values, absentKey: 99, absentValue: "omega");
    }

    [Fact]
    public void SmallDictionary_ShouldHonourIDictionary()
    {
        IDictionary<int, string?> subject = new SmallDictionary<int, string>();

        AssertIDictionaryContract(subject, IntKeys, Values, absentKey: 99, absentValue: "omega");
    }

    [Fact]
    public void IntDictionary_ShouldHonourIDictionary()
    {
        IDictionary<int, string?> subject = new IntDictionary<string>();

        AssertIDictionaryContract(subject, IntKeys, Values, absentKey: 99, absentValue: "omega");
    }

    [Fact]
    public void LongDictionary_ShouldHonourIDictionary()
    {
        IDictionary<long, string?> subject = new LongDictionary<string>();

        AssertIDictionaryContract(subject, LongKeys, Values, absentKey: 99L, absentValue: "omega");
    }

    [Fact]
    public void EnumMap_ShouldHonourIDictionary()
    {
        IDictionary<EnumSetColor, string?> subject = new EnumMap<EnumSetColor, string>();

        AssertIDictionaryContract(
            subject,
            [EnumSetColor.Red, EnumSetColor.Green, EnumSetColor.Blue],
            Values,
            absentKey: EnumSetColor.Yellow,
            absentValue: "omega");
    }

    // BTreeDictionary has declared IDictionary<,> since it shipped; it joins the suite so the
    // family contract is asserted in one place rather than drifting per type.
    [Fact]
    public void BTreeDictionary_ShouldHonourIDictionary()
    {
        IDictionary<int, string?> subject = new BTreeDictionary<int, string>();

        AssertIDictionaryContract(subject, IntKeys, Values, absentKey: 99, absentValue: "omega");
    }

    // ---------------- string keys, the shape the issue calls out ----------------

    [Fact]
    public void CelerityDictionary_WithStringKeys_ShouldBindToIDictionaryParameter()
    {
        var dictionary = new CelerityDictionary<string, int, StringFnV1AHasher>
        {
            ["one"] = 1,
            ["two"] = 2,
        };

        // The exact call the issue reports as a hard compile error on main.
        Assert.Equal(2, SumCountThroughInterface<string, int>(dictionary));
    }

    // ---------------- type-specific corners ----------------

    [Fact]
    public void EnumMap_InterfaceAdd_ShouldRejectOutOfRangeKey()
    {
        // EnumMap's key universe is bounded, so an out-of-range cast cannot be stored. The
        // interface still reads honestly: the rejection is an ArgumentOutOfRangeException, itself an
        // ArgumentException — the failure IDictionary<,>.Add already documents for a rejected key.
        IDictionary<EnumSetColor, string?> subject = new EnumMap<EnumSetColor, string>();

        var outOfRange = (EnumSetColor)999;

        Assert.Throws<ArgumentOutOfRangeException>(() => subject.Add(outOfRange, "x"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => subject.Add(new KeyValuePair<EnumSetColor, string?>(outOfRange, "x")));
        Assert.Throws<ArgumentOutOfRangeException>(() => subject[outOfRange] = "x");

        // The read surface reports it absent rather than throwing, so Contains / Remove stay usable.
        Assert.False(subject.ContainsKey(outOfRange));
        Assert.False(subject.Contains(new KeyValuePair<EnumSetColor, string?>(outOfRange, "x")));
        Assert.False(subject.Remove(outOfRange));
        Assert.False(subject.Remove(new KeyValuePair<EnumSetColor, string?>(outOfRange, "x")));
    }

    [Fact]
    public void PooledCelerityDictionary_InterfaceMembers_ShouldThrowAfterDispose()
    {
        // Consuming a pooled dictionary as IDictionary<,> loses the dispose obligation from the
        // signature, so the members have to keep reporting the disposed state themselves.
        var dictionary = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>();
        dictionary[1] = "one";
        IDictionary<int, string?> subject = dictionary;
        dictionary.Dispose();

        Assert.Throws<ObjectDisposedException>(() => subject.CopyTo(new KeyValuePair<int, string?>[4], 0));
        Assert.Throws<ObjectDisposedException>(() => subject.Keys);
        Assert.Throws<ObjectDisposedException>(() => subject.Values);
        Assert.Throws<ObjectDisposedException>(() => subject.Count);
    }

    [Fact]
    public void InterfaceIndexerSetter_ShouldNotBumpVersion_OnOverwrite()
    {
        // The family rule pinned by IndexerOverwriteEnumerationTests, reached through the interface
        // indexer's setter: overwriting an existing key changes nothing structural, so a live
        // enumerator survives it.
        var dictionary = new CelerityDictionary<int, string, Int32WangNaiveHasher> { [1] = "one" };
        IDictionary<int, string?> subject = dictionary;

        IEnumerator<KeyValuePair<int, string?>> enumerator = subject.GetEnumerator();
        subject[1] = "uno";

        Assert.True(enumerator.MoveNext());
        Assert.Equal("uno", enumerator.Current.Value);
    }
}
