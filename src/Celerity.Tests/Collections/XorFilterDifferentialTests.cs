using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="XorFilter{T, THasher}"/> against
/// <see cref="HashSet{T}"/> as the oracle. CsCheck generates the element set, both models are
/// built from it, and the relationship an approximate membership filter actually promises is
/// asserted: <b>one-directional</b> — every element the oracle holds must be reported present,
/// while a probe the oracle does not hold may be reported present at the advertised rate and
/// nothing can be concluded from a single such answer.
///
/// <para>
/// A xor filter is built once and never mutated, so there is no operation sequence to interleave;
/// what varies is the input, and that is what is generated here. The two axes that matter are
/// <b>size</b> — the peeling construction is the risk surface, and it has to converge from the
/// empty set upward, including the sizes where the 1.23× slot table rounds awkwardly — and
/// <b>duplication</b>, since the constructor folds duplicates and the resulting count is a claim
/// about the fold, not about the input.
/// </para>
///
/// <para>
/// The count claim is asserted differently per hasher, because it is a different claim.
/// <see cref="Int64WangHasher"/> is a bijection on 64 bits, so no two distinct <c>long</c>s can
/// share a key and the distinct count must equal the oracle's <b>exactly</b>. The naive 32-bit
/// hasher can collide two distinct <c>int</c>s onto one key, which the constructor folds into a
/// single entry, so there the count is a <b>lower bound</b> — dedupe only ever removes. Asserting
/// equality for the weak hasher would be asserting the absence of a collision the type explicitly
/// tolerates.
/// </para>
///
/// <para>
/// The realized false-positive <i>rate</i> is not measured here. It is a statistic, and a
/// per-iteration bound over a generated set small enough to shrink would either be so loose it
/// asserts nothing or so tight it flakes; <see cref="XorFilterFalsePositiveTests"/> measures it
/// over fixed datasets sized to the bound instead. What is asserted here is the part that is a
/// hard guarantee rather than a probability, plus the one case where the rate is exactly zero: an
/// empty filter short-circuits, so it must report <i>every</i> probe absent.
/// </para>
/// </summary>
public class XorFilterDifferentialTests
{
    /// <summary>
    /// The weak 32-bit hasher, whose key space admits collisions the constructor folds away. The
    /// element domain is narrow relative to the set size so duplicates are generated densely.
    /// </summary>
    [Fact]
    public void XorFilter_ShouldHaveNoFalseNegatives_UnderTheNaiveInt32Hasher()
    {
        Gen.Int[-400, 400].List[0, 300].Sample(items =>
        {
            var oracle = new HashSet<int>(items);
            var sut = new XorFilter<int, Int32WangNaiveHasher>(oracle);

            // A collision folds two oracle elements into one entry, so the count can only fall.
            Assert.True(
                sut.Count <= oracle.Count,
                $"distinct count {sut.Count} exceeds the oracle's {oracle.Count}");

            foreach (int item in oracle)
                Assert.True(sut.Contains(item), $"false negative for {item}");

            AssertShape(sut, oracle.Count);

            if (oracle.Count == 0)
            {
                for (int probe = -400; probe <= 400; probe++)
                    Assert.False(sut.Contains(probe), $"the empty filter reported {probe} present");
            }
        }, iter: 40);
    }

    /// <summary>
    /// The bijective 64-bit hasher, where the fold can never remove anything the oracle counted,
    /// so the distinct count is an equality rather than a bound.
    /// </summary>
    [Fact]
    public void XorFilter_ShouldHaveNoFalseNegatives_UnderTheBijectiveInt64Hasher()
    {
        Gen.Long[-4_000, 4_000].List[0, 300].Sample(items =>
        {
            var oracle = new HashSet<long>(items);
            var sut = new XorFilter<long, Int64WangHasher>(oracle);

            Assert.Equal(oracle.Count, sut.Count);

            foreach (long item in oracle)
                Assert.True(sut.Contains(item), $"false negative for {item}");

            AssertShape(sut, oracle.Count);
        }, iter: 40);
    }

    /// <summary>
    /// A reference-typed element, where the key comes from the string's contents rather than from
    /// a scrambled integer. Generated strings share prefixes and lengths far more often than
    /// generated integers share bits, which is a different shape of input to the same peel.
    /// </summary>
    [Fact]
    public void XorFilter_ShouldHaveNoFalseNegatives_OverStrings()
    {
        Gen.String[Gen.Char['a', 'f'], 0, 6].List[0, 200].Sample(items =>
        {
            var oracle = new HashSet<string>(items);
            var sut = new XorFilter<string, StringFnV1AHasher>(oracle);

            Assert.True(
                sut.Count <= oracle.Count,
                $"distinct count {sut.Count} exceeds the oracle's {oracle.Count}");

            foreach (string item in oracle)
                Assert.True(sut.Contains(item), $"false negative for \"{item}\"");

            AssertShape(sut, oracle.Count);
        }, iter: 40);
    }

    /// <summary>
    /// Duplicates in the source must not change the filter that comes out of it: the constructor
    /// folds on the hashed key, so a source enumerated with every element repeated is the same
    /// filter as the distinct source, down to the slot count.
    /// </summary>
    [Fact]
    public void XorFilter_ShouldIgnoreDuplicatesInTheSource()
    {
        Gen.Int[-200, 200].List[0, 200].Sample(items =>
        {
            var distinct = new HashSet<int>(items);
            var withDuplicates = items.Concat(items).Concat(distinct).ToArray();

            var fromDistinct = new XorFilter<int, Int32WangNaiveHasher>(distinct);
            var fromDuplicates = new XorFilter<int, Int32WangNaiveHasher>(withDuplicates);

            Assert.Equal(fromDistinct.Count, fromDuplicates.Count);
            Assert.Equal(fromDistinct.SlotCount, fromDuplicates.SlotCount);

            foreach (int item in distinct)
                Assert.True(fromDuplicates.Contains(item), $"false negative for {item}");
        }, iter: 40);
    }

    // The size claims that hold whatever the element type is. The slot table is over-provisioned
    // (the peel needs roughly 1.23 slots per entry to converge), but the assertion is only that it
    // is never *under* the entry count — the exact provisioning factor is the implementation's to
    // choose, and pinning it here would turn a tuning change into a test failure. What is pinned
    // exactly is that both derived properties are read off the two counts and nothing else.
    private static void AssertShape<T, THasher>(XorFilter<T, THasher> sut, int oracleCount)
        where THasher : struct, IHashProvider<T>
    {
        Assert.Equal(1.0 / (1 << XorFilter<T, THasher>.FingerprintBits), sut.FalsePositiveRate);

        if (sut.Count == 0)
        {
            Assert.Equal(0, oracleCount);
            Assert.Equal(0d, sut.BitsPerElement);
            return;
        }

        Assert.True(
            sut.SlotCount >= sut.Count,
            $"slot count {sut.SlotCount} is below the entry count {sut.Count}");
        Assert.Equal(sut.SlotCount * 8.0 / sut.Count, sut.BitsPerElement);
    }
}
