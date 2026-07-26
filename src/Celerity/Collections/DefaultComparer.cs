namespace Celerity.Collections;

/// <summary>
/// The zero-cost default ordering: a <b>struct</b> <see cref="IComparer{T}"/> that forwards to
/// <see cref="Comparer{T}.Default"/>. It is to the ordered collections what the struct hashers are to the
/// hashed ones — because it is a value type used as a generic type argument, the JIT specializes the
/// collection for it and inlines the comparison instead of emitting an interface call per key.
/// </summary>
/// <typeparam name="T">The type being compared.</typeparam>
/// <remarks>
/// <para>
/// <see cref="BTreeDictionary{TKey, TValue, TComparer}"/> and <see cref="BTreeSet{T, TComparer}"/> take their
/// ordering as a <c>struct, IComparer&lt;T&gt;</c> type parameter rather than an
/// <see cref="IComparer{T}"/> instance, because an interface-typed comparer would cost a virtual call for
/// every key inspected inside a node — several per node visit, on the hottest path in the tree. The
/// two-parameter <see cref="BTreeDictionary{TKey, TValue}"/> / one-parameter <see cref="BTreeSet{T}"/>
/// aliases close over this type, so the common case needs no type argument at all.
/// </para>
/// <para>
/// To order by something else, write your own single-field-free struct and pass it as the type argument:
/// </para>
/// <code>
/// public readonly struct DescendingInt : IComparer&lt;int&gt;
/// {
///     public int Compare(int x, int y) =&gt; y.CompareTo(x);
/// }
///
/// var tree = new BTreeSet&lt;int, DescendingInt&gt;();
/// </code>
/// <para>
/// A <c>null</c> argument is not rejected: <see cref="Comparer{T}.Default"/> orders <c>null</c> before every
/// non-<c>null</c> value, which is what makes <c>default(TKey)</c> a legal key in the ordered collections.
/// </para>
/// </remarks>
public readonly struct DefaultComparer<T> : IComparer<T>
{
    /// <summary>
    /// Compares two values using <see cref="Comparer{T}.Default"/>.
    /// </summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>
    /// A negative number when <paramref name="x"/> precedes <paramref name="y"/>, zero when they are
    /// equivalent, and a positive number when <paramref name="x"/> follows <paramref name="y"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="T"/> implements neither <see cref="IComparable{T}"/> nor <see cref="IComparable"/>.
    /// </exception>
    public int Compare(T? x, T? y) => Comparer<T>.Default.Compare(x, y);
}
