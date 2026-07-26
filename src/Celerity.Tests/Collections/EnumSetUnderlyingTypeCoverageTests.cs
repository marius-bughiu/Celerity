using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Pins the two <see cref="EnumSet{TEnum}"/> behaviours that only a <b>16-bit-backed</b> enum can
/// exercise, plus the over-capacity rejection path that only a <c>ulong</c>-backed enum can reach.
///
/// <para>
/// <b>Why a separate file.</b> <see cref="EnumSet{TEnum}"/> converts between an enum value and a bit
/// index through two private helpers that switch on <c>Unsafe.SizeOf&lt;TEnum&gt;()</c> — one arm per
/// underlying width (1 / 2 / 4 / 8 bytes). The switch is a per-instantiation JIT constant, so an arm
/// is only ever compiled and executed for an <see cref="EnumSet{TEnum}"/> instantiated over an enum
/// of exactly that width. The enums declared in <c>EnumSetTests</c> cover <c>byte</c> (1),
/// the implicit <c>int</c> (4), and <c>long</c>/<c>ulong</c> (8) — nothing there is 2 bytes wide, so
/// the <c>short</c>/<c>ushort</c> arms of both helpers were never reached. The enums below close
/// that gap by round-tripping 16-bit-backed values through the whole public surface: add, membership,
/// removal, <c>All()</c>, enumeration, <c>CopyTo</c>, and the word-wise set algebra.
/// </para>
///
/// <para>
/// <b>The 16-bit arm is read <i>unsigned</i>, and that is load-bearing.</b> The value-to-index helper
/// reinterprets the enum's bits as <see cref="ushort"/>, not <see cref="short"/>, so that every value
/// maps to a non-negative index and an out-of-range cast (including a negative one) fails the single
/// unsigned bound check rather than indexing backwards out of the bit vector. Two tests pin exactly
/// that: <c>65535</c> — which reads as <c>-1</c> under a signed reinterpretation — must be an
/// ordinary, storable element, and a negative runtime cast must be rejected as out of range rather
/// than aliasing onto a valid bit.
/// </para>
///
/// <para>
/// <b>The over-capacity scan.</b> The per-enum metadata initializer measures the enum's maximum
/// underlying value to size the bit vector. For a <c>ulong</c>-backed enum it cannot simply widen
/// each member to <see cref="long"/> — a member above <see cref="long.MaxValue"/> would overflow the
/// conversion — so it compares in <see cref="ulong"/> space first and flags any member past the
/// supported maximum. That flag alone disqualifies the enum, even when every <i>other</i> member
/// would fit. The tests here assert the observable consequence: a clean
/// <see cref="NotSupportedException"/> naming the supported range from every public entry point,
/// never an <see cref="OverflowException"/> and never a
/// <see cref="TypeInitializationException"/> leaking out of the static initializer. The boundary is
/// also pinned from both sides: <c>65535</c> is supported, <c>65536</c> is not.
/// </para>
/// </summary>
public class EnumSetUnderlyingTypeCoverageTests
{
    // ── 16-bit-backed test enums ───────────────────────────────────────────────
    // Both are 2 bytes wide, so EnumSet<T> compiles the short/ushort arms of its
    // bit-index conversion helpers for them.

    /// <summary>A <c>ushort</c>-backed enum whose maximum member sits exactly on the supported bound.</summary>
    public enum UShortBacked : ushort
    {
        /// <summary>Bit 0 — the zero-valued member.</summary>
        Zero = 0,

        /// <summary>Bit 300 — word 4, proving the vector spans more than one word.</summary>
        Mid = 300,

        /// <summary>Bit 65535 — the largest value EnumSet will size a vector for, and <c>-1</c> if read as a signed 16-bit integer.</summary>
        Max = 65535,
    }

    /// <summary>A <c>short</c>-backed enum with small non-negative members across several words.</summary>
    public enum ShortBacked : short
    {
        /// <summary>Bit 0.</summary>
        S0 = 0,

        /// <summary>Bit 7 — same word as <see cref="S0"/>.</summary>
        S1 = 7,

        /// <summary>Bit 65 — the second word.</summary>
        S2 = 65,

        /// <summary>Bit 300 — the fifth word.</summary>
        S3 = 300,
    }

    // ── ulong-backed test enums for the over-capacity scan ─────────────────────

    /// <summary>A <c>ulong</c>-backed enum whose maximum member is exactly the supported maximum.</summary>
    public enum ULongAtCap : ulong
    {
        /// <summary>Bit 0.</summary>
        Zero = 0,

        /// <summary>Bit 65535 — on the bound, therefore still supported.</summary>
        Max = 65535,
    }

    /// <summary>A <c>ulong</c>-backed enum with a single member one past the supported maximum.</summary>
    public enum ULongJustOverCap : ulong
    {
        /// <summary>Bit 0.</summary>
        Zero = 0,

        /// <summary>Bit 9 — comfortably in range on its own.</summary>
        Small = 9,

        /// <summary>65536 — one past the bound, which disqualifies the whole enum.</summary>
        Over = 65536,
    }

    /// <summary>
    /// A <c>ulong</c>-backed enum with a member above <see cref="long.MaxValue"/> — the case that
    /// forces the metadata scan to compare in unsigned space instead of widening to <c>long</c>.
    /// </summary>
    public enum ULongUnsignedOnly : ulong
    {
        /// <summary>Bit 0.</summary>
        Zero = 0,

        /// <summary>Bit 9 — in range on its own.</summary>
        Small = 9,

        /// <summary>The maximum <see cref="ulong"/>; unrepresentable as a positive <see cref="long"/>.</summary>
        Over = ulong.MaxValue,
    }

    // ── 16-bit round-trip: value → bit index → value ───────────────────────────

    [Fact]
    public void TryAdd_ShouldRoundTripEveryMember_WhenEnumIsUShortBacked()
    {
        var set = new EnumSet<UShortBacked>();

        Assert.True(set.TryAdd(UShortBacked.Zero));
        Assert.True(set.TryAdd(UShortBacked.Mid));
        Assert.True(set.TryAdd(UShortBacked.Max));

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(UShortBacked.Zero));
        Assert.True(set.Contains(UShortBacked.Mid));
        Assert.True(set.Contains(UShortBacked.Max));

        // Re-adding is a no-op that reports the element was already present.
        Assert.False(set.TryAdd(UShortBacked.Max));
        Assert.Equal(3, set.Count);
    }

    [Fact]
    public void Contains_ShouldReportMaxUShortValueAsPresent_WhenEnumIsUShortBacked()
    {
        // 65535 has every one of its 16 bits set: reinterpreted as a *signed* 16-bit integer it is
        // -1, which would read as a huge unsigned index and be rejected as out of range. This test
        // is the one that fails if the 2-byte arm ever reads `short` instead of `ushort`.
        var set = new EnumSet<UShortBacked>();
        set.Add(UShortBacked.Max);

        Assert.True(set.Contains(UShortBacked.Max));
        Assert.Equal(UShortBacked.Max, Assert.Single(set));
        Assert.True(set.Remove(UShortBacked.Max));
        Assert.Empty(set);
    }

    [Fact]
    public void Add_ShouldThrowArgumentOutOfRange_WhenShortBackedValueIsNegative()
    {
        // ShortBacked tops out at 300, so its vector addresses bits 0..319. The bits of (short)-1
        // read back as the unsigned 65535, which fails the same single bound check — it must not
        // alias onto a valid bit or index the array backwards.
        var set = new EnumSet<ShortBacked>();
        var negative = (ShortBacked)(-1);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(negative));
        Assert.Equal("item", ex.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => set.TryAdd(negative));

        Assert.False(set.Contains(negative));
        Assert.False(set.Remove(negative));
        Assert.Empty(set);
    }

    [Fact]
    public void Add_ShouldThrowArgumentOutOfRange_WhenShortBackedValueIsAboveTheVector()
    {
        var set = new EnumSet<ShortBacked>();
        var tooLarge = (ShortBacked)30000; // in range for a short, far past the 320-bit vector

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(tooLarge));
        Assert.Equal("item", ex.ParamName);
        Assert.False(set.Contains(tooLarge));
        Assert.False(set.Remove(tooLarge));
    }

    [Fact]
    public void Contains_ShouldReturnFalse_ForUndefinedButInRangeShortBackedValue()
    {
        // 100 is inside the addressable vector but is not a declared constant: EnumSet stores bits,
        // not declarations, so it is simply absent rather than rejected.
        var set = new EnumSet<ShortBacked> { ShortBacked.S3 };

        Assert.False(set.Contains((ShortBacked)100));
        Assert.False(set.Remove((ShortBacked)100));
        Assert.True(set.TryAdd((ShortBacked)100));
        Assert.True(set.Contains((ShortBacked)100));
    }

    // ── 16-bit round-trip in the other direction: bit index → value ────────────

    [Fact]
    public void GetEnumerator_ShouldYieldAscendingValues_WhenEnumIsShortBacked()
    {
        // Enumeration reconstructs each element from its bit index through the 2-byte arm of the
        // index-to-value helper; inserting out of order proves the reconstruction, not the
        // insertion sequence, drives the output.
        var set = new EnumSet<ShortBacked> { ShortBacked.S3, ShortBacked.S0, ShortBacked.S2, ShortBacked.S1 };

        Assert.Equal(
            new[] { ShortBacked.S0, ShortBacked.S1, ShortBacked.S2, ShortBacked.S3 },
            set.ToList());
    }

    [Fact]
    public void GetEnumerator_ShouldYieldMaxUShortValue_WhenEnumIsUShortBacked()
    {
        // Bit 65535 must come back out as UShortBacked.Max — a signed reconstruction would produce
        // a value of -1 instead.
        var set = new EnumSet<UShortBacked> { UShortBacked.Max, UShortBacked.Zero };

        Assert.Equal(new[] { UShortBacked.Zero, UShortBacked.Max }, set.ToList());
    }

    [Fact]
    public void All_ShouldContainEveryDeclaredConstant_WhenEnumIsShortBacked()
    {
        var set = EnumSet<ShortBacked>.All();

        Assert.Equal(Enum.GetValues<ShortBacked>().Length, set.Count);
        Assert.Equal(
            new[] { ShortBacked.S0, ShortBacked.S1, ShortBacked.S2, ShortBacked.S3 },
            set.ToList());
        Assert.False(set.Contains((ShortBacked)1)); // in range, never declared
    }

    [Fact]
    public void All_ShouldContainEveryDeclaredConstant_WhenEnumIsUShortBacked()
    {
        var set = EnumSet<UShortBacked>.All();

        Assert.Equal(3, set.Count);
        Assert.Equal(
            new[] { UShortBacked.Zero, UShortBacked.Mid, UShortBacked.Max },
            set.ToList());
    }

    [Fact]
    public void CopyTo_ShouldWriteAscendingValues_WhenEnumIsShortBacked()
    {
        var set = new EnumSet<ShortBacked> { ShortBacked.S3, ShortBacked.S0, ShortBacked.S2 };
        var array = new ShortBacked[4];

        set.CopyTo(array, 1);

        Assert.Equal(default, array[0]);
        Assert.Equal(ShortBacked.S0, array[1]);
        Assert.Equal(ShortBacked.S2, array[2]);
        Assert.Equal(ShortBacked.S3, array[3]);
    }

    [Fact]
    public void SourceConstructor_ShouldDeduplicate_WhenEnumIsUShortBacked()
    {
        var set = new EnumSet<UShortBacked>(new[]
        {
            UShortBacked.Max, UShortBacked.Zero, UShortBacked.Max, UShortBacked.Zero,
        });

        Assert.Equal(2, set.Count);
        Assert.Equal(new[] { UShortBacked.Zero, UShortBacked.Max }, set.ToList());
    }

    // ── Word-wise set algebra over a 16-bit-backed enum ────────────────────────

    [Fact]
    public void UnionWith_ShouldMergeBothOperands_WhenEnumIsShortBacked()
    {
        var a = new EnumSet<ShortBacked> { ShortBacked.S0, ShortBacked.S3 };
        var b = new EnumSet<ShortBacked> { ShortBacked.S1, ShortBacked.S3 };

        a.UnionWith(b);

        Assert.Equal(new[] { ShortBacked.S0, ShortBacked.S1, ShortBacked.S3 }, a.ToList());
    }

    [Fact]
    public void IntersectWith_ShouldKeepOnlySharedElements_WhenEnumIsShortBacked()
    {
        var a = new EnumSet<ShortBacked> { ShortBacked.S0, ShortBacked.S2, ShortBacked.S3 };
        var b = new EnumSet<ShortBacked> { ShortBacked.S2, ShortBacked.S3 };

        a.IntersectWith(b);

        Assert.Equal(new[] { ShortBacked.S2, ShortBacked.S3 }, a.ToList());
    }

    [Fact]
    public void ExceptWith_ShouldRemoveOtherElements_WhenEnumIsShortBacked()
    {
        var a = new EnumSet<ShortBacked> { ShortBacked.S0, ShortBacked.S2, ShortBacked.S3 };
        var b = new EnumSet<ShortBacked> { ShortBacked.S2 };

        a.ExceptWith(b);

        Assert.Equal(new[] { ShortBacked.S0, ShortBacked.S3 }, a.ToList());
    }

    [Fact]
    public void SymmetricExceptWith_ShouldKeepUnsharedElements_WhenEnumIsShortBacked()
    {
        var a = new EnumSet<ShortBacked> { ShortBacked.S0, ShortBacked.S2 };
        var b = new EnumSet<ShortBacked> { ShortBacked.S2, ShortBacked.S3 };

        a.SymmetricExceptWith(b);

        Assert.Equal(new[] { ShortBacked.S0, ShortBacked.S3 }, a.ToList());
    }

    [Fact]
    public void QueryOperations_ShouldCompareBitVectors_WhenEnumIsUShortBacked()
    {
        var sub = new EnumSet<UShortBacked> { UShortBacked.Zero, UShortBacked.Max };
        var super = new EnumSet<UShortBacked> { UShortBacked.Zero, UShortBacked.Mid, UShortBacked.Max };

        Assert.True(sub.IsSubsetOf(super));
        Assert.True(sub.IsProperSubsetOf(super));
        Assert.True(super.IsSupersetOf(sub));
        Assert.True(super.IsProperSupersetOf(sub));
        Assert.True(sub.Overlaps(super));
        Assert.False(sub.SetEquals(super));
        Assert.True(sub.SetEquals(new EnumSet<UShortBacked> { UShortBacked.Max, UShortBacked.Zero }));
    }

    [Fact]
    public void IntersectWith_ShouldUseTheFallbackPath_WhenOtherIsNotAnEnumSet()
    {
        // A lazy sequence is not an EnumSet, so the shared SetOperations fallback runs — it reaches
        // the same 16-bit conversion helpers through Contains/TryAdd instead of word-wise masking.
        var a = new EnumSet<ShortBacked> { ShortBacked.S0, ShortBacked.S1, ShortBacked.S3 };
        IEnumerable<ShortBacked> other = new[] { ShortBacked.S1, ShortBacked.S2 }.Select(x => x);

        a.IntersectWith(other);

        Assert.Equal(ShortBacked.S1, Assert.Single(a));
    }

    // ── The ulong over-capacity scan ───────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldSucceed_WhenULongEnumMaxIsExactlyTheSupportedBound()
    {
        // The far side of the over-capacity check: 65535 is the largest supported value, so this
        // enum must be accepted and behave normally. Pinned alongside the rejection tests below so
        // the bound cannot silently drift by one in either direction.
        var set = new EnumSet<ULongAtCap> { ULongAtCap.Zero, ULongAtCap.Max };

        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(ULongAtCap.Max));
        Assert.Equal(new[] { ULongAtCap.Zero, ULongAtCap.Max }, set.ToList());
        Assert.Equal(2, EnumSet<ULongAtCap>.All().Count);
    }

    [Fact]
    public void Constructor_ShouldThrowNotSupported_WhenULongEnumHasAMemberOnePastTheBound()
    {
        // Only one member (65536) is over the bound; the other two would fit comfortably. A single
        // over-capacity member must still disqualify the whole enum rather than being silently
        // dropped from an otherwise-usable set.
        var ex = Assert.Throws<NotSupportedException>(() => new EnumSet<ULongJustOverCap>());

        Assert.Contains("range", ex.Message);
        Assert.Contains("65535", ex.Message);
        Assert.Contains(nameof(ULongJustOverCap), ex.Message);
    }

    [Fact]
    public void Constructor_ShouldThrowNotSupported_WhenULongEnumMemberExceedsLongMaxValue()
    {
        // ulong.MaxValue cannot be widened to a positive long. The metadata scan compares in
        // unsigned space precisely so this reports a clean NotSupportedException — Assert.Throws is
        // exact about the type, so an OverflowException or a TypeInitializationException escaping
        // the static initializer fails here.
        var ex = Assert.Throws<NotSupportedException>(() => new EnumSet<ULongUnsignedOnly>());

        Assert.Contains("range", ex.Message);
        Assert.Contains(nameof(ULongUnsignedOnly), ex.Message);
    }

    [Fact]
    public void All_ShouldThrowNotSupported_WhenULongEnumIsOverCapacity()
    {
        // Every public entry point must reject the enum, not just the constructor.
        Assert.Throws<NotSupportedException>(() => EnumSet<ULongUnsignedOnly>.All());
        Assert.Throws<NotSupportedException>(() => EnumSet<ULongJustOverCap>.All());
    }

    [Fact]
    public void SourceConstructor_ShouldThrowNotSupported_WhenULongEnumIsOverCapacity()
    {
        Assert.Throws<NotSupportedException>(
            () => new EnumSet<ULongUnsignedOnly>(new[] { ULongUnsignedOnly.Small }));
    }
}
