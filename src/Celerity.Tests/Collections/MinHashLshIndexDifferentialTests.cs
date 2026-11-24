using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="MinHashLshIndex{TKey,T,THasher}"/> against a
/// naive model. CsCheck generates the band layout, a pool of small sets and a stream of add / remove /
/// query / clear operations; after every query the index's candidates must equal the model's, which
/// keeps each key's <em>densified registers</em> and compares them band by band — no hashing, no
/// bucket chains, no deduplication trick.
///
/// <para>
/// The model also densifies independently: it walks each empty register's probe order to the end,
/// where the index stops after a few steps and inverts the permutation to find the same donor. The
/// sets are drawn so that the fallback is the common case — 16 registers, at most eight elements, and
/// a hasher whose 64-bit hash puts element <c>x</c> in register <c>x mod 16</c> with value
/// <c>x / 16</c>, so registers are often empty and two sets often agree on one.
/// </para>
/// </summary>
public class MinHashLshIndexDifferentialTests
{
    private const int Precision = 4;
    private const int Registers = 1 << Precision;
    private const ulong Empty = ulong.MaxValue;

    // Element x lands in register x mod 16 with value x / 16: the top Precision bits of the hash
    // pick the register and the rest are the value.
    private struct RegisterValueHasher : IHashProvider<int>, IHashProvider64<int>
    {
        public int Hash(int key) => key;

        public ulong Hash64(int key) => ((ulong)(key & (Registers - 1)) << (64 - Precision)) | (uint)(key >> Precision);
    }

    private static ulong[] RegistersOf(IEnumerable<int> set)
    {
        var registers = Enumerable.Repeat(Empty, Registers).ToArray();
        foreach (int x in set)
        {
            int register = x & (Registers - 1);
            registers[register] = Math.Min(registers[register], (ulong)(x >> Precision));
        }

        return registers;
    }

    // The probe order of register i, as the index defines it: t -> (a·t + c) mod m.
    private static int Probe(int register, int step)
    {
        ulong z = ((ulong)register + 1) * 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (int)(((z | 1) * (ulong)step + (z >> 32)) & (Registers - 1));
    }

    // Walks each empty register's whole order, which reaches every register once, and returns
    // each element's full hash (register index on top, value below), as the index bands it.
    private static ulong[] Densify(ulong[] registers)
    {
        if (registers.All(v => v == Empty))
            return registers;

        var dense = new ulong[Registers];
        for (int i = 0; i < Registers; i++)
        {
            int source = i;
            for (int step = 0; registers[source] == Empty; step++)
                source = Probe(i, step);
            dense[i] = ((ulong)source << (64 - Precision)) | registers[source];
        }

        return dense;
    }

    private static bool SharesABand(ulong[] a, ulong[] b, int bands, int rows)
    {
        for (int band = 0; band < bands; band++)
        {
            if (a.AsSpan(band * rows, rows).SequenceEqual(b.AsSpan(band * rows, rows)))
                return true;
        }

        return false;
    }

    [Fact]
    public void DisjointSingletons_ShouldNotMatch_WhenTheirValuesCoincideInDifferentRegisters()
    {
        // {0} and {1} both store value 0, in registers 0 and 1. Densified by value alone, both
        // became sixteen zeroes and matched every band despite estimating a Jaccard of 0.
        var index = new MinHashLshIndex<int, int, RegisterValueHasher>(Registers, 1, Precision);
        var zero = new MinHashSketch<int, RegisterValueHasher>(new[] { 0 }, Precision);
        var one = new MinHashSketch<int, RegisterValueHasher>(new[] { 1 }, Precision);
        index.Add(0, zero);

        Assert.Equal(0d, zero.EstimateJaccard(one));
        Assert.Empty(index.Query(one));
        Assert.Equal(new[] { 0 }, index.Query(zero));
    }

    private enum Op { Add, Remove, Query, Clear }

    private static readonly Gen<(Op Kind, int Key, int Set)> GenOp =
        Gen.Select(
            Gen.Int[0, 99].Select(n => n < 40 ? Op.Add : n < 55 ? Op.Remove : n < 98 ? Op.Query : Op.Clear),
            Gen.Int[0, 9],
            Gen.Int[0, 11]);

    private static readonly Gen<(int Bands, int Rows, List<int[]> Sets, List<(Op Kind, int Key, int Set)> Ops)> GenScript =
        Gen.Int[0, 4].SelectMany(log2Bands =>
        {
            int bands = 1 << log2Bands;
            return Gen.Select(
                Gen.Const(bands),
                Gen.Int[1, Registers / bands],
                Gen.Int[0, 47].Array[0, 8].List[12, 12],
                GenOp.List[0, 300]);
        });

    [Fact]
    public void Query_ShouldReturnExactlyTheKeysThatShareADensifiedBand()
    {
        GenScript.Sample(script =>
        {
            var (bands, rows, sets, ops) = script;
            var sketches = sets.Select(s => new MinHashSketch<int, RegisterValueHasher>(s, Precision)).ToList();
            var dense = sets.Select(s => Densify(RegistersOf(s))).ToList();

            var index = new MinHashLshIndex<int, int, RegisterValueHasher>(bands, rows, Precision);
            var model = new Dictionary<int, ulong[]>();

            foreach (var (kind, key, set) in ops)
            {
                switch (kind)
                {
                    case Op.Add:
                        bool added = index.TryAdd(key, sketches[set]);
                        Assert.Equal(model.TryAdd(key, dense[set]), added);
                        break;

                    case Op.Remove:
                        Assert.Equal(model.Remove(key), index.Remove(key));
                        break;

                    case Op.Query:
                        var expected = model.Where(e => SharesABand(e.Value, dense[set], bands, rows))
                            .Select(e => e.Key).OrderBy(k => k).ToList();
                        var actual = new List<int>();
                        Assert.Equal(expected.Count, index.Query(sketches[set], actual));
                        Assert.Equal(expected, actual.OrderBy(k => k));
                        break;

                    default:
                        index.Clear();
                        model.Clear();
                        break;
                }

                Assert.Equal(model.Count, index.Count);
            }

            Assert.Equal(model.Keys.OrderBy(k => k), index.OrderBy(k => k));
            foreach (int key in Enumerable.Range(0, 10))
                Assert.Equal(model.ContainsKey(key), index.ContainsKey(key));
        }, iter: 300);
    }
}
