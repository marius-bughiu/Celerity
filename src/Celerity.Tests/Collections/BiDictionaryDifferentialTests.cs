using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for
/// <see cref="BiDictionary{TKey, TValue, TKeyHasher, TValueHasher}"/> against an independent reference:
/// the hand-rolled pair of <see cref="Dictionary{TKey, TValue}"/> instances the type replaces, one per
/// direction, plus a <see cref="List{T}"/> of keys for the documented enumeration order. CsCheck
/// generates the constructor arguments and a stream of operations — indexer set, add, try-add,
/// force-set, remove and remove-value (with and without the out argument), clear, ensure-capacity and
/// trim-excess — and asserts after every single operation that the two agree on the return value, the
/// count, every key's value, every value's key, and the exact enumeration order.
/// </summary>
/// <remarks>
/// <para>
/// The model shares no code with the type under test: it has no chains and no bucket arrays, just two
/// dictionaries kept in sync by hand and a list. The list encodes the order contract — a new key is
/// appended, a removal moves the last key into the removed one's position, and a rebind keeps a key
/// where it is — which a set-equivalence check cannot see.
/// </para>
/// <para>
/// The key and value spans are small and overlapping so that value conflicts — the indexer refusing a
/// value another key owns, <c>ForceSet</c> evicting that key — happen on most scripts. Both spans
/// include <c>0</c> (the default key and value, stored in-table under hash 0) and negatives. The load
/// factor is generated because it decides how often the table grows; at 0.01 each growth doubles
/// several times. The colliding variant puts every key in one key chain and every value in one value
/// chain, so every removal and every swap-last move walks them.
/// </para>
/// </remarks>
public class BiDictionaryDifferentialTests
{
    private enum Op
    {
        Set,
        Add,
        TryAdd,
        ForceSet,
        Remove,
        RemoveOut,
        RemoveValue,
        RemoveValueOut,
        Clear,
        TrimExcess,
        EnsureCapacity,
    }

    // The dictionary pair a caller would otherwise maintain by hand, plus the enumeration order.
    private sealed class PairModel
    {
        public readonly Dictionary<int, int> Forward = new();
        public readonly Dictionary<int, int> Inverse = new();
        public readonly List<int> Order = new();

        public int Count => Forward.Count;

        private void Append(int key, int value)
        {
            Forward[key] = value;
            Inverse[value] = key;
            Order.Add(key);
        }

        private void Rebind(int key, int value)
        {
            Inverse.Remove(Forward[key]);
            Forward[key] = value;
            Inverse[value] = key;
        }

        private void RemoveEntry(int key)
        {
            Inverse.Remove(Forward[key]);
            Forward.Remove(key);
            int i = Order.IndexOf(key);
            Order[i] = Order[^1];
            Order.RemoveAt(Order.Count - 1);
        }

        // Returns null when the indexer must throw (the value belongs to a different key).
        public bool? TrySet(int key, int value)
        {
            if (Inverse.TryGetValue(value, out int owner))
                return owner == key ? true : null;
            if (Forward.ContainsKey(key))
                Rebind(key, value);
            else
                Append(key, value);
            return true;
        }

        public bool TryAdd(int key, int value)
        {
            if (Forward.ContainsKey(key) || Inverse.ContainsKey(value))
                return false;
            Append(key, value);
            return true;
        }

        public bool ForceSet(int key, int value)
        {
            bool evicted = false;
            if (Inverse.TryGetValue(value, out int owner))
            {
                if (owner == key)
                    return false;
                RemoveEntry(owner);
                evicted = true;
            }
            if (Forward.ContainsKey(key))
                Rebind(key, value);
            else
                Append(key, value);
            return evicted;
        }

        public bool Remove(int key, out int value)
        {
            if (!Forward.TryGetValue(key, out value))
                return false;
            RemoveEntry(key);
            return true;
        }

        public bool RemoveValue(int value, out int key)
        {
            if (!Inverse.TryGetValue(value, out key))
                return false;
            RemoveEntry(key);
            return true;
        }

        public void Clear()
        {
            Forward.Clear();
            Inverse.Clear();
            Order.Clear();
        }
    }

    private const int MinItem = -4;
    private const int MaxItem = 16;

    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 99].Select(n =>
            n < 22 ? Op.Set
            : n < 28 ? Op.Add
            : n < 36 ? Op.TryAdd
            : n < 52 ? Op.ForceSet
            : n < 62 ? Op.Remove
            : n < 68 ? Op.RemoveOut
            : n < 78 ? Op.RemoveValue
            : n < 84 ? Op.RemoveValueOut
            : n < 86 ? Op.Clear
            : n < 93 ? Op.TrimExcess
            : Op.EnsureCapacity);

    private static readonly Gen<(Op Kind, int Key, int Value)> GenOp =
        Gen.Select(GenKind, Gen.Int[MinItem, MaxItem], Gen.Int[MinItem, MaxItem]);

    private static readonly Gen<float> GenLoadFactor =
        Gen.OneOfConst(0.01f, 0.1f, 0.5f, 0.75f, 0.99f);

    private static readonly Gen<(int Capacity, float LoadFactor, List<(Op Kind, int Key, int Value)> Ops)> GenScript =
        Gen.Select(Gen.Int[0, 32], GenLoadFactor, GenOp.List[0, 200]);

    [Fact]
    public void BiDictionary_ShouldMatch_ADictionaryPairModel()
        => RunAgainstModel<Int32WangNaiveHasher, Int32WangNaiveHasher>();

    // Every key shares key bucket 0 and every value shares value bucket 0, so lookups, removals and
    // swap-last moves walk one chain per side past mismatching entries.
    [Fact]
    public void BiDictionary_ShouldMatch_ADictionaryPairModel_WhenEverythingCollides()
        => RunAgainstModel<CacheCollidingHasher, CacheCollidingHasher>();

    // Collisions on one side only: the other side's chains stay short.
    [Fact]
    public void BiDictionary_ShouldMatch_ADictionaryPairModel_WhenOnlyKeysCollide()
        => RunAgainstModel<CacheCollidingHasher, Int32WangNaiveHasher>();

    [Fact]
    public void BiDictionary_ShouldMatch_ADictionaryPairModel_WhenOnlyValuesCollide()
        => RunAgainstModel<Int32WangNaiveHasher, CacheCollidingHasher>();

    private static void RunAgainstModel<TKeyHasher, TValueHasher>()
        where TKeyHasher : struct, IHashProvider<int>
        where TValueHasher : struct, IHashProvider<int>
    {
        GenScript.Sample(script =>
        {
            var sut = new BiDictionary<int, int, TKeyHasher, TValueHasher>(script.Capacity, script.LoadFactor);
            var model = new PairModel();

            foreach (var (kind, key, value) in script.Ops)
            {
                switch (kind)
                {
                    case Op.Set:
                        if (model.TrySet(key, value) is null)
                        {
                            var ex = Assert.Throws<ArgumentException>(() => sut[key] = value);
                            Assert.Equal("value", ex.ParamName);
                        }
                        else
                        {
                            sut[key] = value;
                        }
                        break;

                    case Op.Add:
                    {
                        bool keyTaken = model.Forward.ContainsKey(key);
                        bool valueTaken = model.Inverse.ContainsKey(value);
                        if (model.TryAdd(key, value))
                        {
                            sut.Add(key, value);
                        }
                        else
                        {
                            var ex = Assert.Throws<ArgumentException>(() => sut.Add(key, value));
                            Assert.Equal(keyTaken ? "key" : "value", ex.ParamName);
                            Assert.True(keyTaken || valueTaken);
                        }
                        break;
                    }

                    case Op.TryAdd:
                        Assert.Equal(model.TryAdd(key, value), sut.TryAdd(key, value));
                        break;

                    case Op.ForceSet:
                        Assert.Equal(model.ForceSet(key, value), sut.ForceSet(key, value));
                        break;

                    case Op.Remove:
                        Assert.Equal(model.Remove(key, out _), sut.Remove(key));
                        break;

                    case Op.RemoveOut:
                    {
                        bool expected = model.Remove(key, out int expectedValue);
                        Assert.Equal(expected, sut.Remove(key, out int actualValue));
                        Assert.Equal(expectedValue, actualValue);
                        break;
                    }

                    case Op.RemoveValue:
                        Assert.Equal(model.RemoveValue(value, out _), sut.RemoveValue(value));
                        break;

                    case Op.RemoveValueOut:
                    {
                        bool expected = model.RemoveValue(value, out int expectedKey);
                        Assert.Equal(expected, sut.RemoveValue(value, out int actualKey));
                        Assert.Equal(expectedKey, actualKey);
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
                        // Ask for room beyond the current count; the generated value's magnitude
                        // decides whether it grows or is a no-op.
                        int requested = model.Count + Math.Abs(value) * 4;
                        Assert.True(sut.EnsureCapacity(requested) >= requested);
                        break;
                    }
                }

                AssertEquivalent(sut, model);
            }
        }, iter: 300);
    }

    private static void AssertEquivalent<TKeyHasher, TValueHasher>(
        BiDictionary<int, int, TKeyHasher, TValueHasher> sut, PairModel model)
        where TKeyHasher : struct, IHashProvider<int>
        where TValueHasher : struct, IHashProvider<int>
    {
        Assert.Equal(model.Count, sut.Count);
        Assert.Equal(model.Inverse.Count, sut.Inverse.Count);

        // The exact order, in both directions.
        var actualKeys = new List<int>(sut.Count);
        foreach (var kvp in sut)
        {
            actualKeys.Add(kvp.Key);
            Assert.Equal(model.Forward[kvp.Key], kvp.Value);
        }
        Assert.Equal(model.Order, actualKeys);

        var inverseKeys = new List<int>(sut.Count);
        foreach (var kvp in sut.Inverse)
        {
            inverseKeys.Add(kvp.Value);
            Assert.Equal(model.Inverse[kvp.Key], kvp.Value);
        }
        Assert.Equal(model.Order, inverseKeys);

        // Every item in the span, present or not, looked up from both sides.
        for (int x = MinItem; x <= MaxItem; x++)
        {
            bool expected = model.Forward.TryGetValue(x, out int expectedValue);
            Assert.Equal(expected, sut.TryGetValue(x, out int actualValue));
            Assert.Equal(expectedValue, actualValue);

            expected = model.Inverse.TryGetValue(x, out int expectedKey);
            Assert.Equal(expected, sut.TryGetKey(x, out int actualKey));
            Assert.Equal(expectedKey, actualKey);
            Assert.Equal(expected, sut.ContainsValue(x));
        }
    }
}
