using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="EnumMap{TEnum, TValue}"/> against
/// <see cref="Dictionary{TKey, TValue}"/> as the oracle. CsCheck generates randomized operation
/// sequences over a key domain, applies each to both models, and reconciles the whole observable
/// surface — <see cref="EnumMap{TEnum, TValue}.Count"/>, both lookup paths, both removal
/// overloads, <c>Keys</c>, <c>Values</c>, <c>ContainsValue</c> and <c>CopyTo</c> — after every
/// step.
///
/// <para>
/// Two things here the oracle cannot be asked directly. <b>Enumeration order</b>: a
/// <see cref="Dictionary{TKey, TValue}"/> promises none, while <see cref="EnumMap{TEnum, TValue}"/>
/// promises ascending underlying value, which falls out of walking the occupancy vector low bit
/// first — so order is reconciled against the oracle's keys *sorted*, not against its enumeration.
/// <b>Presence of a default value</b>: the value domain includes <c>0</c> deliberately, because
/// occupancy is tracked out-of-band precisely so a key mapped to <c>default(TValue)</c> is not
/// mistaken for an absent one, and only an oracle that stores zeros can catch a regression there.
/// </para>
///
/// <para>
/// The key domain is wider than the enum's declared members on purpose. The backing store is sized
/// to <c>WordCount * 64</c> addressable positions, so an undefined-but-addressable cast such as
/// <c>(EnumSetColor)40</c> is a perfectly valid key that no test written from the enum's member
/// list would ever produce, while a cast past the last word is rejected by the write surface and
/// reported absent by the read surface. Both are generated, and the second is asserted as the
/// asymmetry it is: a throw from <c>TryAdd</c> and the indexer setter, a quiet <c>false</c> from
/// <c>ContainsKey</c>, <c>TryGetValue</c> and <c>Remove</c>.
/// </para>
///
/// <para>
/// Deterministic reproductions of each individual behaviour live in <see cref="EnumMapTests"/> and
/// <see cref="EnumMapEnumerationTests"/>; this suite is the randomized second opinion over the
/// interleavings those cannot enumerate.
/// </para>
/// </summary>
public class EnumMapDifferentialTests
{
    private enum Op { TryAdd, Add, IndexerSet, Remove, RemoveOut, Lookup, Clear, Rebuild }

    // Weighted so the map spends most of its life partially populated: adds outnumber removes,
    // and the two wholesale operations stay rare enough that long interleavings survive.
    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 99].Select(n => n < 30 ? Op.TryAdd
                                 : n < 42 ? Op.Add
                                 : n < 60 ? Op.IndexerSet
                                 : n < 72 ? Op.Remove
                                 : n < 82 ? Op.RemoveOut
                                 : n < 96 ? Op.Lookup
                                 : n < 98 ? Op.Clear
                                 : Op.Rebuild);

    // The value domain includes 0 — the default — so the occupancy vector is what has to
    // distinguish "mapped to zero" from "absent", not the stored value.
    private static readonly Gen<(Op Kind, int KeyIndex, int Value)> GenOp =
        Gen.Select(GenKind, Gen.Int[0, 63], Gen.Int[0, 6]);

    /// <summary>
    /// A one-word enum (max member 5, so 64 addressable positions): every declared member, four
    /// undefined-but-addressable casts including the top of the single word, and one cast past it.
    /// </summary>
    [Fact]
    public void EnumMap_ShouldMatch_Dictionary_OverAOneWordEnum()
    {
        var domain = new[]
        {
            EnumSetColor.Red, EnumSetColor.Green, EnumSetColor.Blue,
            EnumSetColor.Yellow, EnumSetColor.Cyan, EnumSetColor.Magenta,
            (EnumSetColor)6, (EnumSetColor)31, (EnumSetColor)40, (EnumSetColor)63,
        };

        Run(domain, (EnumSetColor)64, k => (int)k);
    }

    /// <summary>
    /// A three-word enum (max member 130, so 192 addressable positions), which is where an
    /// occupancy walk that mishandles a word boundary — or an enumeration that visits words out of
    /// order — shows up. The domain straddles all three words and includes both edges of each.
    /// </summary>
    [Fact]
    public void EnumMap_ShouldMatch_Dictionary_OverAMultiWordEnum()
    {
        var domain = new[]
        {
            EnumSetWide.Zero, EnumSetWide.One, EnumSetWide.WordEdge,
            EnumSetWide.NextWord, EnumSetWide.High, EnumSetWide.Top,
            (EnumSetWide)62, (EnumSetWide)127, (EnumSetWide)128, (EnumSetWide)191,
        };

        Run(domain, (EnumSetWide)192, k => (int)k);
    }

    /// <summary>
    /// A byte-backed enum, so the underlying value is read at a width other than 32 bits. Its
    /// maximum member is 200, giving four words, and a byte can never reach the position past
    /// them — <c>(EnumSetByte)255</c> is still addressable — so this one has no out-of-range key
    /// and the parameter is passed as <c>null</c>.
    /// </summary>
    [Fact]
    public void EnumMap_ShouldMatch_Dictionary_OverAByteBackedEnum()
    {
        var domain = new[]
        {
            EnumSetByte.A, EnumSetByte.B, EnumSetByte.C,
            (EnumSetByte)1, (EnumSetByte)64, (EnumSetByte)191, (EnumSetByte)255,
        };

        Run(domain, outOfRange: null, k => (int)k);
    }

    // ── The script ────────────────────────────────────────────────────────────

    private static void Run<TEnum>(TEnum[] domain, TEnum? outOfRange, Func<TEnum, int> underlying)
        where TEnum : struct, Enum
    {
        GenOp.List[0, 400].Sample(ops =>
        {
            var sut = new EnumMap<TEnum, int>();
            var oracle = new Dictionary<TEnum, int>();

            foreach (var (kind, keyIndex, value) in ops)
            {
                // One index in the generated range addresses the out-of-range key when the enum
                // has one, so the rejection paths are reached by the same generated stream rather
                // than by a separate hand-written case.
                bool outside = outOfRange is not null && keyIndex % 16 == 15;
                TEnum key = outside ? outOfRange!.Value : domain[keyIndex % domain.Length];

                switch (kind)
                {
                    case Op.TryAdd when outside:
                        Assert.Throws<ArgumentOutOfRangeException>(() => sut.TryAdd(key, value));
                        break;

                    case Op.TryAdd:
                        Assert.Equal(!oracle.ContainsKey(key), sut.TryAdd(key, value));
                        oracle.TryAdd(key, value);
                        break;

                    case Op.Add when outside:
                        Assert.Throws<ArgumentOutOfRangeException>(() => sut.Add(key, value));
                        break;

                    case Op.Add when oracle.ContainsKey(key):
                        // A rejected Add must leave the entry it collided with untouched.
                        Assert.Throws<ArgumentException>(() => sut.Add(key, value));
                        Assert.Equal(oracle[key], sut[key]);
                        break;

                    case Op.Add:
                        sut.Add(key, value);
                        oracle.Add(key, value);
                        break;

                    case Op.IndexerSet when outside:
                        Assert.Throws<ArgumentOutOfRangeException>(() => sut[key] = value);
                        break;

                    case Op.IndexerSet:
                        sut[key] = value;
                        oracle[key] = value;
                        break;

                    case Op.Remove:
                        // Out of range is not an error here: it is simply not present.
                        Assert.Equal(oracle.Remove(key), sut.Remove(key));
                        break;

                    case Op.RemoveOut:
                    {
                        bool expected = oracle.Remove(key, out int expectedValue);
                        Assert.Equal(expected, sut.Remove(key, out int actualValue));
                        Assert.Equal(expected ? expectedValue : 0, actualValue);
                        break;
                    }

                    case Op.Lookup:
                    {
                        bool present = oracle.TryGetValue(key, out int expected);
                        Assert.Equal(present, sut.ContainsKey(key));
                        Assert.Equal(present, sut.TryGetValue(key, out int actual));
                        Assert.Equal(present ? expected : 0, actual);

                        if (present)
                            Assert.Equal(expected, sut[key]);
                        else
                            Assert.Throws<KeyNotFoundException>(() => sut[key]);
                        break;
                    }

                    case Op.Clear:
                        sut.Clear();
                        oracle.Clear();
                        break;

                    case Op.Rebuild:
                        // The IEnumerable constructor is the only build path the operation surface
                        // never reaches, and it has a same-type fast path worth reconciling too.
                        AssertEquivalent(new EnumMap<TEnum, int>(sut), oracle, underlying);
                        break;
                }

                Assert.Equal(oracle.Count, sut.Count);
            }

            AssertEquivalent(sut, oracle, underlying);
        }, iter: 40);
    }

    private static void AssertEquivalent<TEnum>(
        EnumMap<TEnum, int> sut, Dictionary<TEnum, int> oracle, Func<TEnum, int> underlying)
        where TEnum : struct, Enum
    {
        Assert.Equal(oracle.Count, sut.Count);

        // The oracle promises no order, so its keys are sorted into the order EnumMap promises.
        var expected = oracle.OrderBy(e => underlying(e.Key)).ToArray();

        Assert.Equal(expected, sut.ToArray());
        Assert.Equal(expected.Select(e => e.Key), sut.Keys.ToArray());
        // `Values` is declared ICollection<TValue?> — the annotation the IDictionary<,>
        // implementation carries — so the projection restores the concrete int being stored.
        Assert.Equal(expected.Select(e => e.Value), sut.Values.Cast<int>());

        // CopyTo writes the same sequence at an offset, leaving the padding it was given alone.
        var buffer = new KeyValuePair<TEnum, int>[oracle.Count + 2];
        buffer[0] = new KeyValuePair<TEnum, int>(default, -1);
        sut.CopyTo(buffer, 1);
        Assert.Equal(new KeyValuePair<TEnum, int>(default, -1), buffer[0]);
        Assert.Equal(expected, buffer.Skip(1).Take(oracle.Count));
        Assert.Equal(default, buffer[^1]);

        // ContainsValue over the whole value domain plus one value that is never generated.
        for (int v = 0; v <= 7; v++)
            Assert.Equal(oracle.ContainsValue(v), sut.ContainsValue(v));
    }
}
