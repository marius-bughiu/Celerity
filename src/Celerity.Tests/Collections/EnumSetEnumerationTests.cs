using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Issue #259: GetEnumerator on EnumSet<TEnum>.
//
// EnumSet walks its bit vector low bit first, so enumeration is deterministic:
// elements come out in ascending underlying-value order (a bonus over the hash-table
// sets). These tests cover that order guarantee, the boxed generic / non-generic
// paths, LINQ, and the modification-during-enumeration conflict detection. The test
// enums (EnumSetColor / EnumSetWide / ...) are defined in EnumSetTests.
public class EnumSetEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var set = new EnumSet<EnumSetColor>();

        var items = new List<EnumSetColor>();
        foreach (EnumSetColor item in set)
            items.Add(item);

        Assert.Empty(items);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldSingleElement()
    {
        var set = new EnumSet<EnumSetColor> { EnumSetColor.Blue };

        var items = new List<EnumSetColor>();
        foreach (EnumSetColor item in set)
            items.Add(item);

        Assert.Equal(EnumSetColor.Blue, Assert.Single(items));
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryElementExactlyOnce()
    {
        var set = EnumSet<EnumSetColor>.All();

        var seen = new HashSet<EnumSetColor>();
        foreach (EnumSetColor item in set)
            Assert.True(seen.Add(item), $"Duplicate element {item} emitted.");

        Assert.Equal(Enum.GetValues<EnumSetColor>().Length, seen.Count);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldInAscendingUnderlyingValueOrder()
    {
        // Insert out of order across three words; enumeration must be ascending by value.
        var set = new EnumSet<EnumSetWide>
        {
            EnumSetWide.Top,        // 130
            EnumSetWide.Zero,       // 0
            EnumSetWide.High,       // 65
            EnumSetWide.WordEdge,   // 63
            EnumSetWide.NextWord,   // 64
        };

        var order = set.ToList();

        Assert.Equal(
            new[]
            {
                EnumSetWide.Zero, EnumSetWide.WordEdge, EnumSetWide.NextWord,
                EnumSetWide.High, EnumSetWide.Top,
            },
            order);
    }

    [Fact]
    public void GetEnumerator_ShouldSpanMultipleWords()
    {
        var set = new EnumSet<EnumSetWide> { EnumSetWide.Zero, EnumSetWide.NextWord, EnumSetWide.Top };

        var items = set.ToList();
        Assert.Equal(3, items.Count);
        Assert.Contains(EnumSetWide.Zero, items);
        Assert.Contains(EnumSetWide.NextWord, items);
        Assert.Contains(EnumSetWide.Top, items);
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenSetModifiedByAdd()
    {
        var set = new EnumSet<EnumSetColor> { EnumSetColor.Red };

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (EnumSetColor item in set)
                set.Add(EnumSetColor.Blue);
        });
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenSetModifiedByRemove()
    {
        var set = new EnumSet<EnumSetColor> { EnumSetColor.Red, EnumSetColor.Blue };

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (EnumSetColor item in set)
                set.Remove(EnumSetColor.Blue);
        });
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenSetCleared()
    {
        var set = new EnumSet<EnumSetColor> { EnumSetColor.Red, EnumSetColor.Blue };

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (EnumSetColor item in set)
                set.Clear();
        });
    }

    // The EnumSet-to-EnumSet fast path of the four bulk operations bumped the version unconditionally, so a
    // bulk operation that changed nothing broke a running enumeration — unlike the type's own no-op Add /
    // Remove / Clear, its IEnumerable fallback path, and HashSet<T>.
    public static TheoryData<string> BulkOperations => new() { "Union", "Intersect", "Except", "SymmetricExcept" };

    private static void ApplyBulk(EnumSet<EnumSetWide> set, string operation, EnumSet<EnumSetWide> other)
    {
        switch (operation)
        {
            case "Union": set.UnionWith(other); break;
            case "Intersect": set.IntersectWith(other); break;
            case "Except": set.ExceptWith(other); break;
            default: set.SymmetricExceptWith(other); break;
        }
    }

    [Theory]
    [MemberData(nameof(BulkOperations))]
    public void Enumerator_ShouldSurvive_WhenABulkOperationWithAnotherEnumSetChangesNothing(string operation)
    {
        var set = new EnumSet<EnumSetWide> { EnumSetWide.One, EnumSetWide.NextWord, EnumSetWide.Top };
        // Union with a subset, intersect with a superset, and except / symmetric-except with a disjoint-and-
        // empty set each leave every bit where it was.
        EnumSet<EnumSetWide> other = operation switch
        {
            "Union" => new EnumSet<EnumSetWide> { EnumSetWide.NextWord },
            "Intersect" => new EnumSet<EnumSetWide>
            {
                EnumSetWide.One, EnumSetWide.WordEdge, EnumSetWide.NextWord, EnumSetWide.Top,
            },
            _ => new EnumSet<EnumSetWide>(),
        };

        var seen = new List<EnumSetWide>();
        foreach (EnumSetWide item in set)
        {
            ApplyBulk(set, operation, other);
            seen.Add(item);
        }

        Assert.Equal(new[] { EnumSetWide.One, EnumSetWide.NextWord, EnumSetWide.Top }, seen);
        Assert.Equal(3, set.Count);
    }

    [Fact]
    public void Enumerator_ShouldSurvive_WhenASetIsUnionedOrIntersectedWithItself()
    {
        var set = new EnumSet<EnumSetColor> { EnumSetColor.Red, EnumSetColor.Blue };

        int visited = 0;
        foreach (EnumSetColor item in set)
        {
            set.UnionWith(set);
            set.IntersectWith(set);
            visited++;
        }

        Assert.Equal(2, visited);
    }

    [Theory]
    [MemberData(nameof(BulkOperations))]
    public void Enumerator_ShouldThrow_WhenABulkOperationWithAnotherEnumSetChangesTheSet(string operation)
    {
        var set = new EnumSet<EnumSetWide> { EnumSetWide.One, EnumSetWide.NextWord };
        EnumSet<EnumSetWide> other = operation switch
        {
            "Union" => new EnumSet<EnumSetWide> { EnumSetWide.Top },
            _ => new EnumSet<EnumSetWide> { EnumSetWide.NextWord },
        };

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (EnumSetWide item in set)
                ApplyBulk(set, operation, other);
        });
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenASymmetricDifferenceSwapsMembersWithoutChangingTheCount()
    {
        // {One} ^ {One, Top} == {Top}: the count stays 1, but the membership changed, so this is a real mutation.
        var set = new EnumSet<EnumSetWide> { EnumSetWide.One };
        var other = new EnumSet<EnumSetWide> { EnumSetWide.One, EnumSetWide.Top };

        EnumSet<EnumSetWide>.Enumerator e = set.GetEnumerator();
        set.SymmetricExceptWith(other);

        Assert.Equal(1, set.Count);
        Assert.True(set.Contains(EnumSetWide.Top));
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void Enumerator_Reset_RestartsEnumeration()
    {
        var set = new EnumSet<EnumSetColor> { EnumSetColor.Red, EnumSetColor.Green };

        EnumSet<EnumSetColor>.Enumerator e = set.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.True(e.MoveNext());
        Assert.False(e.MoveNext());

        e.Reset();
        var afterReset = new List<EnumSetColor>();
        while (e.MoveNext())
            afterReset.Add(e.Current);

        Assert.Equal(new[] { EnumSetColor.Red, EnumSetColor.Green }, afterReset);
    }

    [Fact]
    public void Enumerator_Reset_ShouldThrow_WhenSetModified()
    {
        var set = new EnumSet<EnumSetColor> { EnumSetColor.Red };
        EnumSet<EnumSetColor>.Enumerator e = set.GetEnumerator();
        set.Add(EnumSetColor.Blue);

        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }

    [Fact]
    public void GenericIEnumerator_YieldsAllElements()
    {
        var set = new EnumSet<EnumSetColor> { EnumSetColor.Red, EnumSetColor.Blue };

        IEnumerator<EnumSetColor> e = ((IEnumerable<EnumSetColor>)set).GetEnumerator();
        var items = new List<EnumSetColor>();
        while (e.MoveNext())
            items.Add(e.Current);

        Assert.Equal(new[] { EnumSetColor.Red, EnumSetColor.Blue }, items);
    }

    [Fact]
    public void NonGenericIEnumerator_YieldsAllElements()
    {
        var set = new EnumSet<EnumSetColor> { EnumSetColor.Red, EnumSetColor.Blue };

        IEnumerator e = ((IEnumerable)set).GetEnumerator();
        var items = new List<EnumSetColor>();
        while (e.MoveNext())
            items.Add((EnumSetColor)e.Current!);

        Assert.Equal(new[] { EnumSetColor.Red, EnumSetColor.Blue }, items);
    }

    [Fact]
    public void Linq_OverEnumSet_Works()
    {
        var set = EnumSet<EnumSetColor>.All();

        Assert.Equal(Enum.GetValues<EnumSetColor>().Length, set.Count());
        Assert.Contains(EnumSetColor.Magenta, (IEnumerable<EnumSetColor>)set);
        Assert.Equal(EnumSetColor.Red, set.First());
    }
}
