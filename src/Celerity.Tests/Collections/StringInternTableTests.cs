using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Dedicated tests for <see cref="StringInternTable"/> / <see cref="StringInternTable{THasher}"/>.
/// </summary>
public class StringInternTableTests
{
    // ── Construction / validation ─────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldStartEmpty_WhenDefault()
    {
        var table = new StringInternTable();
        Assert.Equal(0, table.Count);
        Assert.Empty(table);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_ShouldThrowArgumentOutOfRangeException_WhenCapacityIsNegative(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringInternTable(capacity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringInternTable<StringXxHash3Hasher>(capacity));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(-0.5f)]
    [InlineData(1.5f)]
    public void Constructor_ShouldThrowArgumentOutOfRangeException_WhenLoadFactorIsOutOfRange(float loadFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringInternTable(16, loadFactor));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringInternTable<StringXxHash3Hasher>(16, loadFactor));
    }

    [Fact]
    public void Constructor_ShouldAcceptZeroCapacity_WhenRoundedUp()
    {
        var table = new StringInternTable(0);
        Assert.Same(table.GetOrAdd("a".AsSpan()), table.GetOrAdd("a".AsSpan()));
    }

    // ── The headline behaviour: allocate once, return the same reference ──────

    [Fact]
    public void GetOrAdd_ShouldReturnTheSameReference_WhenTheSameCharactersAreSeenAgain()
    {
        var table = new StringInternTable();

        string first = table.GetOrAdd("token".AsSpan());
        string second = table.GetOrAdd("token".AsSpan());

        Assert.Equal("token", first);
        Assert.Same(first, second);
        Assert.Equal(1, table.Count);
    }

    [Fact]
    public void GetOrAdd_ShouldReturnTheSameReference_WhenSpansComeFromDifferentBuffers()
    {
        var table = new StringInternTable();

        string first = table.GetOrAdd("xx-token-yy".AsSpan(3, 5));
        char[] buffer = "..token..".ToCharArray();
        string second = table.GetOrAdd(buffer.AsSpan(2, 5));

        Assert.Equal("token", first);
        Assert.Same(first, second);
        Assert.Equal(1, table.Count);
    }

    [Fact]
    public void GetOrAdd_ShouldNotAllocateANewString_WhenGivenAStringOnAMiss()
    {
        var table = new StringInternTable();
        string supplied = new string('k', 5);

        Assert.Same(supplied, table.GetOrAdd(supplied));
    }

    [Fact]
    public void GetOrAdd_ShouldReturnTheAlreadyInternedInstance_WhenGivenAnEqualString()
    {
        var table = new StringInternTable();
        string canonical = table.GetOrAdd("token".AsSpan());
        string duplicate = new string("token".ToCharArray());

        Assert.NotSame(canonical, duplicate);
        Assert.Same(canonical, table.GetOrAdd(duplicate));
        Assert.Equal(1, table.Count);
    }

    [Fact]
    public void GetOrAdd_ShouldThrowArgumentNullException_WhenStringIsNull()
    {
        var table = new StringInternTable();
        Assert.Throws<ArgumentNullException>(() => table.GetOrAdd((string)null!));
    }

    [Fact]
    public void GetOrAdd_ShouldTreatTheEmptySpanAsTheEmptyString()
    {
        var table = new StringInternTable();

        string interned = table.GetOrAdd(ReadOnlySpan<char>.Empty);

        Assert.Equal(string.Empty, interned);
        Assert.Equal(1, table.Count);
        Assert.True(table.Contains(string.Empty));
        Assert.Same(interned, table.GetOrAdd(string.Empty));
    }

    [Fact]
    public void GetOrAdd_ShouldKeepEveryDistinctToken_WhenTheTableResizes()
    {
        var table = new StringInternTable(capacity: 2);
        var canonical = new string[400];

        for (int i = 0; i < canonical.Length; i++)
            canonical[i] = table.GetOrAdd($"token-{i}".AsSpan());

        Assert.Equal(canonical.Length, table.Count);

        // Every token still resolves to the instance handed out before the growth.
        for (int i = 0; i < canonical.Length; i++)
            Assert.Same(canonical[i], table.GetOrAdd($"token-{i}".AsSpan()));
    }

    // ── TryGet / Contains ─────────────────────────────────────────────────────

    [Fact]
    public void TryGet_ShouldNotIntern_WhenTheCharactersAreAbsent()
    {
        var table = new StringInternTable();

        Assert.False(table.TryGet("absent".AsSpan(), out string? value));
        Assert.Null(value);
        Assert.Equal(0, table.Count);
    }

    [Fact]
    public void TryGet_ShouldReturnTheInternedInstance_WhenPresent()
    {
        var table = new StringInternTable();
        string canonical = table.GetOrAdd("present".AsSpan());

        Assert.True(table.TryGet("present".AsSpan(), out string? value));
        Assert.Same(canonical, value);
    }

    [Fact]
    public void Contains_ShouldAgreeAcrossTheSpanAndStringOverloads()
    {
        var table = new StringInternTable();
        table.GetOrAdd("present".AsSpan());

        Assert.True(table.Contains("present"));
        Assert.True(table.Contains("present".AsSpan()));
        Assert.False(table.Contains("absent"));
        Assert.False(table.Contains("absent".AsSpan()));
    }

    [Fact]
    public void Contains_ShouldThrowArgumentNullException_WhenStringIsNull()
    {
        var table = new StringInternTable();
        Assert.Throws<ArgumentNullException>(() => table.Contains((string)null!));
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_ShouldDropEveryInternedString()
    {
        var table = new StringInternTable();
        string before = table.GetOrAdd("token".AsSpan());
        table.GetOrAdd("other".AsSpan());

        table.Clear();

        Assert.Equal(0, table.Count);
        Assert.False(table.Contains("token".AsSpan()));

        // A fresh instance is minted after a Clear — the old one is no longer canonical.
        Assert.NotSame(before, table.GetOrAdd("token".AsSpan()));
    }

    [Fact]
    public void Clear_ShouldBeANoOp_WhenAlreadyEmpty()
    {
        var table = new StringInternTable();
        table.Clear();
        Assert.Equal(0, table.Count);
    }

    // ── Enumeration ───────────────────────────────────────────────────────────

    [Fact]
    public void GetEnumerator_ShouldYieldEveryInternedString()
    {
        var table = new StringInternTable();
        string[] tokens = ["a", "bb", "ccc", string.Empty];
        foreach (string token in tokens)
            table.GetOrAdd(token.AsSpan());

        var seen = new List<string>();
        foreach (string s in table)
            seen.Add(s);

        Assert.Equal(tokens.OrderBy(s => s, StringComparer.Ordinal), seen.OrderBy(s => s, StringComparer.Ordinal));
    }

    [Fact]
    public void GetEnumerator_ShouldYieldTheCanonicalInstances()
    {
        var table = new StringInternTable();
        string canonical = table.GetOrAdd("token".AsSpan());

        Assert.Same(canonical, Assert.Single(table));
    }

    [Fact]
    public void MoveNext_ShouldThrowInvalidOperationException_WhenTheTableIsModifiedDuringEnumeration()
    {
        var table = new StringInternTable();
        table.GetOrAdd("a".AsSpan());

        StringInternTable<StringFnV1AFullHasher>.Enumerator enumerator = table.GetEnumerator();
        table.GetOrAdd("b".AsSpan());

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldRestartTheEnumeration()
    {
        var table = new StringInternTable();
        table.GetOrAdd("a".AsSpan());

        StringInternTable<StringFnV1AFullHasher>.Enumerator enumerator = table.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());

        enumerator.Reset();
        Assert.True(enumerator.MoveNext());
        enumerator.Dispose();
    }

    [Fact]
    public void Reset_ShouldThrowInvalidOperationException_WhenTheTableWasModified()
    {
        var table = new StringInternTable();
        table.GetOrAdd("a".AsSpan());

        StringInternTable<StringFnV1AFullHasher>.Enumerator enumerator = table.GetEnumerator();
        table.GetOrAdd("b".AsSpan());

        Assert.Throws<InvalidOperationException>(enumerator.Reset);
    }

    [Fact]
    public void GetEnumerator_ShouldWorkThroughTheGenericInterface()
    {
        var table = new StringInternTable();
        table.GetOrAdd("a".AsSpan());

        IEnumerable<string> asEnumerable = table;
        Assert.Single(asEnumerable);

        System.Collections.IEnumerable nonGeneric = table;
        var seen = new List<object?>();
        foreach (object? item in nonGeneric)
            seen.Add(item);
        Assert.Single(seen);
    }

    [Fact]
    public void Current_ShouldBeNullBeforeTheFirstMoveNext()
    {
        var table = new StringInternTable();
        table.GetOrAdd("a".AsSpan());

        System.Collections.IEnumerator enumerator = ((IEnumerable<string>)table).GetEnumerator();
        Assert.Null(enumerator.Current);
    }

    // ── Hasher parameterization ───────────────────────────────────────────────

    [Fact]
    public void GetOrAdd_ShouldBehaveIdentically_AcrossHashers()
    {
        var weak = new StringInternTable<StringFnV1AHasher>();
        var strong = new StringInternTable<StringXxHash3Hasher>();

        // "A" and "Ł" share a raw code under the low-byte hasher; the table must still keep
        // them distinct, because the resolution is the ordinal span compare, not the hash.
        string[] tokens = ["A", "Ł", "A", "Ł", "日本語"];
        foreach (string token in tokens)
        {
            weak.GetOrAdd(token.AsSpan());
            strong.GetOrAdd(token.AsSpan());
        }

        Assert.Equal(3, weak.Count);
        Assert.Equal(3, strong.Count);
        Assert.NotSame(weak.GetOrAdd("A".AsSpan()), weak.GetOrAdd("Ł".AsSpan()));
    }

    [Fact]
    public void GetOrAdd_ShouldKeepSurrogatePairsDistinct()
    {
        var table = new StringInternTable();

        string a = table.GetOrAdd("\U0001F600".AsSpan());
        string b = table.GetOrAdd("\U0001F601".AsSpan());

        Assert.NotSame(a, b);
        Assert.Equal(2, table.Count);
        Assert.Equal("\U0001F600", a);
    }
}
