using System.Diagnostics.CodeAnalysis;
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
/// thread in between copies one pair fewer than it counted and leaves the last slot of the destination untouched.
/// A constructor that trusted <c>CopyTo</c> to fill every slot would build that leftover <c>default</c> in as an
/// entry the caller never supplied: a <c>(0, null)</c> key in a frozen map, a box at the origin in a spatial
/// index, a trailing <c>0</c> in a range-query tree. <see cref="List{T}(IEnumerable{T})"/> and
/// <c>Enumerable.ToArray</c> share that contract, so going through either does not help.
/// </para>
/// <para>
/// So only an array or a <see cref="List{T}"/> is copied directly. An array's length cannot change, and a list's
/// count and contents can only disagree under a data race on a type that is not thread-safe anyway. Any other
/// collection is enumerated, its count serving only as the size of the storage, and the caller keeps exactly what
/// the enumeration yielded. That is the rule <see cref="Dictionary{TKey, TValue}"/>'s own constructor applies, and
/// the one <see cref="SegmentedList{T}.AddRange"/> follows.
/// </para>
/// <para>
/// A collection that yields more than it counted gained elements after it was counted. There is no slot for the
/// extra ones, and keeping only the first <c>Count</c> could drop elements it held all along, so that throws
/// <see cref="ArgumentException"/> naming the constructor's parameter — the exception its own <c>CopyTo</c> would
/// have thrown for want of room.
/// </para>
/// </remarks>
internal static class CountedSource
{
    /// <summary>
    /// Copies <paramref name="source"/> into <paramref name="destination"/> and returns how many elements it wrote:
    /// <c>destination.Length</c>, unless the source shrank after its count was read.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The caller's collection.</param>
    /// <param name="destination">
    /// Storage the caller sized from <paramref name="source"/>'s <see cref="ICollection{T}.Count"/>. Only the
    /// returned number of elements, from the start, holds anything the source supplied.
    /// </param>
    /// <param name="paramName">The constructor parameter <paramref name="source"/> was passed as.</param>
    /// <returns>The number of elements written.</returns>
    /// <exception cref="ArgumentException"><paramref name="source"/> yields more elements than fit.</exception>
    internal static int CopyTo<T>(ICollection<T> source, Span<T> destination, string paramName)
    {
        // A covariant array (a string[] read as an object[]) is fine here: only Span<T> refuses one, and the array
        // is read through a ReadOnlySpan<T>. Its length is the Count the destination was sized from.
        if (source is T[] array)
        {
            new ReadOnlySpan<T>(array).CopyTo(destination);
            return array.Length;
        }

        // Read through the list's own storage, so a subclass that re-implements ICollection<T> cannot misreport it.
        // Only a data race on the list can make it longer than the destination, and CopyTo then throws.
        if (source is List<T> list)
        {
            ReadOnlySpan<T> items = CollectionsMarshal.AsSpan(list);
            items.CopyTo(destination);
            return items.Length;
        }

        int written = 0;
        foreach (T item in source)
        {
            if (written == destination.Length)
                ThrowYieldedMoreThanCounted(destination.Length, paramName);

            destination[written++] = item;
        }

        return written;
    }

    /// <summary>
    /// Copies <paramref name="source"/> into a new array exactly as long as what it yields. A drop-in for
    /// <c>Enumerable.ToArray</c> that does not trust a counted source's <c>CopyTo</c> to fill what its
    /// <c>Count</c> reported.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The caller's sequence.</param>
    /// <param name="paramName">The parameter <paramref name="source"/> was passed as.</param>
    /// <returns>The elements of <paramref name="source"/>, in enumeration order.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> is a collection that yields more elements than it counted.
    /// </exception>
    internal static T[] ToArray<T>(IEnumerable<T> source, string paramName)
    {
        // Anything that is not itself a collection is left to Enumerable.ToArray. A LINQ operator over a concurrent
        // collection (an OrderBy, say) still materializes it by LINQ's own rules, which no constructor can change.
        if (source is not ICollection<T> collection)
            return source.ToArray();

        int count = collection.Count;
        T[] result = count == 0 ? [] : new T[count];
        int written = CopyTo(collection, result, paramName);
        return written == count ? result : result[..written];
    }

    [DoesNotReturn]
    private static void ThrowYieldedMoreThanCounted(int count, string paramName) =>
        throw new ArgumentException(
            $"The collection yielded more elements than the {count} its Count reported, so it changed while it was being copied.",
            paramName);
}
