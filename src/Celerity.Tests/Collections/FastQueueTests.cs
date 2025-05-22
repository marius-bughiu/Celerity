using Celerity.Collections;

namespace Celerity.Tests.Collections;

public class FastQueueTests
{
    [Fact]
    public void EnqueueDequeue_ShouldReturnItemsInOrder()
    {
        var queue = new FastQueue<int>(2);
        queue.Enqueue(1);
        queue.Enqueue(2);

        Assert.Equal(1, queue.Dequeue());
        Assert.Equal(2, queue.Dequeue());
    }

    [Fact]
    public void Peek_ShouldReturnFirstItemWithoutRemoving()
    {
        var queue = new FastQueue<int>();
        queue.Enqueue(5);
        queue.Enqueue(6);

        Assert.Equal(5, queue.Peek());
        Assert.Equal(5, queue.Dequeue());
    }

    [Fact]
    public void Dequeue_ShouldThrow_WhenQueueIsEmpty()
    {
        var queue = new FastQueue<int>();
        Assert.Throws<InvalidOperationException>(() => queue.Dequeue());
    }

    [Fact]
    public void Queue_ShouldResize_WhenCapacityExceeded()
    {
        var queue = new FastQueue<int>(2);
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3); // triggers resize

        Assert.Equal(3, queue.Count);
        Assert.Equal(1, queue.Dequeue());
        Assert.Equal(2, queue.Dequeue());
        Assert.Equal(3, queue.Dequeue());
    }
}
