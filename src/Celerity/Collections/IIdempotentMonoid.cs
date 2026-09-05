namespace Celerity.Collections;

/// <summary>
/// An <see cref="IMonoid{T}"/> whose operation is additionally <b>idempotent</b> — <c>Combine(a, a)</c> equals
/// <c>a</c> for every value in its domain. This is the extra law <see cref="SparseTable{T, TMonoid}"/> needs,
/// and the reason that type does not simply take an <see cref="IMonoid{T}"/>.
/// </summary>
/// <typeparam name="T">The element type the operation combines.</typeparam>
/// <remarks>
/// <para>
/// The interface declares no members of its own. It exists so the law can be stated in the type system rather
/// than only in prose: a sparse table answers a range in <c>O(1)</c> by covering it with <b>two overlapping</b>
/// power-of-two windows and combining them, so every element in the overlap is folded in <i>twice</i>. That is
/// harmless exactly when re-folding a value changes nothing, and wrong otherwise.
/// </para>
/// <para>
/// Sum is the operation this excludes, and it is not hypothetical: <see cref="SumMonoid{T}"/> ships, and
/// <c>SparseTable&lt;int, SumMonoid&lt;int&gt;&gt;</c> would otherwise compile and quietly return inflated
/// answers for every range whose length is not a power of two. Constraining the table to this interface turns
/// that into a compile error. The four shipped folds that <i>are</i> idempotent —
/// <see cref="MinMonoid{T}"/>, <see cref="MaxMonoid{T}"/>, <see cref="BitwiseAndMonoid{T}"/> and
/// <see cref="BitwiseOrMonoid{T}"/> — implement it, and <see cref="SegmentTree{T, TMonoid}"/> is unaffected: it
/// never overlaps, so it keeps the plain <see cref="IMonoid{T}"/> constraint and still accepts all five.
/// </para>
/// <para>
/// Idempotence is required over the same declared <b>domain</b> as the two monoid laws, not over every bit
/// pattern <typeparamref name="T"/> can hold — see <see cref="IMonoid{T}"/>. For a floating-point
/// <typeparamref name="T"/>, <see cref="MinMonoid{T}"/> and <see cref="MaxMonoid{T}"/> are defined over the
/// finite values, and idempotence holds there; <c>NaN</c> is outside the domain and fails it, as it fails the
/// identity law.
/// </para>
/// <para>
/// To fold by an idempotent operation the built-in monoids do not cover, implement this interface instead of
/// <see cref="IMonoid{T}"/> on a field-free struct. Greatest common divisor is the standard example —
/// <c>gcd(a, a)</c> is <c>a</c> — and the <see cref="IMonoid{T}"/> remarks carry the full implementation:
/// </para>
/// <code>
/// public readonly struct GcdMonoid : IIdempotentMonoid&lt;uint&gt;
/// {
///     public uint Identity =&gt; 0;   // gcd(0, a) == a
///
///     public uint Combine(uint left, uint right)
///     {
///         while (right != 0)
///             (left, right) = (right, left % right);
///
///         return left;
///     }
/// }
///
/// var table = new SparseTable&lt;uint, GcdMonoid&gt;(values);
/// </code>
/// <para>
/// Declaring the interface is an assertion the compiler cannot check. An implementation that is not actually
/// idempotent gives an unspecified answer for ranges whose length is not a power of two, in the same way a
/// non-associative <see cref="IMonoid{T}"/> gives an unspecified answer to a segment-tree query.
/// </para>
/// </remarks>
public interface IIdempotentMonoid<T> : IMonoid<T>
{
}
