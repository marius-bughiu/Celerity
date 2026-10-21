using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="LinkedDictionary{TKey, TValue, THasher}"/>
/// against an independent reference: a plain <see cref="List{T}"/> of key/value pairs kept in
/// insertion order and scanned linearly. CsCheck generates the constructor arguments and a stream of
/// operations — indexer set, add, try-add, remove (with and without the out value), move to either
/// end, remove from either end, clear, ensure-capacity and trim-excess — and asserts after every single
/// operation that the two agree on the return value, the count, every key's value, and the exact
/// enumeration order of keys and values.
/// </summary>
/// <remarks>
/// <para>
/// The order is the contract a set-equivalence property cannot see: <c>CollectionModelPropertyTests</c>
/// compares <c>LinkedDictionary</c> to <see cref="Dictionary{TKey, TValue}"/> as a set, and this suite
/// pins where each key sits. The model shares no code with the type under test: it has no hashing, no
/// chains, no free list, just a list and its indices.
/// </para>
/// <para>
/// The capacity and load factor are generated alongside the script because they decide how often the
/// table grows; a load factor as small as 0.01 makes each growth double several times, and
/// <c>TrimExcess</c> / <c>EnsureCapacity</c> rebuild the table mid-script. The key span includes
/// <c>0</c> (the default key, stored in-table under hash 0) and negatives. The colliding variant
/// puts every key in one bucket chain, so every removal walks it.
/// </para>
/// </remarks>
public class LinkedDictionaryDifferentialTests
{
    private enum Op
    {
        Set,
        Add,
        TryAdd,
        Remove,
        RemoveOut,
        MoveToEnd,
        MoveToFront,
        TryRemoveFirst,
        TryRemoveLast,
        Clear,
        TrimExcess,
        EnsureCapacity,
    }

    // A reference ordered map: list order is insertion order.
    private sealed class OrderedModel
    {
        private readonly List<KeyValuePair<int, int>> _entries = new();

        public int Count => _entries.Count;

        public IReadOnlyList<KeyValuePair<int, int>> Entries => _entries;

        private int IndexOf(int key)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Key == key)
                    return i;
            }
            return -1;
        }

        public bool TryGetValue(int key, out int value)
        {
            int i = IndexOf(key);
            value = i >= 0 ? _entries[i].Value : 0;
            return i >= 0;
        }

        public void Set(int key, int value)
        {
            int i = IndexOf(key);
            if (i >= 0)
                _entries[i] = new KeyValuePair<int, int>(key, value); // keeps its position
            else
                _entries.Add(new KeyValuePair<int, int>(key, value));
        }

        public bool TryAdd(int key, int value)
        {
            if (IndexOf(key) >= 0)
                return false;
            _entries.Add(new KeyValuePair<int, int>(key, value));
            return true;
        }

        public bool Remove(int key, out int value)
        {
            int i = IndexOf(key);
            if (i < 0)
            {
                value = 0;
                return false;
            }
            value = _entries[i].Value;
            _entries.RemoveAt(i);
            return true;
        }

        public bool MoveToEnd(int key)
        {
            int i = IndexOf(key);
            if (i < 0)
                return false;
            KeyValuePair<int, int> entry = _entries[i];
            _entries.RemoveAt(i);
            _entries.Add(entry);
            return true;
        }

        public bool MoveToFront(int key)
        {
            int i = IndexOf(key);
            if (i < 0)
                return false;
            KeyValuePair<int, int> entry = _entries[i];
            _entries.RemoveAt(i);
            _entries.Insert(0, entry);
            return true;
        }

        public bool TryRemoveAt(bool first, out int key, out int value)
        {
            if (_entries.Count == 0)
            {
                key = 0;
                value = 0;
                return false;
            }
            int i = first ? 0 : _entries.Count - 1;
            key = _entries[i].Key;
            value = _entries[i].Value;
            _entries.RemoveAt(i);
            return true;
        }

        public void Clear() => _entries.Clear();
    }

    private const int MinKey = -4;
    private const int MaxKey = 20;

    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 99].Select(n =>
            n < 22 ? Op.Set
            : n < 28 ? Op.Add
            : n < 36 ? Op.TryAdd
            : n < 48 ? Op.Remove
            : n < 54 ? Op.RemoveOut
            : n < 64 ? Op.MoveToEnd
            : n < 72 ? Op.MoveToFront
            : n < 80 ? Op.TryRemoveFirst
            : n < 88 ? Op.TryRemoveLast
            : n < 90 ? Op.Clear
            : n < 95 ? Op.TrimExcess
            : Op.EnsureCapacity);

    private static readonly Gen<(Op Kind, int Key, int Value)> GenOp =
        Gen.Select(GenKind, Gen.Int[MinKey, MaxKey], Gen.Int[-1_000, 1_000]);

    private static readonly Gen<float> GenLoadFactor =
        Gen.OneOfConst(0.01f, 0.1f, 0.5f, 0.75f, 0.99f);

    private static readonly Gen<(int Capacity, float LoadFactor, List<(Op Kind, int Key, int Value)> Ops)> GenScript =
        Gen.Select(Gen.Int[0, 32], GenLoadFactor, GenOp.List[0, 200]);

    [Fact]
    public void LinkedDictionary_ShouldMatch_AnOrderedListModel() => RunAgainstModel<Int32WangNaiveHasher>();

    // Every key shares bucket 0, so lookups and removals walk one chain past mismatching entries.
    [Fact]
    public void LinkedDictionary_ShouldMatch_AnOrderedListModel_WhenEveryKeyCollides()
        => RunAgainstModel<CacheCollidingHasher>();

    private static void RunAgainstModel<THasher>()
        where THasher : struct, IHashProvider<int>
    {
        GenScript.Sample(script =>
        {
            var sut = new LinkedDictionary<int, int, THasher>(script.Capacity, script.LoadFactor);
            var model = new OrderedModel();

            foreach (var (kind, key, value) in script.Ops)
            {
                switch (kind)
                {
                    case Op.Set:
                        sut[key] = value;
                        model.Set(key, value);
                        break;

                    case Op.Add:
                        if (model.TryAdd(key, value))
                            sut.Add(key, value);
                        else
                            Assert.Throws<ArgumentException>(() => sut.Add(key, value));
                        break;

                    case Op.TryAdd:
                        Assert.Equal(model.TryAdd(key, value), sut.TryAdd(key, value));
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

                    case Op.MoveToEnd:
                        Assert.Equal(model.MoveToEnd(key), sut.MoveToEnd(key));
                        break;

                    case Op.MoveToFront:
                        Assert.Equal(model.MoveToFront(key), sut.MoveToFront(key));
                        break;

                    case Op.TryRemoveFirst:
                    case Op.TryRemoveLast:
                    {
                        bool first = kind == Op.TryRemoveFirst;
                        bool expected = model.TryRemoveAt(first, out int expectedKey, out int expectedValue);
                        bool actual = first
                            ? sut.TryRemoveFirst(out int actualKey, out int actualValue)
                            : sut.TryRemoveLast(out actualKey, out actualValue);
                        Assert.Equal(expected, actual);
                        Assert.Equal(expectedKey, actualKey);
                        Assert.Equal(expectedValue, actualValue);
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
                        // Ask for room beyond the current count; the value is the generated
                        // non-negative magnitude, so it sometimes grows and sometimes is a no-op.
                        int requested = model.Count + Math.Abs(value) % 64;
                        Assert.True(sut.EnsureCapacity(requested) >= requested);
                        break;
                    }
                }

                AssertEquivalent(sut, model);
            }
        }, iter: 300);
    }

    private static void AssertEquivalent<THasher>(LinkedDictionary<int, int, THasher> sut, OrderedModel model)
        where THasher : struct, IHashProvider<int>
    {
        Assert.Equal(model.Count, sut.Count);

        // The exact order — the contract this suite exists for.
        var actual = new List<KeyValuePair<int, int>>(sut.Count);
        foreach (var kvp in sut)
            actual.Add(kvp);
        Assert.Equal(model.Entries, actual);

        // Every key in the span, present or not.
        for (int k = MinKey; k <= MaxKey; k++)
        {
            bool expected = model.TryGetValue(k, out int expectedValue);
            Assert.Equal(expected, sut.TryGetValue(k, out int actualValue));
            Assert.Equal(expectedValue, actualValue);
        }

        // Both ends agree with the model.
        bool any = model.Count > 0;
        Assert.Equal(any, sut.TryGetFirst(out int firstKey, out int firstValue));
        Assert.Equal(any, sut.TryGetLast(out int lastKey, out int lastValue));
        if (any)
        {
            Assert.Equal(model.Entries[0], new KeyValuePair<int, int>(firstKey, firstValue));
            Assert.Equal(model.Entries[^1], new KeyValuePair<int, int>(lastKey, lastValue));
        }
    }
}
