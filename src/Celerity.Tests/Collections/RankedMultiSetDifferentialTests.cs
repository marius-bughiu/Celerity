using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="RankedMultiSet{T, TComparer}"/> against a stably sorted
/// <see cref="List{T}"/> as the oracle. CsCheck generates randomized operation sequences, applies each to both,
/// and asserts they agree on content, order, every rank, and the order of equal elements.
///
/// <para>
/// The elements are <c>(Key, Id)</c> pairs ordered by the key alone, so the ids are invisible to the comparer
/// and record only insertion order. The oracle inserts each pair after every equal key and removes the first
/// equal key, so agreeing on the full pairs checks the type's stability, not only its sorting.
/// </para>
/// </summary>
public class RankedMultiSetDifferentialTests
{
    private enum Op { Add, Remove, RemoveAll, RemoveAt, TrimExcess, Clear }

    // Keys -5..200 with Clear at 0.3% of ops: the multiset often grows past one 512-element bucket, so
    // splits, merges and drops happen, but runs of equal keys stay short. The long-run property below
    // covers runs that span buckets.
    private static readonly Gen<(Op op, int value)> GenOp =
        Gen.Select(
            Gen.Int[0, 999].Select(n => n < 620 ? Op.Add
                                      : n < 880 ? Op.Remove
                                      : n < 900 ? Op.RemoveAll
                                      : n < 990 ? Op.RemoveAt
                                      : n < 997 ? Op.TrimExcess
                                      : Op.Clear),
            Gen.Int[-5, 200]);

    // Four keys and no Clear: a few thousand ops build runs of hundreds of copies that span several buckets
    // sharing one maximum, and RemoveAll / RemoveAt then cut through them. The ids check FIFO order across
    // every split, merge and drop.
    private static readonly Gen<(Op op, int value)> GenLongRunOp =
        Gen.Select(
            Gen.Int[0, 999].Select(n => n < 700 ? Op.Add
                                      : n < 900 ? Op.Remove
                                      : n < 905 ? Op.RemoveAll
                                      : n < 995 ? Op.RemoveAt
                                      : Op.TrimExcess),
            Gen.Int[0, 3]);

    [Fact]
    public void RankedMultiSet_ShouldMatch_AStablySortedList_WhenRunsSpanBuckets()
    {
        GenLongRunOp.List[1000, 5000].Sample(Run, iter: 40);
    }

    [Fact]
    public void RankedMultiSet_ShouldMatch_AStablySortedList()
    {
        GenOp.List[0, 4000].Sample(Run, iter: 40);
    }

    private static void Run(List<(Op op, int value)> ops)
    {
        {
            var sut = new RankedMultiSet<(int Key, int Id), ByKey>(default(ByKey));
            var oracle = new List<(int Key, int Id)>();
            int nextId = 0;

            foreach (var (op, value) in ops)
            {
                switch (op)
                {
                    case Op.Add:
                        var item = (value, nextId++);
                        sut.Add(item);
                        oracle.Insert(UpperBound(oracle, value), item);
                        break;
                    case Op.Remove:
                        int at = LowerBound(oracle, value);
                        bool present = at < oracle.Count && oracle[at].Key == value;
                        Assert.Equal(present, sut.Remove((value, -1)));
                        if (present)
                            oracle.RemoveAt(at);
                        break;
                    case Op.RemoveAll:
                        Assert.Equal(oracle.RemoveAll(x => x.Key == value), sut.RemoveAll((value, -1)));
                        break;
                    case Op.RemoveAt when oracle.Count > 0:
                        int rank = (int)((uint)value % (uint)oracle.Count);
                        Assert.Equal(oracle[rank], sut[rank]);
                        sut.RemoveAt(rank);
                        oracle.RemoveAt(rank);
                        break;
                    case Op.RemoveAt:
                        break;
                    case Op.TrimExcess:
                        sut.TrimExcess();
                        break;
                    case Op.Clear:
                        sut.Clear();
                        oracle.Clear();
                        break;
                }
            }

            AssertEquivalent(sut, oracle);
        }
    }

    [Fact]
    public void RankedMultiSet_ShouldMatch_AStablySortedList_OnTheOrderedSurface()
    {
        Gen.Int[-5, 200].List[0, 2500].Sample(generated =>
        {
            var sut = new RankedMultiSet<int>(generated);
            var oracle = generated.Order().ToList();

            Assert.Equal(oracle, sut.ToArray());

            if (oracle.Count == 0)
            {
                Assert.False(sut.TryGetMin(out _));
                Assert.False(sut.TryGetMax(out _));
                return;
            }

            Assert.Equal(oracle[0], sut.Min);
            Assert.Equal(oracle[^1], sut.Max);

            for (int probe = -6; probe <= 201; probe += 7)
            {
                int less = oracle.Count(x => x < probe);
                int lessOrEqual = oracle.Count(x => x <= probe);

                Assert.Equal(less, sut.CountLessThan(probe));
                Assert.Equal(lessOrEqual, sut.CountLessThanOrEqual(probe));
                Assert.Equal(lessOrEqual - less, sut.CountOf(probe));
                Assert.Equal(oracle.IndexOf(probe), sut.IndexOf(probe));
                Assert.Equal(oracle.LastIndexOf(probe), sut.LastIndexOf(probe));

                Assert.Equal(less < oracle.Count, sut.TryGetLowerBound(probe, out int lower));
                if (less < oracle.Count)
                    Assert.Equal(oracle[less], lower);

                Assert.Equal(lessOrEqual < oracle.Count, sut.TryGetUpperBound(probe, out int upper));
                if (lessOrEqual < oracle.Count)
                    Assert.Equal(oracle[lessOrEqual], upper);

                Assert.Equal(
                    oracle.Where(x => x >= probe && x < probe + 40).ToArray(),
                    sut.EnumerateRange(probe, probe + 40).ToArray());
            }
        }, iter: 40);
    }

    // Content, order of equal keys, and the element at every rank.
    private static void AssertEquivalent(
        RankedMultiSet<(int Key, int Id), ByKey> sut, List<(int Key, int Id)> oracle)
    {
        Assert.Equal(oracle.Count, sut.Count);
        Assert.Equal(oracle, sut.ToArray());

        for (int i = 0; i < oracle.Count; i++)
        {
            Assert.Equal(oracle[i], sut[i]);
            Assert.Equal(LowerBound(oracle, oracle[i].Key), sut.IndexOf(oracle[i]));
            Assert.Equal(UpperBound(oracle, oracle[i].Key) - 1, sut.LastIndexOf(oracle[i]));
        }
    }

    private static int LowerBound(List<(int Key, int Id)> list, int key)
    {
        int lo = 0;
        int hi = list.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (list[mid].Key < key)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    private static int UpperBound(List<(int Key, int Id)> list, int key)
    {
        int lo = 0;
        int hi = list.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (list[mid].Key <= key)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    private readonly struct ByKey : IComparer<(int Key, int Id)>
    {
        public int Compare((int Key, int Id) x, (int Key, int Id) y) => x.Key.CompareTo(y.Key);
    }
}
