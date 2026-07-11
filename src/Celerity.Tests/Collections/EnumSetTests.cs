using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Issue #259: EnumSet<TEnum>, a bit-vector set specialized for enum element types —
// the .NET analogue of Java's EnumSet. Membership is a single bit test; set algebra
// against another EnumSet<TEnum> is word-wise bitwise work.
//
// EnumSet is generic over `TEnum : struct, Enum`, so — unlike the `<int>` hash sets —
// it cannot slot into the int-parameterized cross-collection shared suites
// (SetConstructorValidationTests, SetIEnumerableConstructorTests, TryAddProbeCountTests,
// LoadFactorBoundaryTests, ...): `int` is not an enum, and EnumSet has no hasher,
// load factor, probe chain, or capacity constructor anyway. Its equivalent coverage
// therefore lives here and in EnumSetEnumerationTests / EnumSetAlgebraDifferentialTests,
// exactly as the sketch types keep their coverage out of the hash-set shared suites.

// ── Test enums, shared across the EnumSet* test files ──────────────────────────
public enum EnumSetColor { Red, Green, Blue, Yellow, Cyan, Magenta } // 0..5, one word
public enum EnumSetWide { Zero = 0, One = 1, WordEdge = 63, NextWord = 64, High = 65, Top = 130 } // 3 words
public enum EnumSetByte : byte { A = 0, B = 7, C = 200 }
public enum EnumSetLong : long { L0 = 0, L1 = 5, L2 = 100 }
public enum EnumSetULong : ulong { U0 = 0, U1 = 9, U2 = 300 }
public enum EnumSetNegative { Neg = -1, Zero = 0, Pos = 3 }
public enum EnumSetSparse { Lo = 0, Hi = 70000 }
public enum EnumSetEmpty { }

public class EnumSetTests
{
    [Fact]
    public void Add_ShouldStoreElement()
    {
        var set = new EnumSet<EnumSetColor>();
        set.Add(EnumSetColor.Blue);

        Assert.True(set.Contains(EnumSetColor.Blue));
        Assert.Single(set);
    }

    [Fact]
    public void Add_ShouldThrow_OnDuplicate()
    {
        var set = new EnumSet<EnumSetColor>();
        set.Add(EnumSetColor.Green);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(EnumSetColor.Green));
        Assert.Contains("Green", ex.Message);
        Assert.Single(set);
    }

    [Fact]
    public void TryAdd_ShouldReturnTrueThenFalse()
    {
        var set = new EnumSet<EnumSetColor>();

        Assert.True(set.TryAdd(EnumSetColor.Red));
        Assert.False(set.TryAdd(EnumSetColor.Red));
        Assert.Single(set);
    }

    [Fact]
    public void Contains_ShouldReturnFalse_ForAbsentElement()
    {
        var set = new EnumSet<EnumSetColor>();
        set.Add(EnumSetColor.Red);

        Assert.False(set.Contains(EnumSetColor.Magenta));
    }

    [Fact]
    public void ZeroValuedMember_IsAnOrdinaryElement()
    {
        // Red == 0 (bit 0). Unlike the hash-table sets there is no out-of-band default slot;
        // bit 0 is just a bit.
        var set = new EnumSet<EnumSetColor>();
        Assert.False(set.Contains(EnumSetColor.Red));

        set.Add(EnumSetColor.Red);
        Assert.True(set.Contains(EnumSetColor.Red));
        Assert.Single(set);

        Assert.True(set.Remove(EnumSetColor.Red));
        Assert.False(set.Contains(EnumSetColor.Red));
        Assert.Empty(set);
    }

    [Fact]
    public void Remove_ShouldDeleteElement()
    {
        var set = new EnumSet<EnumSetColor>();
        set.Add(EnumSetColor.Cyan);

        Assert.True(set.Remove(EnumSetColor.Cyan));
        Assert.False(set.Contains(EnumSetColor.Cyan));
        Assert.Empty(set);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_ForAbsentElement()
    {
        var set = new EnumSet<EnumSetColor>();
        set.Add(EnumSetColor.Blue);

        Assert.False(set.Remove(EnumSetColor.Yellow));
        Assert.Single(set);
    }

    [Fact]
    public void Clear_ShouldEmptyTheSet()
    {
        var set = new EnumSet<EnumSetColor> { };
        set.Add(EnumSetColor.Red);
        set.Add(EnumSetColor.Blue);

        set.Clear();

        Assert.Empty(set);
        Assert.False(set.Contains(EnumSetColor.Red));
        Assert.False(set.Contains(EnumSetColor.Blue));
    }

    [Fact]
    public void Clear_OnEmptySet_IsNoOp()
    {
        var set = new EnumSet<EnumSetColor>();
        set.Clear();
        Assert.Empty(set);
    }

    [Fact]
    public void MultiWordEnum_StoresAcrossWords()
    {
        // EnumSetWide spans 3 words (max value 130). Exercise elements in each word.
        var set = new EnumSet<EnumSetWide>
        {
            EnumSetWide.Zero,       // word 0, bit 0
            EnumSetWide.WordEdge,   // word 0, bit 63
            EnumSetWide.NextWord,   // word 1, bit 0
            EnumSetWide.High,       // word 1, bit 1
            EnumSetWide.Top,        // word 2, bit 2
        };

        Assert.Equal(5, set.Count);
        Assert.True(set.Contains(EnumSetWide.Zero));
        Assert.True(set.Contains(EnumSetWide.WordEdge));
        Assert.True(set.Contains(EnumSetWide.NextWord));
        Assert.True(set.Contains(EnumSetWide.High));
        Assert.True(set.Contains(EnumSetWide.Top));
        Assert.False(set.Contains(EnumSetWide.One));

        Assert.True(set.Remove(EnumSetWide.NextWord));
        Assert.False(set.Contains(EnumSetWide.NextWord));
        Assert.Equal(4, set.Count);
    }

    [Fact]
    public void All_ContainsEveryDeclaredConstant()
    {
        var set = EnumSet<EnumSetColor>.All();

        EnumSetColor[] declared = Enum.GetValues<EnumSetColor>();
        Assert.Equal(declared.Length, set.Count);
        foreach (EnumSetColor c in declared)
            Assert.True(set.Contains(c), $"All() missing {c}");
    }

    [Fact]
    public void All_OnSparselyValuedEnum_CountsConstantsNotBits()
    {
        // EnumSetWide declares 6 constants but spans 131 bit positions; All() must set
        // exactly the 6 declared bits, not the whole vector.
        var set = EnumSet<EnumSetWide>.All();
        Assert.Equal(6, set.Count);
        Assert.False(set.Contains((EnumSetWide)2)); // an undefined-but-in-range value
    }

    [Fact]
    public void SourceConstructor_CopiesAndDeduplicates()
    {
        var set = new EnumSet<EnumSetColor>(new[]
        {
            EnumSetColor.Red, EnumSetColor.Blue, EnumSetColor.Red, EnumSetColor.Blue,
        });

        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(EnumSetColor.Red));
        Assert.True(set.Contains(EnumSetColor.Blue));
    }

    [Fact]
    public void SourceConstructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new EnumSet<EnumSetColor>(null!));
    }

    [Fact]
    public void SourceConstructor_FromAnotherEnumSet_CopiesBitVector()
    {
        var source = new EnumSet<EnumSetWide> { EnumSetWide.Zero, EnumSetWide.Top, EnumSetWide.High };
        var copy = new EnumSet<EnumSetWide>(source);

        Assert.Equal(source.Count, copy.Count);
        Assert.True(copy.SetEquals(source));

        // Independence: mutating the copy does not affect the source.
        copy.Add(EnumSetWide.One);
        Assert.False(source.Contains(EnumSetWide.One));
    }

    [Fact]
    public void NegativeEnum_IsUnsupported()
    {
        var ex = Assert.Throws<NotSupportedException>(() => new EnumSet<EnumSetNegative>());
        Assert.Contains("negative", ex.Message);

        Assert.Throws<NotSupportedException>(() => EnumSet<EnumSetNegative>.All());
    }

    [Fact]
    public void SparseEnum_IsUnsupported()
    {
        var ex = Assert.Throws<NotSupportedException>(() => new EnumSet<EnumSetSparse>());
        Assert.Contains("range", ex.Message);
    }

    [Fact]
    public void EmptyEnum_IsSupportedButHoldsNothing()
    {
        var set = new EnumSet<EnumSetEmpty>();
        Assert.Empty(set);

        var all = EnumSet<EnumSetEmpty>.All();
        Assert.Empty(all);
    }

    [Fact]
    public void OutOfRangeValue_AddThrows_ContainsAndRemoveReturnFalse()
    {
        var set = new EnumSet<EnumSetColor>();
        var bad = (EnumSetColor)999;

        Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(bad));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.TryAdd(bad));
        Assert.False(set.Contains(bad));
        Assert.False(set.Remove(bad));
    }

    [Fact]
    public void OutOfRangeValue_InSourceConstructor_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EnumSet<EnumSetColor>(new[] { EnumSetColor.Red, (EnumSetColor)500 }));
    }

    [Theory]
    [MemberData(nameof(ByteEnumCases))]
    public void ByteBackedEnum_RoundTrips(EnumSetByte value)
    {
        var set = new EnumSet<EnumSetByte>();
        set.Add(value);
        Assert.True(set.Contains(value));
        Assert.Single(set);
        Assert.Contains(value, set);
    }

    public static IEnumerable<object[]> ByteEnumCases() =>
        Enum.GetValues<EnumSetByte>().Select(v => new object[] { v });

    [Fact]
    public void LongBackedEnum_RoundTrips()
    {
        var set = new EnumSet<EnumSetLong> { EnumSetLong.L0, EnumSetLong.L1, EnumSetLong.L2 };
        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(EnumSetLong.L2));
        Assert.Equal(new[] { EnumSetLong.L0, EnumSetLong.L1, EnumSetLong.L2 }, set.OrderBy(x => (long)x));
    }

    [Fact]
    public void ULongBackedEnum_RoundTrips()
    {
        var set = new EnumSet<EnumSetULong> { EnumSetULong.U0, EnumSetULong.U1, EnumSetULong.U2 };
        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(EnumSetULong.U2));
        Assert.False(set.Contains((EnumSetULong)1));
    }

    [Fact]
    public void ISetAdd_ReturnsBool()
    {
        ISet<EnumSetColor> set = new EnumSet<EnumSetColor>();
        Assert.True(set.Add(EnumSetColor.Red));
        Assert.False(set.Add(EnumSetColor.Red));
    }

    [Fact]
    public void ICollectionAdd_ToleratesDuplicate()
    {
        ICollection<EnumSetColor> set = new EnumSet<EnumSetColor>();
        set.Add(EnumSetColor.Red);
        set.Add(EnumSetColor.Red); // must not throw
        Assert.Single(set);
        Assert.False(set.IsReadOnly);
    }

    [Fact]
    public void CopyTo_CopiesInAscendingOrder()
    {
        var set = new EnumSet<EnumSetWide> { EnumSetWide.Top, EnumSetWide.Zero, EnumSetWide.NextWord };
        var array = new EnumSetWide[5];

        set.CopyTo(array, 1);

        Assert.Equal(default, array[0]);
        Assert.Equal(EnumSetWide.Zero, array[1]);     // 0
        Assert.Equal(EnumSetWide.NextWord, array[2]); // 64
        Assert.Equal(EnumSetWide.Top, array[3]);      // 130
    }

    [Fact]
    public void CopyTo_ArgumentValidation()
    {
        var set = new EnumSet<EnumSetColor> { EnumSetColor.Red, EnumSetColor.Blue };

        Assert.Throws<ArgumentNullException>(() => set.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.CopyTo(new EnumSetColor[2], -1));
        Assert.Throws<ArgumentException>(() => set.CopyTo(new EnumSetColor[2], 1)); // insufficient space
    }

    // ── Set algebra: EnumSet-vs-EnumSet fast path and vs a general IEnumerable ──

    [Fact]
    public void UnionWith_EnumSetOperand_TakesFastPath()
    {
        var a = new EnumSet<EnumSetWide> { EnumSetWide.Zero, EnumSetWide.NextWord };
        var b = new EnumSet<EnumSetWide> { EnumSetWide.NextWord, EnumSetWide.Top };

        a.UnionWith(b);

        Assert.Equal(3, a.Count);
        Assert.True(a.Contains(EnumSetWide.Zero));
        Assert.True(a.Contains(EnumSetWide.NextWord));
        Assert.True(a.Contains(EnumSetWide.Top));
    }

    [Fact]
    public void IntersectWith_EnumSetOperand_TakesFastPath()
    {
        var a = new EnumSet<EnumSetWide> { EnumSetWide.Zero, EnumSetWide.NextWord, EnumSetWide.Top };
        var b = new EnumSet<EnumSetWide> { EnumSetWide.NextWord, EnumSetWide.Top };

        a.IntersectWith(b);

        Assert.Equal(2, a.Count);
        Assert.False(a.Contains(EnumSetWide.Zero));
    }

    [Fact]
    public void ExceptWith_EnumSetOperand_TakesFastPath()
    {
        var a = new EnumSet<EnumSetWide> { EnumSetWide.Zero, EnumSetWide.NextWord, EnumSetWide.Top };
        var b = new EnumSet<EnumSetWide> { EnumSetWide.NextWord };

        a.ExceptWith(b);

        Assert.Equal(2, a.Count);
        Assert.False(a.Contains(EnumSetWide.NextWord));
    }

    [Fact]
    public void SymmetricExceptWith_EnumSetOperand_TakesFastPath()
    {
        var a = new EnumSet<EnumSetWide> { EnumSetWide.Zero, EnumSetWide.NextWord };
        var b = new EnumSet<EnumSetWide> { EnumSetWide.NextWord, EnumSetWide.Top };

        a.SymmetricExceptWith(b);

        Assert.Equal(2, a.Count);
        Assert.True(a.Contains(EnumSetWide.Zero));
        Assert.True(a.Contains(EnumSetWide.Top));
        Assert.False(a.Contains(EnumSetWide.NextWord));
    }

    [Fact]
    public void SetAlgebra_GeneralEnumerableOperand_Works()
    {
        var a = new EnumSet<EnumSetColor> { EnumSetColor.Red, EnumSetColor.Green, EnumSetColor.Blue };

        // A lazy, non-EnumSet enumerable forces the SetOperations fallback path.
        IEnumerable<EnumSetColor> other = new[] { EnumSetColor.Green, EnumSetColor.Yellow }.Select(x => x);
        a.IntersectWith(other);

        Assert.Single(a);
        Assert.True(a.Contains(EnumSetColor.Green));
    }

    [Fact]
    public void SelfAliasing_ExceptWith_EmptiesSet()
    {
        var a = new EnumSet<EnumSetColor> { EnumSetColor.Red, EnumSetColor.Blue };
        a.ExceptWith(a);
        Assert.Empty(a);
    }

    [Fact]
    public void QueryOperations_EnumSetOperand()
    {
        var sub = new EnumSet<EnumSetWide> { EnumSetWide.Zero, EnumSetWide.Top };
        var super = new EnumSet<EnumSetWide> { EnumSetWide.Zero, EnumSetWide.Top, EnumSetWide.One };

        Assert.True(sub.IsSubsetOf(super));
        Assert.True(sub.IsProperSubsetOf(super));
        Assert.True(super.IsSupersetOf(sub));
        Assert.True(super.IsProperSupersetOf(sub));
        Assert.True(sub.Overlaps(super));
        Assert.False(sub.SetEquals(super));
        Assert.True(sub.SetEquals(new EnumSet<EnumSetWide> { EnumSetWide.Top, EnumSetWide.Zero }));
        Assert.False(sub.IsProperSubsetOf(new EnumSet<EnumSetWide> { EnumSetWide.Zero, EnumSetWide.Top }));
    }

    [Fact]
    public void QueryOperations_GeneralEnumerableOperand()
    {
        var sub = new EnumSet<EnumSetColor> { EnumSetColor.Red, EnumSetColor.Blue };
        IEnumerable<EnumSetColor> super =
            new[] { EnumSetColor.Red, EnumSetColor.Blue, EnumSetColor.Green }.Select(x => x);

        Assert.True(sub.IsSubsetOf(super));
        Assert.True(sub.Overlaps(super));
        Assert.False(sub.SetEquals(super));
    }

    [Fact]
    public void SetAlgebra_NullOther_Throws()
    {
        var set = new EnumSet<EnumSetColor>();
        Assert.Throws<ArgumentNullException>(() => set.UnionWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.IntersectWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.ExceptWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.SymmetricExceptWith(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsSubsetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsProperSubsetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsSupersetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.IsProperSupersetOf(null!));
        Assert.Throws<ArgumentNullException>(() => set.Overlaps(null!));
        Assert.Throws<ArgumentNullException>(() => set.SetEquals(null!));
    }
}
