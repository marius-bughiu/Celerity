namespace Celerity.Collections;

/// <summary>
/// An <b>associative</b> binary operation together with its identity element — the algebraic structure
/// <see cref="SegmentTree{T, TMonoid}"/> folds a range with.
/// </summary>
/// <typeparam name="T">The element type the operation combines.</typeparam>
/// <remarks>
/// <para>
/// Implementations are taken as a <c>struct</c> generic type argument rather than as an interface-typed
/// instance, for the same reason the hashed collections take <c>IHashProvider&lt;T&gt;</c> and the ordered
/// ones take <c>IComparer&lt;T&gt;</c> that way: the JIT specializes the collection for the concrete struct
/// and inlines <see cref="Combine"/> instead of emitting an interface call, and a segment tree calls it
/// <c>O(log n)</c> times per query and per update.
/// </para>
/// <para>
/// An implementation must satisfy the two monoid laws, because the tree relies on both to answer a query
/// from precomputed partial folds:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Associativity</b> — <c>Combine(Combine(a, b), c)</c> equals <c>Combine(a, Combine(b, c))</c>. The tree
/// chooses its own bracketing, so an operation that is not associative gives an unspecified answer.
/// </description></item>
/// <item><description>
/// <b>Identity</b> — <c>Combine(Identity, a)</c> and <c>Combine(a, Identity)</c> both equal <c>a</c>.
/// <see cref="Identity"/> seeds an empty range and every freshly constructed element.
/// </description></item>
/// </list>
/// <para>
/// Both laws are required only over the implementation's <b>domain</b> — the set of values it declares itself
/// defined for — not over every bit pattern <typeparamref name="T"/> can hold. An implementation that restricts
/// its domain must say so, because a value outside it produces an unspecified aggregate rather than a thrown
/// exception. Two shipped monoids do restrict it: <see cref="MinMonoid{T}"/> and <see cref="MaxMonoid{T}"/> are
/// defined over the <i>finite</i> values of a floating-point <typeparamref name="T"/>, since their identity is
/// the largest / smallest finite value and <c>NaN</c> loses every comparison. The other three are defined over
/// all of <typeparamref name="T"/>.
/// </para>
/// <para>
/// Commutativity is <b>not</b> required. <see cref="SegmentTree{T, TMonoid}"/> preserves index order when it
/// folds, so a non-commutative operation (matrix product, "last write wins", string concatenation) is a valid
/// monoid here.
/// </para>
/// <para>
/// To fold by something the built-in monoids do not cover, write a field-free struct and pass it as the type
/// argument:
/// </para>
/// <code>
/// public readonly struct GcdMonoid : IMonoid&lt;uint&gt;
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
/// var tree = new SegmentTree&lt;uint, GcdMonoid&gt;(values);
/// </code>
/// <para>
/// That example is written over <see cref="uint"/> deliberately. A signed gcd has to normalize its sign, and
/// the obvious <c>Math.Abs</c> throws on <see cref="int.MinValue"/> — whose true gcd with <c>0</c> is
/// <c>2147483648</c>, a value no <see cref="int"/> can hold. Restricting the domain to unsigned values removes
/// the corner rather than papering over it.
/// </para>
/// </remarks>
public interface IMonoid<T>
{
    /// <summary>
    /// Gets the identity element: the value <c>e</c> for which <c>Combine(e, a)</c> and <c>Combine(a, e)</c>
    /// both equal <c>a</c>, for every <c>a</c>. It is also the aggregate of an empty range.
    /// </summary>
    T Identity { get; }

    /// <summary>
    /// Combines two values with the monoid's associative operation, preserving operand order.
    /// </summary>
    /// <param name="left">The value that comes first in index order.</param>
    /// <param name="right">The value that comes second in index order.</param>
    /// <returns>The combination of the two values.</returns>
    T Combine(T left, T right);
}
