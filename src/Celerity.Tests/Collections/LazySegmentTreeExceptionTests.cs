using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

public class LazySegmentTreeExceptionTests
{
    [Fact]
    public void DecimalCompositionOverflow_ShouldFaultTheTreeUntilClear()
    {
        decimal maximum = decimal.MaxValue;
        var tree = new LazySegmentTree<decimal, decimal, MaxMonoid<decimal>, MinMaxAddAction<decimal>>(
            new[] { -maximum, -maximum });
        tree.Apply(0, 2, maximum);

        // The updated root fits, but composing the two parked additions overflows. Previously the root became
        // decimal.MaxValue while the pending update still made both leaves zero.
        Assert.Throws<OverflowException>(() => tree.Apply(0, 2, maximum));

        Assert.Equal(2, tree.Count);
        AssertFaulted(tree);
        tree.Clear();
        Assert.Equal(new[] { decimal.MinValue, decimal.MinValue }, tree);
        Assert.Equal(decimal.MinValue, tree.Aggregate);
        tree.Apply(0, 2, maximum);
        Assert.Equal(new[] { 0m, 0m }, tree);
        Assert.Equal(0m, tree.Aggregate);
    }

    [Theory]
    [InlineData("Apply")]
    [InlineData("IndexerGet")]
    [InlineData("IndexerSet")]
    [InlineData("Query")]
    [InlineData("EmptyQuery")]
    [InlineData("GetEnumerator")]
    public void Operation_ShouldFaultOnEveryCallbackFailure_AndRecoverWithClear(string operation)
    {
        // Count the callbacks on a successful operation, then fail each one in a fresh tree. This reaches failures
        // before and after partial writes, including Apply/Compose, a parent Combine, and both identity getters.
        var counter = new CallbackState();
        var reference = CreateTree(counter);
        counter.Arm(0);
        RunOperation(reference, operation);
        int callbackCount = counter.Calls;
        Assert.True(callbackCount > 0);

        for (int failureAt = 1; failureAt <= callbackCount; failureAt++)
        {
            var callbacks = new CallbackState();
            var tree = CreateTree(callbacks);
            callbacks.Arm(failureAt);

            var exception = Assert.Throws<InjectedException>(() => RunOperation(tree, operation));

            Assert.Same(callbacks.Failure, exception);
            callbacks.Arm(0);
            Assert.Equal(16, tree.Count);
            AssertFaulted(tree);

            tree.Clear();
            Assert.Equal(new int[16], tree);
            Assert.Equal(0, tree.Aggregate);
            tree.Apply(0, 16, 3);
            tree[5] = 7;
            Assert.Equal(52, tree.Aggregate);
            Assert.Equal(52, tree.Query(0, 16));
            Assert.Equal(7, tree[5]);
        }
    }

    [Fact]
    public void FailedRead_ShouldInvalidateExistingEnumerators()
    {
        var callbacks = new CallbackState();
        var tree = CreateTree(callbacks);
        var enumerator = tree.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(4, enumerator.Current);

        // Even an identity push calls the custom action. A failed read must invalidate the already-materialized
        // enumerator, although no successful public mutation has happened since that enumerator was obtained.
        callbacks.Arm(2);
        Assert.Same(callbacks.Failure, Assert.Throws<InjectedException>(() => tree[5]));
        callbacks.Arm(0);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
        tree.Clear();
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Equal(new int[16], tree);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void Clear_ShouldPreserveAHealthyTree_WhenEitherIdentityThrows(int failureAt, bool materialize)
    {
        var callbacks = new CallbackState();
        var tree = CreateTree(callbacks);
        int[] expected = Enumerable.Range(4, 16).ToArray();
        // Test both outstanding pending updates and a live enumerator with fully materialized leaves.
        var enumerator = materialize ? tree.GetEnumerator() : default;
        callbacks.Arm(failureAt);

        Assert.Same(callbacks.Failure, Assert.Throws<InjectedException>(() => tree.Clear()));

        callbacks.Arm(0);
        Assert.Equal(expected.Sum(), tree.Aggregate);
        Assert.Equal(expected, tree);
        if (materialize)
        {
            Assert.True(enumerator.MoveNext());
            Assert.Equal(4, enumerator.Current);
        }

        tree.Apply(0, 16, 1);
        Assert.Equal(expected.Sum() + 16, tree.Aggregate);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Clear_ShouldPreserveTheFaultState_WhenRecoveryIdentityThrows(int failureAt)
    {
        var callbacks = new CallbackState();
        var tree = CreateTree(callbacks);
        callbacks.Arm(1);
        Assert.Throws<InjectedException>(() => tree.Apply(3, 13, 7));

        callbacks.Arm(failureAt);
        Assert.Same(callbacks.Failure, Assert.Throws<InjectedException>(() => tree.Clear()));

        callbacks.Arm(0);
        AssertFaulted(tree);
        tree.Clear();
        Assert.Equal(new int[16], tree);
        Assert.Equal(0, tree.Aggregate);
    }

    [Fact]
    public void InvalidArguments_ShouldNotFaultTheTreeOrInvalidateEnumerators()
    {
        var callbacks = new CallbackState();
        var tree = CreateTree(callbacks);
        var enumerator = tree.GetEnumerator();
        callbacks.Arm(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Apply(-1, 4, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Apply(0, 17, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Apply(4, 3, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Query(-1, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Query(0, 17));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Query(4, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => tree[16] = 1);
        tree.Apply(3, 3, 7);

        Assert.Equal(0, callbacks.Calls);
        callbacks.Arm(0);
        Assert.Equal(184, tree.Aggregate);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(4, enumerator.Current);
        Assert.Equal(Enumerable.Range(4, 16), tree);
    }

    [Fact]
    public void CommonOperations_ShouldAllocateNothing_AfterWarmup()
    {
        var tree = new LazySegmentTree<int, int, SumMonoid<int>, SumAddAction<int>>(32);
        Exercise(tree, 10_000);

        long before = GC.GetAllocatedBytesForCurrentThread();
        int checksum = Exercise(tree, 1_000);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.NotEqual(0, checksum);
    }

    private static int Exercise(LazySegmentTree<int, int, SumMonoid<int>, SumAddAction<int>> tree, int count)
    {
        int checksum = 0;
        for (int i = 0; i < count; i++)
        {
            tree.Apply(3, 29, 1);
            tree[7] = i;
            checksum += tree[7] + tree.Query(5, 24) + tree.Aggregate;
            foreach (int value in tree)
                checksum += value;
            if ((i & 15) == 0)
                tree.Clear();
        }

        return checksum;
    }

    private static LazySegmentTree<int, int, ThrowingSum, ThrowingAdd> CreateTree(CallbackState callbacks)
    {
        var tree = new LazySegmentTree<int, int, ThrowingSum, ThrowingAdd>(
            Enumerable.Range(1, 16).ToArray(), new ThrowingSum(callbacks), new ThrowingAdd(callbacks));
        tree.Apply(0, 16, 3);
        return tree;
    }

    private static void RunOperation(LazySegmentTree<int, int, ThrowingSum, ThrowingAdd> tree, string operation)
    {
        switch (operation)
        {
            case "Apply": tree.Apply(3, 13, 7); break;
            case "IndexerGet": _ = tree[5]; break;
            case "IndexerSet": tree[5] = 55; break;
            case "Query": _ = tree.Query(2, 14); break;
            case "EmptyQuery": _ = tree.Query(3, 3); break;
            case "GetEnumerator": _ = tree.GetEnumerator(); break;
            default: throw new ArgumentException("Unknown test operation.", nameof(operation));
        }
    }

    private static void AssertFaulted<T, TUpdate, TMonoid, TAction>(
        LazySegmentTree<T, TUpdate, TMonoid, TAction> tree)
        where TMonoid : struct, IMonoid<T>
        where TAction : struct, IRangeAction<T, TUpdate>
    {
        Assert.Throws<InvalidOperationException>(() => tree.Aggregate);
        Assert.Throws<InvalidOperationException>(() => tree[0]);
        Assert.Throws<InvalidOperationException>(() => tree[0] = default!);
        Assert.Throws<InvalidOperationException>(() => tree.Apply(0, tree.Count, default!));
        Assert.Throws<InvalidOperationException>(() => tree.Apply(0, 0, default!));
        Assert.Throws<InvalidOperationException>(() => tree.Query(0, tree.Count));
        Assert.Throws<InvalidOperationException>(() => tree.Query(0, 0));
        Assert.Throws<InvalidOperationException>(() => { _ = tree.GetEnumerator(); });
        Assert.Throws<InvalidOperationException>(() => { _ = ((IEnumerable<T>)tree).GetEnumerator(); });
        Assert.Throws<InvalidOperationException>(() => { _ = ((IEnumerable)tree).GetEnumerator(); });
    }

    private sealed class InjectedException : Exception;

    private sealed class CallbackState
    {
        public int Calls { get; private set; }
        public InjectedException Failure { get; } = new();
        private int _failureAt;

        public void Arm(int failureAt)
        {
            Calls = 0;
            _failureAt = failureAt;
        }

        public void Tick()
        {
            if (++Calls == _failureAt)
                throw Failure;
        }
    }

    private readonly struct ThrowingSum(CallbackState callbacks) : IMonoid<int>
    {
        public int Identity
        {
            get
            {
                callbacks.Tick();
                return 0;
            }
        }

        public int Combine(int left, int right)
        {
            callbacks.Tick();
            return left + right;
        }
    }

    private readonly struct ThrowingAdd(CallbackState callbacks) : IRangeAction<int, int>
    {
        public int Identity
        {
            get
            {
                callbacks.Tick();
                return 0;
            }
        }

        public int Compose(int earlier, int later)
        {
            callbacks.Tick();
            return earlier + later;
        }

        public int Apply(int update, int aggregate, int count)
        {
            callbacks.Tick();
            return aggregate + update * count;
        }
    }
}
