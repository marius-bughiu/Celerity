using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;
using static Celerity.Tests.Collections.MinimalPerfectHashTests;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="MinimalPerfectHash{T, THasher}"/> against
/// <see cref="HashSet{T}"/> as the oracle. CsCheck generates the key source — with dense duplicates, since the
/// constructor folds them — and the function built from it must be a bijection from the oracle's distinct
/// keys onto <c>[0, Count)</c> with <c>Count</c> equal to the oracle's size. The function is built once and
/// never mutated, so what varies is the input: its size, its duplication, the hasher's quality, and
/// <see cref="MinimalPerfectHash{T, THasher}.Gamma"/>.
/// </summary>
public class MinimalPerfectHashDifferentialTests
{
    // Pairs collide below 64, exercising the exact table alongside the levels.
    private struct WeakHasher : IHashProvider<int>
    {
        public int Hash(int key) => key is >= 0 and < 64 ? key >> 1 : key;
    }

    [Fact]
    public void IsABijection_UnderAGoodHasher_AtAnyGamma()
    {
        Gen.Select(Gen.Int[-2_000, 2_000].List[0, 400], Gen.Double[1.0, 10.0]).Sample((items, gamma) =>
        {
            var oracle = new HashSet<int>(items);
            var sut = new MinimalPerfectHash<int, Int32Murmur3Hasher>(items, gamma);

            Assert.Equal(oracle.Count, sut.Count);
            AssertBijection(sut, oracle);
        }, iter: 60);
    }

    [Fact]
    public void IsABijection_UnderACollidingHasher()
    {
        Gen.Int[-100, 300].List[0, 300].Sample(items =>
        {
            var oracle = new HashSet<int>(items);
            var sut = new MinimalPerfectHash<int, WeakHasher>(items);

            Assert.Equal(oracle.Count, sut.Count);
            AssertBijection(sut, oracle);
        }, iter: 60);
    }

    [Fact]
    public void IsABijection_OverStrings_WithA32BitHasher()
    {
        Gen.String[Gen.Char['a', 'f'], 0, 6].List[0, 200].Sample(items =>
        {
            var oracle = new HashSet<string>(items);
            var sut = new MinimalPerfectHash<string, StringFnV1AHasher>(items);

            Assert.Equal(oracle.Count, sut.Count);
            AssertBijection(sut, oracle);
        }, iter: 60);
    }

    [Fact]
    public void Duplicates_DoNotChangeTheShape()
    {
        Gen.Long[-5_000, 5_000].List[0, 300].Sample(items =>
        {
            var distinct = new HashSet<long>(items);
            long[] withDuplicates = items.Concat(items).Concat(distinct).ToArray();

            var fromDistinct = new MinimalPerfectHash<long, Int64WangHasher>(distinct);
            var fromDuplicates = new MinimalPerfectHash<long, Int64WangHasher>(withDuplicates);

            Assert.Equal(fromDistinct.Count, fromDuplicates.Count);
            Assert.Equal(fromDistinct.LevelCount, fromDuplicates.LevelCount);
            Assert.Equal(fromDistinct.BitsPerKey, fromDuplicates.BitsPerKey);
            AssertBijection(fromDuplicates, distinct);
        }, iter: 40);
    }
}
