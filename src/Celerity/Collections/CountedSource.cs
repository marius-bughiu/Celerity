using System.Runtime.InteropServices;

namespace Celerity.Collections;

/// <summary>
/// The one place a constructor reads a caller's counted collection into storage it sized from that collection's
/// <see cref="ICollection{T}.Count"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ICollection{T}.Count"/> and <see cref="ICollection{T}.CopyTo"/> are two separate calls, and a
/// collection built for concurrent use can change between them. A
/// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/> that loses an entry to another
/// thread in between copies one pair fewer than it counted and leaves the last slot of the destination untouched,
/// and one that gains an entry throws for want of room. A constructor that trusted <c>CopyTo</c> to fill every slot
/// built the leftover <c>default</c> in as an entry the caller never supplied: a <c>(0, null)</c> key in a frozen
/// map, a box at the origin in a spatial index, a trailing <c>0</c> in a range-query tree.
/// <see cref="List{T}(IEnumerable{T})"/> and <c>Enumerable.ToArray</c> share that contract, so going through either
/// does not help.
/// </para>
/// <para>
/// So only an array or a <see cref="List{T}"/> is copied directly: an array's length cannot change, and a list's
/// count and contents can only disagree under a data race on a type that is not thread-safe anyway. The list must
/// be exactly that type, as <see cref="Dictionary{TKey, TValue}"/>'s constructor also requires, because a subclass
/// can re-implement <see cref="ICollection{T}"/> to present other contents than its storage. Any other collection
/// is enumerated once. Its count only sizes the storage, which grows if the enumeration yields more, and the caller
/// keeps exactly what was yielded. For a <c>ConcurrentDictionary</c> that is the weakly consistent view its
/// enumerator documents. In the current implementation it also never yields a key twice: an added key goes to the
/// head of its bucket, behind an enumerator already in that bucket, and a resize leaves a running enumerator on the
/// old table, which is no longer written. So a build from a live one succeeds rather than reporting a duplicate.
/// </para>
/// <para>
/// The price is the enumeration itself: an interface call or two per element, where <c>CopyTo</c> may have been a
/// block copy. It falls only on counted sources that are neither an array nor an exact <see cref="List{T}"/>,
/// which includes a plain subclass of <see cref="List{T}"/>.
/// </para>
/// </remarks>
internal static class CountedSource
{
    /// <summary>
    /// Reads <paramref name="source"/> into <paramref name="buffer"/> from <paramref name="offset"/> on and returns
    /// how many elements it read.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The caller's collection.</param>
    /// <param name="buffer">
    /// Storage the caller sized from <paramref name="source"/>'s <see cref="ICollection{T}.Count"/>, with room for
    /// at least that many elements from <paramref name="offset"/> on. Replaced by a longer copy, its first
    /// <paramref name="offset"/> slots kept, when the source yields more than fits.
    /// </param>
    /// <param name="offset">Where the source's first element goes.</param>
    /// <returns>
    /// The number of elements read. It differs from the count the buffer was sized from only when the source
    /// changed after it was counted, and the slots past it hold nothing the source supplied.
    /// </returns>
    internal static int Read<T>(ICollection<T> source, ref T[] buffer, int offset)
    {
        ReadOnlySpan<T> items;
        if (source is T[] array)
        {
            // A covariant array (a string[] read as an object[]) is safe through a read-only span; only a writable
            // one refuses it.
            items = array;
        }
        else if (source.GetType() == typeof(List<T>))
        {
            items = CollectionsMarshal.AsSpan((List<T>)source);
        }
        else
        {
            return Enumerate(source, ref buffer, offset);
        }

        // The buffer was sized from this same count, so the copy fits; only a data race on the list could make the
        // span copy throw.
        items.CopyTo(buffer.AsSpan(offset));
        return items.Length;
    }

    /// <summary>
    /// Copies <paramref name="source"/> into a new array exactly as long as what it yields: a drop-in for
    /// <c>Enumerable.ToArray</c> that does not trust a collection's <c>CopyTo</c> to fill what its <c>Count</c>
    /// reported. The result is never the caller's own array, so it can be sorted in place.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The caller's sequence.</param>
    /// <returns>The elements of <paramref name="source"/>, in enumeration order.</returns>
    internal static T[] ToArray<T>(IEnumerable<T> source)
    {
        // Anything that is not itself a collection is left to Enumerable.ToArray. A LINQ operator over a concurrent
        // collection (an OrderBy, say) still materializes it by LINQ's own rules, which no constructor can change.
        if (source is not ICollection<T> collection)
            return source.ToArray();

        int count = collection.Count;
        T[] result = count == 0 ? [] : new T[count];
        int read = Read(collection, ref result, 0);
        return read == result.Length ? result : result[..read];
    }

    private static int Enumerate<T>(ICollection<T> source, ref T[] buffer, int offset)
    {
        T[] storage = buffer;
        int position = offset;
        foreach (T item in source)
        {
            if (position == storage.Length)
                storage = Grow(storage, position);

            storage[position++] = item;
        }

        buffer = storage;
        return position - offset;
    }

    // Doubles, as List<T> does, starting from four for an empty buffer, and caps at Array.MaxLength. Past that, the
    // one more slot asked for fails the allocation itself.
    private static T[] Grow<T>(T[] storage, int position)
    {
        int doubled = (int)Math.Min(Math.Max(2L * storage.Length, 4), Array.MaxLength);
        var grown = new T[Math.Max(doubled, position + 1)];
        Array.Copy(storage, grown, position);
        return grown;
    }
}
