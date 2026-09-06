using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="Deque{T}"/> against an independent reference
/// deque built from a <see cref="List{T}"/> (front = index 0). CsCheck generates the starting
/// capacity and the stream of end operations — push/pop front and back, peeks, index reads and the
/// occasional clear — and asserts after every single operation that the two agree on count, on the
/// exact front-to-back element sequence, on both ends, and on an index read. This is the strongest
/// guard against a wrap-around or growth-re-linearization bug in the circular buffer that only
/// surfaces after many mixed operations.
///
/// <para>
/// The starting capacity is generated rather than enumerated because it decides where the first
/// wrap lands: capacity 0 and 1 reach the growth path immediately, and a small odd capacity puts
/// the boundary in the middle of the operation stream instead of at a round number.
/// </para>
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

    private enum Op { PushFront, PushBack, PopFront, PopBack, IndexRead, Clear }

    // Pushes outnumber pops so the deque grows and wraps rather than hovering near empty, and the
    // clear stays rare for the same reason.
    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 79].Select(n => n < 20 ? Op.PushFront
                                 : n < 40 ? Op.PushBack
                                 : n < 50 ? Op.PopFront
                                 : n < 60 ? Op.PopBack
                                 : n < 79 ? Op.IndexRead
                                 : Op.Clear);

    private static readonly Gen<(Op Kind, int Value, int Index)> GenOp =
        Gen.Select(GenKind, Gen.Int, Gen.Int[0, 1023]);

    private static readonly Gen<(int StartCapacity, List<(Op Kind, int Value, int Index)> Ops)> GenScript =
        Gen.Select(Gen.Int[0, 4], GenOp.List[0, 400]);

    [Fact]
    public void Deque_ShouldMatch_AReferenceDeque()
    {
        GenScript.Sample(script =>
        {
            var deque = new Deque<int>(script.StartCapacity);
            var oracle = new OracleDeque();

            foreach (var (kind, value, index) in script.Ops)
            {
                switch (kind)
                {
                    case Op.PushFront:
                        deque.PushFront(value);
                        oracle.PushFront(value);
                        break;

                    case Op.PushBack:
                        deque.PushBack(value);
                        oracle.PushBack(value);
                        break;

                    case Op.PopFront:
                        // The peek is asserted first, including its false on empty, so a pop that
                        // returns the right element from the wrong end is still caught.
                        Assert.Equal(oracle.Count > 0, deque.TryPeekFront(out int front));
                        if (oracle.Count > 0)
                        {
                            Assert.Equal(oracle[0], front);
                            Assert.Equal(oracle.PopFront(), deque.PopFront());
                        }
                        break;

                    case Op.PopBack:
                        Assert.Equal(oracle.Count > 0, deque.TryPeekBack(out int back));
                        if (oracle.Count > 0)
                        {
                            Assert.Equal(oracle[oracle.Count - 1], back);
                            Assert.Equal(oracle.PopBack(), deque.PopBack());
                        }
                        break;

                    case Op.IndexRead when oracle.Count > 0:
                    {
                        int i = index % oracle.Count;
                        Assert.Equal(oracle[i], deque[i]);
                        break;
                    }

                    case Op.IndexRead:
                        break;

                    case Op.Clear:
                        deque.Clear();
                        oracle.Clear();
                        break;
                }

                // Full-state agreement after every operation.
                Assert.Equal(oracle.Count, deque.Count);
                Assert.Equal(oracle.ToArray(), deque.ToArray());
            }
        }, iter: 40);
    }
}
