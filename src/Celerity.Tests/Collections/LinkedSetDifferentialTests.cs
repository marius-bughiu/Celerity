using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="LinkedSet{T, THasher}"/> against an
/// independent reference: a plain <see cref="List{T}"/> kept in insertion order and scanned linearly.
/// CsCheck generates the constructor arguments and a stream of operations — add, try-add, remove, move
/// to either end, remove from either end, clear, ensure-capacity, trim-excess and the four mutating
/// set-algebra operations — and asserts after every single operation that the two agree on the return
/// value, the count, every element's membership, and the exact enumeration order.
/// </summary>
/// <remarks>
/// <para>
/// The order is the contract a set-equivalence property cannot see: <c>CollectionModelPropertyTests</c>
/// compares <c>LinkedSet</c> to <see cref="HashSet{T}"/> as a set, and this suite pins where each
/// element sits. The model shares no code with the type under test: it has no hashing, no chains, no
/// free list, just a list and its indices.
/// </para>
/// <para>
/// The capacity and load factor are generated alongside the script because they decide how often the
/// table grows; a load factor as small as 0.01 makes each growth double several times, and
/// <c>TrimExcess</c> / <c>EnsureCapacity</c> rebuild the table mid-script. The element span includes
/// <c>0</c> (the default element, stored in-table under hash 0) and negatives. The colliding variant
/// puts every element in one bucket chain, so every removal walks it.
/// </para>
/// </remarks>
public class LinkedSetDifferentialTests
{
    private enum Op
    {
        Add,
        TryAdd,
        Remove,
        MoveToEnd,
        MoveToFront,
        TryRemoveFirst,
        TryRemoveLast,
        Clear,
        TrimExcess,
        EnsureCapacity,
        UnionWith,
        IntersectWith,
        ExceptWith,
        SymmetricExceptWith,
    }

    // A reference ordered set: list order is insertion order.
    private sealed class OrderedModel
    {
        private readonly List<int> _items = new();

        public int Count => _items.Count;

        public IReadOnlyList<int> Items => _items;

        public bool Contains(int item) => _items.Contains(item);

        public bool TryAdd(int item)
        {
            if (_items.Contains(item))
                return false;
            _items.Add(item);
            return true;
        }

        public bool Remove(int item) => _items.Remove(item);

        public bool MoveToEnd(int item)
        {
            if (!_items.Remove(item))
                return false;
            _items.Add(item);
            return true;
        }

        public bool MoveToFront(int item)
        {
            if (!_items.Remove(item))
                return false;
            _items.Insert(0, item);
            return true;
        }

        public bool TryRemoveAt(bool first, out int item)
        {
            if (_items.Count == 0)
            {
                item = 0;
                return false;
            }
            int i = first ? 0 : _items.Count - 1;
            item = _items[i];
            _items.RemoveAt(i);
            return true;
        }

        public void Clear() => _items.Clear();

        // New elements append in other's first-occurrence order.
        public void UnionWith(IEnumerable<int> other)
        {
            foreach (int item in other)
                TryAdd(item);
        }

        // Survivors keep their order.
        public void IntersectWith(IEnumerable<int> other)
        {
            var keep = new List<int>(other);
            _items.RemoveAll(item => !keep.Contains(item));
        }

        public void ExceptWith(IEnumerable<int> other)
        {
            var drop = new List<int>(other);
            _items.RemoveAll(drop.Contains);
        }

        // Survivors keep their order; the elements only other held follow, in other's
        // first-occurrence order.
        public void SymmetricExceptWith(IEnumerable<int> other)
        {
            var distinct = new List<int>();
            foreach (int item in other)
            {
                if (!distinct.Contains(item))
                    distinct.Add(item);
            }
            foreach (int item in distinct)
            {
                if (!_items.Remove(item))
                    _items.Add(item);
            }
        }
    }

    private const int MinItem = -4;
    private const int MaxItem = 20;

    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 99].Select(n =>
            n < 18 ? Op.Add
            : n < 30 ? Op.TryAdd
            : n < 44 ? Op.Remove
            : n < 52 ? Op.MoveToEnd
            : n < 60 ? Op.MoveToFront
            : n < 67 ? Op.TryRemoveFirst
            : n < 74 ? Op.TryRemoveLast
            : n < 76 ? Op.Clear
            : n < 80 ? Op.TrimExcess
            : n < 84 ? Op.EnsureCapacity
            : n < 88 ? Op.UnionWith
            : n < 92 ? Op.IntersectWith
            : n < 96 ? Op.ExceptWith
            : Op.SymmetricExceptWith);

    private static readonly Gen<int> GenItem = Gen.Int[MinItem, MaxItem];

    private static readonly Gen<(Op Kind, int Item, int Magnitude, int[] Other)> GenOp =
        Gen.Select(GenKind, GenItem, Gen.Int[0, 1_000], GenItem.Array[0, 8]);

    private static readonly Gen<float> GenLoadFactor =
        Gen.OneOfConst(0.01f, 0.1f, 0.5f, 0.75f, 0.99f);

    private static readonly Gen<(int Capacity, float LoadFactor, int[] Source, List<(Op Kind, int Item, int Magnitude, int[] Other)> Ops)> GenScript =
        Gen.Select(Gen.Int[0, 32], GenLoadFactor, GenItem.Array[0, 12], GenOp.List[0, 200]);

    [Fact]
    public void LinkedSet_ShouldMatch_AnOrderedListModel() => RunAgainstModel<Int32WangNaiveHasher>();

    // Every element shares bucket 0, so lookups and removals walk one chain past mismatching entries.
    [Fact]
    public void LinkedSet_ShouldMatch_AnOrderedListModel_WhenEveryElementCollides()
        => RunAgainstModel<CacheCollidingHasher>();

    private static void RunAgainstModel<THasher>()
        where THasher : struct, IHashProvider<int>
    {
        GenScript.Sample(script =>
        {
            // Half the scripts start from the source constructor, which dedups keeping first occurrences.
            var sut = script.Source.Length % 2 == 0
                ? new LinkedSet<int, THasher>(script.Capacity, script.LoadFactor)
                : new LinkedSet<int, THasher>(script.Source, script.Capacity, script.LoadFactor);
            var model = new OrderedModel();
            if (script.Source.Length % 2 != 0)
                model.UnionWith(script.Source);
            AssertEquivalent(sut, model);

            foreach (var (kind, item, magnitude, other) in script.Ops)
            {
                switch (kind)
                {
                    case Op.Add:
                        if (model.TryAdd(item))
                            sut.Add(item);
                        else
                            Assert.Throws<ArgumentException>(() => sut.Add(item));
                        break;

                    case Op.TryAdd:
                        Assert.Equal(model.TryAdd(item), sut.TryAdd(item));
                        break;

                    case Op.Remove:
                        Assert.Equal(model.Remove(item), sut.Remove(item));
                        break;

                    case Op.MoveToEnd:
                        Assert.Equal(model.MoveToEnd(item), sut.MoveToEnd(item));
                        break;

                    case Op.MoveToFront:
                        Assert.Equal(model.MoveToFront(item), sut.MoveToFront(item));
                        break;

                    case Op.TryRemoveFirst:
                    case Op.TryRemoveLast:
                    {
                        bool first = kind == Op.TryRemoveFirst;
                        bool expected = model.TryRemoveAt(first, out int expectedItem);
                        bool actual = first
                            ? sut.TryRemoveFirst(out int actualItem)
                            : sut.TryRemoveLast(out actualItem);
                        Assert.Equal(expected, actual);
                        Assert.Equal(expectedItem, actualItem);
                        break;
                    }

                    case Op.Clear:
                        sut.Clear();
                        model.Clear();
                        break;

                    case Op.TrimExcess:
                        sut.TrimExcess();
                        Assert.True(sut.EnsureCapacity(0) >= model.Count);
                        break;

                    case Op.EnsureCapacity:
                    {
                        // Ask for room beyond the current count; it sometimes grows and sometimes is a no-op.
                        int requested = model.Count + magnitude % 64;
                        Assert.True(sut.EnsureCapacity(requested) >= requested);
                        break;
                    }

                    case Op.UnionWith:
                        sut.UnionWith(other);
                        model.UnionWith(other);
                        break;

                    case Op.IntersectWith:
                        sut.IntersectWith(other);
                        model.IntersectWith(other);
                        break;

                    case Op.ExceptWith:
                        sut.ExceptWith(other);
                        model.ExceptWith(other);
                        break;

                    case Op.SymmetricExceptWith:
                        sut.SymmetricExceptWith(other);
                        model.SymmetricExceptWith(other);
                        break;
                }

                AssertEquivalent(sut, model);
            }
        }, iter: 300);
    }

    private static void AssertEquivalent<THasher>(LinkedSet<int, THasher> sut, OrderedModel model)
        where THasher : struct, IHashProvider<int>
    {
        Assert.Equal(model.Count, sut.Count);

        // The exact order — the contract this suite exists for.
        var actual = new List<int>(sut.Count);
        foreach (int item in sut)
            actual.Add(item);
        Assert.Equal(model.Items, actual);

        // CopyTo walks the same order.
        var copied = new int[sut.Count];
        sut.CopyTo(copied, 0);
        Assert.Equal(model.Items, copied);

        // Every element in the span, present or not.
        for (int k = MinItem; k <= MaxItem; k++)
            Assert.Equal(model.Contains(k), sut.Contains(k));

        // Both ends agree with the model.
        bool any = model.Count > 0;
        Assert.Equal(any, sut.TryGetFirst(out int first));
        Assert.Equal(any, sut.TryGetLast(out int last));
        if (any)
        {
            Assert.Equal(model.Items[0], first);
            Assert.Equal(model.Items[^1], last);
        }
    }
}
