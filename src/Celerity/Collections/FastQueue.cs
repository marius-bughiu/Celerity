using System.Diagnostics.CodeAnalysis;

namespace Celerity.Collections;

/// <summary>
/// Provides a high performance queue based on a circular array.
/// </summary>
/// <typeparam name="T">Type of the elements stored in the queue.</typeparam>
public class FastQueue<T>
{
    private const int DEFAULT_CAPACITY = 4;

    private T?[] _items;
    private int _head;
    private int _tail;
    private int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="FastQueue{T}"/> class
    /// with an optional initial capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity of the queue.</param>
    public FastQueue(int capacity = DEFAULT_CAPACITY)
    {
        int size = FastUtils.NextPowerOfTwo(capacity);
        _items = new T?[size];
        _head = 0;
        _tail = 0;
        _count = 0;
    }

    /// <summary>
    /// Gets the number of elements contained in the queue.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Adds an item to the end of the queue.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Enqueue(T item)
    {
        if (_count >= _items.Length)
        {
            Resize();
        }

        _items[_tail] = item;
        _tail = (_tail + 1) & (_items.Length - 1);
        _count++;
    }

    /// <summary>
    /// Removes and returns the item at the beginning of the queue.
    /// </summary>
    /// <returns>The item removed from the queue.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
    public T Dequeue()
    {
        if (_count == 0)
            throw new InvalidOperationException("Queue is empty.");

        T? item = _items[_head];
        _items[_head] = default;
        _head = (_head + 1) & (_items.Length - 1);
        _count--;
        return item!;
    }

    /// <summary>
    /// Returns the item at the beginning of the queue without removing it.
    /// </summary>
    /// <returns>The item at the beginning of the queue.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the queue is empty.</exception>
    [MaybeNull]
    public T Peek()
    {
        if (_count == 0)
            throw new InvalidOperationException("Queue is empty.");

        return _items[_head]!;
    }

    private void Resize()
    {
        int newSize = _items.Length * 2;
        T?[] newItems = new T?[newSize];

        for (int i = 0; i < _count; i++)
        {
            newItems[i] = _items[(_head + i) & (_items.Length - 1)];
        }

        _items = newItems;
        _head = 0;
        _tail = _count;
    }
}
