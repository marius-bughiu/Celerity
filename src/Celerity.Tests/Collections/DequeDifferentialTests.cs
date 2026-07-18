using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Deterministic, seeded differential coverage for <see cref="Deque{T}"/>. Each seed drives the same random
/// stream of end operations (push/pop front and back, plus index reads and the occasional clear) into the
/// deque and into an independent reference deque built from a <see cref="List{T}"/> (front = index 0), then
/// asserts that after every single operation the two agree on count, the exact front-to-back element
/// sequence, both ends, and a random index read. This is the strongest guard against a wrap-around or
/// growth-re-linearization bug in the circular buffer that only surfaces after many mixed operations.
/// </summary>
public class DequeDifferentialTests
{
    // A reference deque with textbook semantics over a List<T>: index 0 is the front, index Count-1 the back.
    private sealed class OracleDeque
    {
        private readonly List<int> _items = new();

        public int Count => _items.Count;

        public void PushFront(int value) => _items.Insert(0, value);

        public void PushBack(int value) => _items.Add(value);

        public int PopFront()
        {
            int v = _items[0];
            _items.RemoveAt(0);
            return v;
        }

        public int PopBack()
        {
            int v = _items[^1];
            _items.RemoveAt(_items.Count - 1);
            return v;
        }

        public int this[int index] => _items[index];

        public void Clear() => _items.Clear();

        public int[] ToArray() => _items.ToArray();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void RandomOps_MatchReferenceDeque(int startCapacity)
    {
        const int Seeds = 40;
        const int OpsPerSeed = 500;

        for (int seed = 0; seed < Seeds; seed++)
        {
            var rng = new Random(seed * 6151 + startCapacity);
            var deque = new Deque<int>(startCapacity);
            var oracle = new OracleDeque();

            for (int op = 0; op < OpsPerSeed; op++)
            {
                int value = rng.Next();
                switch (rng.Next(8))
                {
                    case 0:
                    case 1:
                        deque.PushFront(value);
                        oracle.PushFront(value);
                        break;
                    case 2:
                    case 3:
                        deque.PushBack(value);
                        oracle.PushBack(value);
                        break;
                    case 4:
                        Assert.Equal(oracle.Count > 0, deque.TryPeekFront(out int pf));
                        if (oracle.Count > 0)
                        {
                            Assert.Equal(oracle[0], pf);
                            Assert.Equal(oracle.PopFront(), deque.PopFront());
                        }
                        break;
                    case 5:
                        Assert.Equal(oracle.Count > 0, deque.TryPeekBack(out int pb));
                        if (oracle.Count > 0)
                        {
                            Assert.Equal(oracle[oracle.Count - 1], pb);
                            Assert.Equal(oracle.PopBack(), deque.PopBack());
                        }
                        break;
                    case 6:
                        if (oracle.Count > 0)
                        {
                            int idx = rng.Next(oracle.Count);
                            Assert.Equal(oracle[idx], deque[idx]);
                        }
                        break;
                    default:
                        // Occasional clear (rarely, so the deque usually keeps growing/wrapping).
                        if (rng.Next(10) == 0)
                        {
                            deque.Clear();
                            oracle.Clear();
                        }
                        break;
                }

                // Full-state agreement after every operation.
                Assert.Equal(oracle.Count, deque.Count);
                Assert.Equal(oracle.ToArray(), deque.ToArray());
            }
        }
    }
}
