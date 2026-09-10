using System.Reflection;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// The cross-collection contract for <c>CopyTo(T[] array, int arrayIndex)</c>: every public
/// overload in <c>Celerity.Collections</c> validates its arguments the way
/// <see cref="HashSet{T}"/> and <c>Dictionary&lt;,&gt;</c> do, and throws the same exception
/// <i>type</i> for the same mistake.
///
/// <para>
/// The check worth having a shared test for is the middle one, <c>arrayIndex &gt; array.Length</c>.
/// Without it, an index past the end falls through to <c>array.Length - arrayIndex</c>, goes
/// negative, and trips the insufficient-space branch — so the caller gets an
/// <see cref="ArgumentException"/> where the rest of the library gives an
/// <see cref="ArgumentOutOfRangeException"/>. <c>Deque&lt;T&gt;</c> and
/// <c>PersistentVector&lt;T&gt;</c> both shipped that way (#440). Since
/// <see cref="ArgumentOutOfRangeException"/> derives from <see cref="ArgumentException"/> a
/// <c>catch</c> could not see the difference, but an exact-type test can — and xUnit's
/// <c>Assert.Throws&lt;T&gt;</c> is exact, which is what makes the assertions below discriminate.
/// </para>
///
/// <para>
/// <see cref="EveryPublicCopyToOverload_ShouldBeCoveredByThisFile"/> closes the loop: it walks the
/// shipped assembly for declarations of the signature and fails if one is not in
/// <see cref="Covered"/>, so a collection added later cannot quietly opt out of the contract the
/// way the two above did.
/// </para>
/// </summary>
public class CopyToArgumentGuardTests
{
    private enum Colour { Red, Green, Blue, Yellow }

    // ── The contract ──────────────────────────────────────────────────────────

    // `count` is how many elements the collection holds, and must be at least one so the
    // insufficient-space cases below have something to fail on.
    private static void AssertCopyToContract<T>(int count, Action<T[], int> copyTo)
    {
        Assert.True(count >= 1, "The subject must hold at least one element.");

        Assert.Equal("array", Assert.Throws<ArgumentNullException>(() => copyTo(null!, 0)).ParamName);

        ArgumentOutOfRangeException negative =
            Assert.Throws<ArgumentOutOfRangeException>(() => copyTo(new T[count], -1));
        Assert.Equal("arrayIndex", negative.ParamName);
        Assert.Contains("non-negative", negative.Message);

        // One past the end of the array — the check the two divergent collections were missing.
        ArgumentOutOfRangeException pastEnd =
            Assert.Throws<ArgumentOutOfRangeException>(() => copyTo(new T[count], count + 1));
        Assert.Equal("arrayIndex", pastEnd.ParamName);
        Assert.Contains("beyond the end", pastEnd.Message);

        // arrayIndex == array.Length is *in* range — an index one element past the last slot is
        // where an empty copy would legitimately start — so a non-empty collection fails it as
        // insufficient space rather than as a bad index. This is the boundary the missing check
        // moves, so it is pinned on both sides.
        Assert.Equal("array", Assert.Throws<ArgumentException>(() => copyTo(new T[count], count)).ParamName);
        Assert.Equal("array", Assert.Throws<ArgumentException>(() => copyTo(new T[count - 1], 0)).ParamName);

        // And a valid call still copies, so the guards are not simply rejecting everything.
        T[] destination = new T[count + 1];
        copyTo(destination, 1);
    }

    // ── Sets ──────────────────────────────────────────────────────────────────

    [Fact]
    public void CeleritySet_ShouldValidateCopyToArguments()
    {
        CeleritySet<int, Int32WangNaiveHasher> set = new();
        set.Add(1);
        set.Add(2);
        set.Add(3);

        AssertCopyToContract<int>(3, set.CopyTo);
    }

    [Fact]
    public void SwissSet_ShouldValidateCopyToArguments()
    {
        SwissSet<int, Int32WangNaiveHasher> set = new();
        set.Add(1);
        set.Add(2);
        set.Add(3);

        AssertCopyToContract<int>(3, set.CopyTo);
    }

    [Fact]
    public void RobinHoodSet_ShouldValidateCopyToArguments()
    {
        RobinHoodSet<int, Int32WangNaiveHasher> set = new();
        set.Add(1);
        set.Add(2);
        set.Add(3);

        AssertCopyToContract<int>(3, set.CopyTo);
    }

    [Fact]
    public void HashCachingSet_ShouldValidateCopyToArguments()
    {
        HashCachingSet<int, Int32WangNaiveHasher> set = new();
        set.Add(1);
        set.Add(2);
        set.Add(3);

        AssertCopyToContract<int>(3, set.CopyTo);
    }

    [Fact]
    public void IntSet_ShouldValidateCopyToArguments()
    {
        IntSet<Int32WangNaiveHasher> set = new();
        set.Add(1);
        set.Add(2);
        set.Add(3);

        AssertCopyToContract<int>(3, set.CopyTo);
    }

    [Fact]
    public void LongSet_ShouldValidateCopyToArguments()
    {
        LongSet<Int64WangNaiveHasher> set = new();
        set.Add(1L);
        set.Add(2L);
        set.Add(3L);

        AssertCopyToContract<long>(3, set.CopyTo);
    }

    [Fact]
    public void SmallSet_ShouldValidateCopyToArguments()
    {
        SmallSet<int> set = new(new[] { 1, 2, 3 });

        AssertCopyToContract<int>(3, set.CopyTo);
    }

    [Fact]
    public void SparseSet_ShouldValidateCopyToArguments()
    {
        SparseSet set = new(16, new[] { 1, 2, 3 });

        AssertCopyToContract<int>(3, set.CopyTo);
    }

    [Fact]
    public void EnumSet_ShouldValidateCopyToArguments()
    {
        EnumSet<Colour> set = new(new[] { Colour.Red, Colour.Green, Colour.Blue });

        AssertCopyToContract<Colour>(3, set.CopyTo);
    }

    [Fact]
    public void BTreeSet_ShouldValidateCopyToArguments()
    {
        BTreeSet<int> set = new(new[] { 1, 2, 3 });

        AssertCopyToContract<int>(3, set.CopyTo);
    }

    [Fact]
    public void RankedSet_ShouldValidateCopyToArguments()
    {
        RankedSet<int> set = new(new[] { 1, 2, 3 });

        AssertCopyToContract<int>(3, set.CopyTo);
    }

    [Fact]
    public void PooledCeleritySet_ShouldValidateCopyToArguments()
    {
        using PooledCeleritySet<int, Int32WangNaiveHasher> set = new();
        set.Add(1);
        set.Add(2);
        set.Add(3);

        AssertCopyToContract<int>(3, set.CopyTo);
    }

    [Fact]
    public void CompressedIntSet_ShouldValidateCopyToArguments()
    {
        CompressedIntSet set = new(new[] { 1, 2, 3 });

        AssertCopyToContract<int>(3, set.CopyTo);
    }

    // ── Dictionaries, and their key / value views ─────────────────────────────

    [Fact]
    public void CelerityDictionary_ShouldValidateCopyToArguments()
    {
        CelerityDictionary<int, string, Int32WangNaiveHasher> dictionary = new();
        Fill(dictionary.Add);

        AssertCopyToContract<KeyValuePair<int, string?>>(3, dictionary.CopyTo);
        AssertCopyToContract<int>(3, dictionary.Keys.CopyTo);
        AssertCopyToContract<string?>(3, dictionary.Values.CopyTo);
    }

    [Fact]
    public void SwissDictionary_ShouldValidateCopyToArguments()
    {
        SwissDictionary<int, string, Int32WangNaiveHasher> dictionary = new();
        Fill(dictionary.Add);

        AssertCopyToContract<KeyValuePair<int, string?>>(3, dictionary.CopyTo);
        AssertCopyToContract<int>(3, dictionary.Keys.CopyTo);
        AssertCopyToContract<string?>(3, dictionary.Values.CopyTo);
    }

    [Fact]
    public void RobinHoodDictionary_ShouldValidateCopyToArguments()
    {
        RobinHoodDictionary<int, string, Int32WangNaiveHasher> dictionary = new();
        Fill(dictionary.Add);

        AssertCopyToContract<KeyValuePair<int, string?>>(3, dictionary.CopyTo);
        AssertCopyToContract<int>(3, dictionary.Keys.CopyTo);
        AssertCopyToContract<string?>(3, dictionary.Values.CopyTo);
    }

    [Fact]
    public void HashCachingDictionary_ShouldValidateCopyToArguments()
    {
        HashCachingDictionary<int, string, Int32WangNaiveHasher> dictionary = new();
        Fill(dictionary.Add);

        AssertCopyToContract<KeyValuePair<int, string?>>(3, dictionary.CopyTo);
        AssertCopyToContract<int>(3, dictionary.Keys.CopyTo);
        AssertCopyToContract<string?>(3, dictionary.Values.CopyTo);
    }

    [Fact]
    public void SmallDictionary_ShouldValidateCopyToArguments()
    {
        SmallDictionary<int, string> dictionary = new();
        Fill(dictionary.Add);

        AssertCopyToContract<KeyValuePair<int, string?>>(3, dictionary.CopyTo);
        AssertCopyToContract<int>(3, dictionary.Keys.CopyTo);
        AssertCopyToContract<string?>(3, dictionary.Values.CopyTo);
    }

    [Fact]
    public void IntDictionary_ShouldValidateCopyToArguments()
    {
        IntDictionary<string, Int32WangNaiveHasher> dictionary = new();
        Fill(dictionary.Add);

        AssertCopyToContract<KeyValuePair<int, string?>>(3, dictionary.CopyTo);
        AssertCopyToContract<int>(3, dictionary.Keys.CopyTo);
        AssertCopyToContract<string?>(3, dictionary.Values.CopyTo);
    }

    [Fact]
    public void LongDictionary_ShouldValidateCopyToArguments()
    {
        LongDictionary<string, Int64WangNaiveHasher> dictionary = new();
        dictionary.Add(1L, "one");
        dictionary.Add(2L, "two");
        dictionary.Add(3L, "three");

        AssertCopyToContract<KeyValuePair<long, string?>>(3, dictionary.CopyTo);
        AssertCopyToContract<long>(3, dictionary.Keys.CopyTo);
        AssertCopyToContract<string?>(3, dictionary.Values.CopyTo);
    }

    [Fact]
    public void BTreeDictionary_ShouldValidateCopyToArguments()
    {
        BTreeDictionary<int, string> dictionary = new();
        Fill(dictionary.Add);

        AssertCopyToContract<KeyValuePair<int, string?>>(3, dictionary.CopyTo);
        AssertCopyToContract<int>(3, dictionary.Keys.CopyTo);
        AssertCopyToContract<string?>(3, dictionary.Values.CopyTo);
    }

    [Fact]
    public void PooledCelerityDictionary_ShouldValidateCopyToArguments()
    {
        using PooledCelerityDictionary<int, string, Int32WangNaiveHasher> dictionary = new();
        Fill(dictionary.Add);

        AssertCopyToContract<KeyValuePair<int, string?>>(3, dictionary.CopyTo);
        AssertCopyToContract<int>(3, dictionary.Keys.CopyTo);
        AssertCopyToContract<string?>(3, dictionary.Values.CopyTo);
    }

    [Fact]
    public void EnumMap_ShouldValidateCopyToArguments()
    {
        EnumMap<Colour, string> map = new();
        map.Add(Colour.Red, "one");
        map.Add(Colour.Green, "two");
        map.Add(Colour.Blue, "three");

        AssertCopyToContract<KeyValuePair<Colour, string?>>(3, map.CopyTo);
        AssertCopyToContract<Colour>(3, map.Keys.CopyTo);
        AssertCopyToContract<string?>(3, map.Values.CopyTo);
    }

    // ── The two sequences #440 was filed for ──────────────────────────────────

    [Fact]
    public void Deque_ShouldValidateCopyToArguments()
    {
        Deque<int> deque = new(new[] { 1, 2, 3 });

        AssertCopyToContract<int>(3, deque.CopyTo);
    }

    [Fact]
    public void PersistentVector_ShouldValidateCopyToArguments()
    {
        PersistentVector<int> vector = new(new[] { 1, 2, 3 });

        AssertCopyToContract<int>(3, vector.CopyTo);
    }

    // ── The completeness gate ─────────────────────────────────────────────────

    // Every public declaration of `void CopyTo(T[], int)` in the shipped collections assembly,
    // named `Type` or `Type.NestedView`. A new collection lands here as a failure listing the
    // name it is missing, not as silent divergence.
    private static readonly HashSet<string> Covered = new(StringComparer.Ordinal)
    {
        "BTreeSet", "CeleritySet", "CompressedIntSet", "Deque", "EnumSet", "HashCachingSet",
        "IntSet", "LongSet", "PersistentVector", "PooledCeleritySet", "RankedSet", "RobinHoodSet",
        "SmallSet", "SparseSet", "SwissSet",

        "BTreeDictionary", "BTreeDictionary.KeyCollection", "BTreeDictionary.ValueCollection",
        "CelerityDictionary", "CelerityDictionary.KeyCollection", "CelerityDictionary.ValueCollection",
        "EnumMap", "EnumMap.KeyCollection", "EnumMap.ValueCollection",
        "HashCachingDictionary", "HashCachingDictionary.KeyCollection", "HashCachingDictionary.ValueCollection",
        "IntDictionary", "IntDictionary.KeyCollection", "IntDictionary.ValueCollection",
        "LongDictionary", "LongDictionary.KeyCollection", "LongDictionary.ValueCollection",
        "PooledCelerityDictionary", "PooledCelerityDictionary.KeyCollection", "PooledCelerityDictionary.ValueCollection",
        "RobinHoodDictionary", "RobinHoodDictionary.KeyCollection", "RobinHoodDictionary.ValueCollection",
        "SmallDictionary", "SmallDictionary.KeyCollection", "SmallDictionary.ValueCollection",
        "SwissDictionary", "SwissDictionary.KeyCollection", "SwissDictionary.ValueCollection",
    };

    [Fact]
    public void EveryPublicCopyToOverload_ShouldBeCoveredByThisFile()
    {
        HashSet<string> declared = DeclaredCopyToOverloads();

        Assert.Empty(declared.Except(Covered, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Empty(Covered.Except(declared, StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal));
    }

    private static HashSet<string> DeclaredCopyToOverloads()
    {
        HashSet<string> declared = new(StringComparer.Ordinal);

        foreach (Type type in typeof(CeleritySet<,>).Assembly.GetTypes())
        {
            if (!type.IsPublic && !type.IsNestedPublic)
                continue;

            // DeclaredOnly, so the non-generic convenience subclasses (`IntSet : IntSet<...>`)
            // are not counted a second time for a method they only inherit.
            foreach (MethodInfo method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.Name != "CopyTo")
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType.IsArray
                    && parameters[1].ParameterType == typeof(int))
                {
                    declared.Add(DisplayName(type));
                }
            }
        }

        return declared;
    }

    // `CelerityDictionary`3+KeyCollection` reads as `CelerityDictionary.KeyCollection`: the arity
    // suffix carries nothing here, and dropping it keeps the covered list stable if a type ever
    // gains a generic parameter.
    private static string DisplayName(Type type)
    {
        string name = type.Name.Split('`')[0];
        return type.DeclaringType is null ? name : $"{DisplayName(type.DeclaringType)}.{name}";
    }

    private static void Fill(Action<int, string> add)
    {
        add(1, "one");
        add(2, "two");
        add(3, "three");
    }
}
