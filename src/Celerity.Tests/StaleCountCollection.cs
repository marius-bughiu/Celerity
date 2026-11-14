using System.Collections;

namespace Celerity.Tests;

/// <summary>
/// A counted collection whose <see cref="Count"/> has gone stale, the way a collection another thread is changing
/// (a <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/>, say) can report one count and
/// then copy or enumerate a different number of elements. <see cref="CopyTo"/> behaves as such a collection's does:
/// it writes the elements it actually holds, leaving any slots past them untouched, and throws when the
/// destination has too little room for them.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class StaleCountCollection<T>(int count, params T[] items) : ICollection<T>
{
    public int Count => count;

    public bool IsReadOnly => true;

    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array.Length - arrayIndex < items.Length)
            throw new ArgumentException("Destination array is not long enough.", nameof(array));

        items.CopyTo(array, arrayIndex);
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(T item) => throw new NotSupportedException();

    public void Clear() => throw new NotSupportedException();

    public bool Contains(T item) => throw new NotSupportedException();

    public bool Remove(T item) => throw new NotSupportedException();
}
