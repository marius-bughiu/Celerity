using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="AggregatingQueue{T, TMonoid}"/> against a <see cref="Queue{T}"/> that
/// re-folds its whole contents on every read — the <c>O(w)</c> answer the type exists to replace.
///
/// <para>
/// What is on trial is the two-stacks bookkeeping: the boundary between the front (elements with stored suffix
/// folds) and the back (one running fold), the flip that moves the boundary, and the ring growth that has to carry
/// both arrays across a wrap. Every check runs on a <b>non-commutative</b> fold — <see cref="ConcatMonoid"/> and
/// <see cref="FirstNonZeroMonoid"/> — because a commutative one cannot tell whether front and back were combined
/// in the right order, or whether a suffix fold was built backwards.
/// </para>
/// </summary>
public class AggregatingQueueDifferentialTests
{
    private enum Op { Enqueue, Dequeue, TryDequeue, Peek, Clear }

    // Enqueues slightly outnumber dequeues so the queue drifts upward and the ring grows across a wrap; a clear is
    // rare enough that long interleavings survive.
    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 99].Select(n => n < 50 ? Op.Enqueue
                                 : n < 75 ? Op.Dequeue
                                 : n < 88 ? Op.TryDequeue
                                 : n < 98 ? Op.Peek
                                 : Op.Clear);

    private static readonly Gen<(int Capacity, int[] Seed, List<(Op Kind, int Value)> Ops)> GenScript =
        Gen.Select(Gen.Int[0, 9], Gen.Int[-3, 3].Array[0, 12], Gen.Select(GenKind, Gen.Int[-3, 3]).List[0, 400]);

    [Fact]
    public void AggregatingQueue_ShouldMatch_ARefoldedQueue_UnderConcatenation()
    {
        GenScript.Sample(script =>
        {
            // Small tokens keep the strings short; each value maps to a distinct letter so order is visible.
            static string Token(int v) => ((char)('d' + v)).ToString();

            var queue = script.Capacity == 0
                ? new AggregatingQueue<string, ConcatMonoid>(script.Seed.Select(Token))
                : new AggregatingQueue<string, ConcatMonoid>(script.Capacity);
            var model = new Queue<string>();
            if (script.Capacity == 0)
            {
                foreach (int v in script.Seed)
                    model.Enqueue(Token(v));
            }

            Run(queue, model, script.Ops.Select(o => (o.Kind, Token(o.Value))), m => string.Concat(m));
        });
    }

    [Fact]
    public void AggregatingQueue_ShouldMatch_ARefoldedQueue_UnderFirstNonZero()
    {
        GenScript.Sample(script =>
        {
            var queue = new AggregatingQueue<int, FirstNonZeroMonoid>(script.Seed);
            var model = new Queue<int>(script.Seed);

            Run(queue, model, script.Ops.Select(o => (o.Kind, o.Value)), m => m.FirstOrDefault(v => v != 0));
        });
    }

    private static void Run<T, TMonoid>(
        AggregatingQueue<T, TMonoid> queue,
        Queue<T> model,
        IEnumerable<(Op Kind, T Value)> ops,
        Func<Queue<T>, T> refold)
        where TMonoid : struct, IMonoid<T>
    {
        AssertConsistent(queue, model, refold);

        foreach (var (kind, value) in ops)
        {
            switch (kind)
            {
                case Op.Enqueue:
                    queue.Enqueue(value);
                    model.Enqueue(value);
                    break;

                case Op.Dequeue:
                    if (model.Count == 0)
                    {
                        Assert.Throws<InvalidOperationException>(() => queue.Dequeue());
                    }
                    else
                    {
                        Assert.Equal(model.Dequeue(), queue.Dequeue());
                    }

                    break;

                case Op.TryDequeue:
                    Assert.Equal(model.TryDequeue(out T? expected), queue.TryDequeue(out T? actual));
                    Assert.Equal(expected, actual);
                    break;

                case Op.Peek:
                    Assert.Equal(model.TryPeek(out T? head), queue.TryPeek(out T? peeked));
                    Assert.Equal(head, peeked);
                    break;

                case Op.Clear:
                    queue.Clear();
                    model.Clear();
                    break;
            }

            AssertConsistent(queue, model, refold);
        }
    }

    private static void AssertConsistent<T, TMonoid>(
        AggregatingQueue<T, TMonoid> queue, Queue<T> model, Func<Queue<T>, T> refold)
        where TMonoid : struct, IMonoid<T>
    {
        Assert.Equal(model.Count, queue.Count);
        Assert.Equal(refold(model), queue.Aggregate);
        Assert.Equal(model, queue);

        // The ring is a power of two that holds everything.
        Assert.True(queue.Capacity == 0 || (queue.Capacity & (queue.Capacity - 1)) == 0);
        Assert.True(queue.Capacity >= queue.Count);
    }
}
