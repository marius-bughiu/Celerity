using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Pins the empty-slot test on the open-addressed collections for <b>reference-type</b> keys.
/// </summary>
/// <remarks>
/// <para>
/// A vacant slot is <c>default(TKey)</c>, which for a reference type is <c>null</c>. The probe loops
/// therefore answer "is this slot vacant?" with a plain null test rather than with
/// <c>EqualityComparer&lt;TKey&gt;.Default.Equals(slot, default)</c>, so the check costs nothing under a
/// <c>__Canon</c>-shared instantiation (see <c>EmptySlot</c>).
/// </para>
/// <para>
/// That substitution is only sound because the runtime's default comparers answer a <c>null</c>
/// right-hand side structurally, before consulting the key's own <c>Equals</c>. <see cref="NullGreedyKey"/>
/// is the adversary for exactly that: its <c>Equals</c> claims equality with <c>null</c>. If the empty-slot
/// test ever routed through it, every occupied slot would read as vacant and these tests would fail —
/// lost entries, wrong counts, and lookups that miss keys that are present.
/// </para>
/// <para>
/// These are behaviour-pinning tests, not regression tests for a fixed bug: they pass both before and
/// after the codegen change, which is the point — they are what makes it safe to keep the null test.
/// </para>
/// </remarks>
public class ReferenceKeyProbeTests
{
    // Enough keys to force several resizes and long probe chains, so the empty-slot
    // test is exercised on the insert, lookup, resize and backward-shift paths.
    private const int KEY_COUNT = 200;

    /// <summary>
    /// A key whose <c>Equals</c> deliberately claims equality with <c>null</c> — and therefore with the
    /// vacant-slot sentinel. Hashing and non-<c>null</c> equality are ordinary identity-on-<c>Id</c>, so
    /// the only thing this type can break is an empty-slot test that consults <c>Equals</c>.
    /// </summary>
    private sealed class NullGreedyKey : IEquatable<NullGreedyKey>
    {
        public NullGreedyKey(int id) => Id = id;

        public int Id { get; }

        public bool Equals(NullGreedyKey? other) => other is null || other.Id == Id;

        public override bool Equals(object? obj) => obj is null || (obj is NullGreedyKey other && other.Id == Id);

        public override int GetHashCode() => Id;
    }

    private static NullGreedyKey[] Keys(int count = KEY_COUNT)
    {
        var keys = new NullGreedyKey[count];
        for (int i = 0; i < count; i++)
            keys[i] = new NullGreedyKey(i + 1);
        return keys;
    }

    private static NullGreedyKey Missing => new NullGreedyKey(int.MaxValue);

    // ---------------- The contract the null test rests on ----------------

    [Fact]
    public void DefaultComparer_ShouldAnswerNullStructurally_EvenWhenEqualsClaimsEqualityWithNull()
    {
        // The adversary is live: the key's own Equals really does claim equality with null,
        // on both the IEquatable<T> and the object overload.
        Assert.True(new NullGreedyKey(1).Equals(null));
        Assert.True(new NullGreedyKey(1).Equals((object?)null));

        var key = new NullGreedyKey(1);

        // And yet the runtime's default comparer says false, because it resolves a null
        // right-hand side before it ever consults the key. That is the invariant that makes
        // `slot is null` an exact substitute for `comparer.Equals(slot, default(TKey))` — if it
        // ever stopped holding, every probe loop in the library would be wrong and this fails.
        Assert.False(EqualityComparer<NullGreedyKey>.Default.Equals(key, null));
        Assert.True(EqualityComparer<NullGreedyKey>.Default.Equals(null, null));
    }

    // ---------------- CelerityDictionary ----------------

    [Fact]
    public void CelerityDictionary_ShouldProbeCorrectly_WhenKeyEqualsClaimsEqualityWithNull()
    {
        var map = new CelerityDictionary<NullGreedyKey, int, DefaultHasher<NullGreedyKey>>();
        NullGreedyKey[] keys = Keys();

        foreach (var key in keys)
            map[key] = key.Id;

        Assert.Equal(keys.Length, map.Count);
        foreach (var key in keys)
        {
            Assert.True(map.ContainsKey(key));
            Assert.True(map.TryGetValue(key, out int value));
            Assert.Equal(key.Id, value);
        }

        Assert.False(map.ContainsKey(Missing));
        Assert.False(map.TryGetValue(Missing, out _));

        // Backward-shift deletion walks the cluster with the same empty-slot test.
        for (int i = 0; i < keys.Length; i += 2)
            Assert.True(map.Remove(keys[i]));

        Assert.Equal(keys.Length / 2, map.Count);
        for (int i = 1; i < keys.Length; i += 2)
            Assert.True(map.ContainsKey(keys[i]));
    }

    [Fact]
    public void CelerityDictionary_ShouldStoreNullKeyOutOfBand_WhenKeyIsAReferenceType()
    {
        var map = new CelerityDictionary<NullGreedyKey, int, DefaultHasher<NullGreedyKey>>();
        NullGreedyKey[] keys = Keys(16);

        foreach (var key in keys)
            map[key] = key.Id;

        map[null!] = -1;

        Assert.Equal(keys.Length + 1, map.Count);
        Assert.True(map.ContainsKey(null!));
        Assert.Equal(-1, map[null!]);
        foreach (var key in keys)
            Assert.Equal(key.Id, map[key]);

        Assert.True(map.Remove(null!));
        Assert.False(map.ContainsKey(null!));
        Assert.Equal(keys.Length, map.Count);
    }

    // ---------------- RobinHoodDictionary ----------------

    [Fact]
    public void RobinHoodDictionary_ShouldProbeCorrectly_WhenKeyEqualsClaimsEqualityWithNull()
    {
        var map = new RobinHoodDictionary<NullGreedyKey, int, DefaultHasher<NullGreedyKey>>();
        NullGreedyKey[] keys = Keys();

        foreach (var key in keys)
            map[key] = key.Id;

        Assert.Equal(keys.Length, map.Count);
        foreach (var key in keys)
            Assert.Equal(key.Id, map[key]);

        Assert.False(map.ContainsKey(Missing));

        for (int i = 0; i < keys.Length; i += 2)
            Assert.True(map.Remove(keys[i]));

        Assert.Equal(keys.Length / 2, map.Count);
        for (int i = 1; i < keys.Length; i += 2)
            Assert.True(map.ContainsKey(keys[i]));
    }

    [Fact]
    public void RobinHoodDictionary_ShouldStoreNullKeyOutOfBand_WhenKeyIsAReferenceType()
    {
        var map = new RobinHoodDictionary<NullGreedyKey, int, DefaultHasher<NullGreedyKey>>();
        foreach (var key in Keys(16))
            map[key] = key.Id;

        map[null!] = -1;

        Assert.True(map.ContainsKey(null!));
        Assert.Equal(-1, map[null!]);
        Assert.True(map.Remove(null!));
        Assert.False(map.ContainsKey(null!));
    }

    // ---------------- SwissDictionary ----------------

    [Fact]
    public void SwissDictionary_ShouldProbeCorrectly_WhenKeyEqualsClaimsEqualityWithNull()
    {
        var map = new SwissDictionary<NullGreedyKey, int, DefaultHasher<NullGreedyKey>>();
        NullGreedyKey[] keys = Keys();

        foreach (var key in keys)
            map[key] = key.Id;

        Assert.Equal(keys.Length, map.Count);
        foreach (var key in keys)
            Assert.Equal(key.Id, map[key]);

        Assert.False(map.ContainsKey(Missing));

        for (int i = 0; i < keys.Length; i += 2)
            Assert.True(map.Remove(keys[i]));

        Assert.Equal(keys.Length / 2, map.Count);
        for (int i = 1; i < keys.Length; i += 2)
            Assert.True(map.ContainsKey(keys[i]));
    }

    [Fact]
    public void SwissDictionary_ShouldStoreNullKeyOutOfBand_WhenKeyIsAReferenceType()
    {
        var map = new SwissDictionary<NullGreedyKey, int, DefaultHasher<NullGreedyKey>>();
        foreach (var key in Keys(16))
            map[key] = key.Id;

        map[null!] = -1;

        Assert.True(map.ContainsKey(null!));
        Assert.Equal(-1, map[null!]);
        Assert.True(map.Remove(null!));
        Assert.False(map.ContainsKey(null!));
    }

    // ---------------- HashCachingDictionary ----------------

    [Fact]
    public void HashCachingDictionary_ShouldProbeCorrectly_WhenKeyEqualsClaimsEqualityWithNull()
    {
        var map = new HashCachingDictionary<NullGreedyKey, int, DefaultHasher<NullGreedyKey>>();
        NullGreedyKey[] keys = Keys();

        foreach (var key in keys)
            map[key] = key.Id;

        Assert.Equal(keys.Length, map.Count);
        foreach (var key in keys)
            Assert.Equal(key.Id, map[key]);

        Assert.False(map.ContainsKey(Missing));

        for (int i = 0; i < keys.Length; i += 2)
            Assert.True(map.Remove(keys[i]));

        Assert.Equal(keys.Length / 2, map.Count);
        for (int i = 1; i < keys.Length; i += 2)
            Assert.True(map.ContainsKey(keys[i]));
    }

    [Fact]
    public void HashCachingDictionary_ShouldStoreNullKeyOutOfBand_WhenKeyIsAReferenceType()
    {
        var map = new HashCachingDictionary<NullGreedyKey, int, DefaultHasher<NullGreedyKey>>();
        foreach (var key in Keys(16))
            map[key] = key.Id;

        map[null!] = -1;

        Assert.True(map.ContainsKey(null!));
        Assert.Equal(-1, map[null!]);
        Assert.True(map.Remove(null!));
        Assert.False(map.ContainsKey(null!));
    }

    // ---------------- PooledCelerityDictionary ----------------

    [Fact]
    public void PooledCelerityDictionary_ShouldProbeCorrectly_WhenKeyEqualsClaimsEqualityWithNull()
    {
        using var map = new PooledCelerityDictionary<NullGreedyKey, int, DefaultHasher<NullGreedyKey>>();
        NullGreedyKey[] keys = Keys();

        foreach (var key in keys)
            map[key] = key.Id;

        Assert.Equal(keys.Length, map.Count);
        foreach (var key in keys)
            Assert.Equal(key.Id, map[key]);

        Assert.False(map.ContainsKey(Missing));

        for (int i = 0; i < keys.Length; i += 2)
            Assert.True(map.Remove(keys[i]));

        Assert.Equal(keys.Length / 2, map.Count);
        for (int i = 1; i < keys.Length; i += 2)
            Assert.True(map.ContainsKey(keys[i]));
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldStoreNullKeyOutOfBand_WhenKeyIsAReferenceType()
    {
        using var map = new PooledCelerityDictionary<NullGreedyKey, int, DefaultHasher<NullGreedyKey>>();
        foreach (var key in Keys(16))
            map[key] = key.Id;

        map[null!] = -1;

        Assert.True(map.ContainsKey(null!));
        Assert.Equal(-1, map[null!]);
        Assert.True(map.Remove(null!));
        Assert.False(map.ContainsKey(null!));
    }

    // ---------------- CelerityMultiMap ----------------

    [Fact]
    public void CelerityMultiMap_ShouldProbeCorrectly_WhenKeyEqualsClaimsEqualityWithNull()
    {
        var map = new CelerityMultiMap<NullGreedyKey, int, DefaultHasher<NullGreedyKey>>();
        NullGreedyKey[] keys = Keys();

        foreach (var key in keys)
        {
            map.Add(key, key.Id);
            map.Add(key, -key.Id);
        }

        Assert.Equal(keys.Length, map.Count);
        foreach (var key in keys)
        {
            Assert.True(map.ContainsKey(key));
            Assert.Equal(2, map.CountValues(key));
            Assert.True(map.Contains(key, key.Id));
        }

        Assert.False(map.ContainsKey(Missing));

        for (int i = 0; i < keys.Length; i += 2)
            Assert.True(map.RemoveAll(keys[i]));

        Assert.Equal(keys.Length / 2, map.Count);
        for (int i = 1; i < keys.Length; i += 2)
            Assert.True(map.ContainsKey(keys[i]));
    }

    [Fact]
    public void CelerityMultiMap_ShouldStoreNullKeyOutOfBand_WhenKeyIsAReferenceType()
    {
        var map = new CelerityMultiMap<NullGreedyKey, int, DefaultHasher<NullGreedyKey>>();
        foreach (var key in Keys(16))
            map.Add(key, key.Id);

        map.Add(null!, -1);

        Assert.True(map.ContainsKey(null!));
        Assert.True(map.Contains(null!, -1));
        Assert.True(map.RemoveAll(null!));
        Assert.False(map.ContainsKey(null!));
    }

    // ---------------- CeleritySet ----------------

    [Fact]
    public void CeleritySet_ShouldProbeCorrectly_WhenElementEqualsClaimsEqualityWithNull()
    {
        var set = new CeleritySet<NullGreedyKey, DefaultHasher<NullGreedyKey>>();
        AssertSetProbesCorrectly(set);
    }

    [Fact]
    public void CeleritySet_ShouldStoreNullElementOutOfBand_WhenElementIsAReferenceType()
    {
        var set = new CeleritySet<NullGreedyKey, DefaultHasher<NullGreedyKey>>();
        AssertNullElementRoundTrips(set);
    }

    // ---------------- RobinHoodSet ----------------

    [Fact]
    public void RobinHoodSet_ShouldProbeCorrectly_WhenElementEqualsClaimsEqualityWithNull()
    {
        var set = new RobinHoodSet<NullGreedyKey, DefaultHasher<NullGreedyKey>>();
        AssertSetProbesCorrectly(set);
    }

    [Fact]
    public void RobinHoodSet_ShouldStoreNullElementOutOfBand_WhenElementIsAReferenceType()
    {
        var set = new RobinHoodSet<NullGreedyKey, DefaultHasher<NullGreedyKey>>();
        AssertNullElementRoundTrips(set);
    }

    // ---------------- SwissSet ----------------

    [Fact]
    public void SwissSet_ShouldProbeCorrectly_WhenElementEqualsClaimsEqualityWithNull()
    {
        var set = new SwissSet<NullGreedyKey, DefaultHasher<NullGreedyKey>>();
        AssertSetProbesCorrectly(set);
    }

    [Fact]
    public void SwissSet_ShouldStoreNullElementOutOfBand_WhenElementIsAReferenceType()
    {
        var set = new SwissSet<NullGreedyKey, DefaultHasher<NullGreedyKey>>();
        AssertNullElementRoundTrips(set);
    }

    // ---------------- HashCachingSet ----------------

    [Fact]
    public void HashCachingSet_ShouldProbeCorrectly_WhenElementEqualsClaimsEqualityWithNull()
    {
        var set = new HashCachingSet<NullGreedyKey, DefaultHasher<NullGreedyKey>>();
        AssertSetProbesCorrectly(set);
    }

    [Fact]
    public void HashCachingSet_ShouldStoreNullElementOutOfBand_WhenElementIsAReferenceType()
    {
        var set = new HashCachingSet<NullGreedyKey, DefaultHasher<NullGreedyKey>>();
        AssertNullElementRoundTrips(set);
    }

    // ---------------- PooledCeleritySet ----------------

    [Fact]
    public void PooledCeleritySet_ShouldProbeCorrectly_WhenElementEqualsClaimsEqualityWithNull()
    {
        using var set = new PooledCeleritySet<NullGreedyKey, DefaultHasher<NullGreedyKey>>();
        AssertSetProbesCorrectly(set);
    }

    [Fact]
    public void PooledCeleritySet_ShouldStoreNullElementOutOfBand_WhenElementIsAReferenceType()
    {
        using var set = new PooledCeleritySet<NullGreedyKey, DefaultHasher<NullGreedyKey>>();
        AssertNullElementRoundTrips(set);
    }

    // ---------------- CelerityMultiSet ----------------

    [Fact]
    public void CelerityMultiSet_ShouldProbeCorrectly_WhenElementEqualsClaimsEqualityWithNull()
    {
        var set = new CelerityMultiSet<NullGreedyKey, DefaultHasher<NullGreedyKey>>();
        NullGreedyKey[] keys = Keys();

        foreach (var key in keys)
            set.Add(key, 2);

        Assert.Equal(keys.Length, set.Count);
        Assert.Equal(keys.Length * 2, set.TotalCount);
        foreach (var key in keys)
            Assert.Equal(2, set.GetCount(key));

        Assert.False(set.Contains(Missing));
        Assert.Equal(0, set.GetCount(Missing));

        for (int i = 0; i < keys.Length; i += 2)
            Assert.True(set.RemoveAll(keys[i]));

        Assert.Equal(keys.Length / 2, set.Count);
        for (int i = 1; i < keys.Length; i += 2)
            Assert.True(set.Contains(keys[i]));
    }

    [Fact]
    public void CelerityMultiSet_ShouldStoreNullElementOutOfBand_WhenElementIsAReferenceType()
    {
        var set = new CelerityMultiSet<NullGreedyKey, DefaultHasher<NullGreedyKey>>();
        foreach (var key in Keys(16))
            set.Add(key);

        set.Add(null!, 3);

        Assert.True(set.Contains(null!));
        Assert.Equal(3, set.GetCount(null!));
        Assert.True(set.RemoveAll(null!));
        Assert.False(set.Contains(null!));
    }

    // ---------------- Value-type keys are unaffected ----------------

    [Fact]
    public void CelerityDictionary_ShouldKeepDefaultKeySemantics_WhenKeyIsAValueType()
    {
        // The empty-slot test only changes shape for reference-type keys; a value-type
        // key still compares against default(TKey), and default(int) == 0 remains a
        // legal key stored out-of-band.
        var map = new CelerityDictionary<int, int, Int32WangHasher>();
        for (int i = 0; i < 64; i++)
            map[i] = i;

        Assert.Equal(64, map.Count);
        Assert.True(map.ContainsKey(0));
        Assert.Equal(0, map[0]);
        Assert.True(map.Remove(0));
        Assert.False(map.ContainsKey(0));
        Assert.Equal(63, map.Count);
    }

    // ---------------- Shared assertions ----------------

    private static void AssertSetProbesCorrectly(ISet<NullGreedyKey> set)
    {
        NullGreedyKey[] keys = Keys();

        foreach (var key in keys)
            Assert.True(set.Add(key));

        Assert.Equal(keys.Length, set.Count);
        foreach (var key in keys)
            Assert.Contains(key, set);

        Assert.DoesNotContain(Missing, set);

        for (int i = 0; i < keys.Length; i += 2)
            Assert.True(set.Remove(keys[i]));

        Assert.Equal(keys.Length / 2, set.Count);
        for (int i = 1; i < keys.Length; i += 2)
            Assert.Contains(keys[i], set);
    }

    private static void AssertNullElementRoundTrips(ISet<NullGreedyKey> set)
    {
        foreach (var key in Keys(16))
            Assert.True(set.Add(key));

        Assert.True(set.Add(null!));

        Assert.Equal(17, set.Count);
        Assert.Contains(null!, set);
        Assert.True(set.Remove(null!));
        Assert.DoesNotContain(null!, set);
        Assert.Equal(16, set.Count);
    }
}
