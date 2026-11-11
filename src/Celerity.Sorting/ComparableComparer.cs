namespace Celerity.Sorting;

/// <summary>
/// The natural order as a struct comparer, so the <see cref="IComparable{T}"/> overloads share
/// one devirtualized implementation with the comparer-parameterized ones. Nulls sort first,
/// matching <see cref="Comparer{T}.Default"/>.
/// </summary>
internal readonly struct ComparableComparer<T> : IComparer<T>
    where T : IComparable<T>
{
    public int Compare(T? x, T? y)
    {
        if (x is null)
        {
            return y is null ? 0 : -1;
        }

        return y is null ? 1 : x.CompareTo(y);
    }
}
