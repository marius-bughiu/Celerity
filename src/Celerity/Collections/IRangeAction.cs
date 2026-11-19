namespace Celerity.Collections;

/// <summary>
/// An <b>update</b> that can be applied to every element of a range at once — the second half of the contract
/// <see cref="LazySegmentTree{T, TUpdate, TMonoid, TAction}"/> needs, alongside the <see cref="IMonoid{T}"/> that
/// folds the elements.
/// </summary>
/// <typeparam name="T">The element type the paired monoid folds.</typeparam>
/// <typeparam name="TUpdate">The update type: "add 5", "set to 7", "multiply by 2 then add 1", ….</typeparam>
/// <remarks>
/// <para>
/// A lazy segment tree applies a range update in <c>O(log n)</c> by touching only the <c>O(log n)</c> nodes that
/// tile the range: each one has its aggregate rewritten in place and the update parked on it, to be pushed to its
/// children only when a later operation needs to look underneath. That is sound only if an update can be applied
/// to an <i>aggregate</i> without seeing the elements behind it, and if two parked updates can be merged into one.
/// Those are the two operations here, plus the "do nothing" update a node holds when nothing is parked on it.
/// </para>
/// <para>
/// Implementations are taken as a <c>struct</c> generic type argument, as <see cref="IMonoid{T}"/> is, so the JIT
/// specializes the tree for the concrete struct and inlines all three members.
/// </para>
/// <para>
/// An implementation must satisfy three laws against the monoid it is paired with, over that monoid's domain:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Identity</b> — <c>Apply(Identity, a, n)</c> equals <c>a</c>, and <c>Compose(Identity, f)</c> and
/// <c>Compose(f, Identity)</c> both equal <c>f</c>.
/// </description></item>
/// <item><description>
/// <b>Composition</b> — <c>Apply(Compose(f, g), a, n)</c> equals <c>Apply(g, Apply(f, a, n), n)</c>: applying
/// the composed update is the same as applying <c>f</c> and then <c>g</c>. <see cref="Compose"/> must be
/// associative, but need not be commutative ("set to 3, then add 1" is not "add 1, then set to 3").
/// </description></item>
/// <item><description>
/// <b>Distributivity</b> — <c>Apply(f, Combine(a, b), n + m)</c> equals
/// <c>Combine(Apply(f, a, n), Apply(f, b, m))</c>, where <c>a</c> folds <c>n</c> elements and <c>b</c> folds
/// <c>m</c>: updating a range's aggregate is the same as updating its two halves and folding them.
/// </description></item>
/// </list>
/// <para>
/// The <c>count</c> argument is what makes distributivity reachable for folds that are not idempotent. Adding
/// <c>d</c> to every element of a range moves its <i>minimum</i> by <c>d</c> whatever the range's length, but moves
/// its <i>sum</i> by <c>d × count</c>. That is also why an action is written for a particular monoid: the four
/// shipped actions each name the folds they are lawful with — <see cref="SumAddAction{T}"/>,
/// <see cref="MinMaxAddAction{T}"/>, <see cref="SumAssignAction{T}"/>, <see cref="IdempotentAssignAction{T}"/> —
/// and pairing one with a fold it does not distribute over compiles but gives unspecified answers.
/// </para>
/// <para>
/// A custom action is a field-free struct. Here is range-multiply over sums, with a <see cref="long"/> scale as the
/// update:
/// </para>
/// <code>
/// public readonly struct SumScaleAction : IRangeAction&lt;long, long&gt;
/// {
///     public long Identity =&gt; 1;
///
///     public long Compose(long earlier, long later) =&gt; earlier * later;
///
///     // Scaling every element by f scales their sum by f, whatever the count.
///     public long Apply(long update, long aggregate, int count) =&gt; aggregate * update;
/// }
///
/// var tree = new LazySegmentTree&lt;long, long, SumMonoid&lt;long&gt;, SumScaleAction&gt;(values);
/// </code>
/// </remarks>
public interface IRangeAction<T, TUpdate>
{
    /// <summary>
    /// Gets the update that changes nothing: <c>Apply(Identity, a, n)</c> is <c>a</c> for every <c>a</c> and
    /// <c>n</c>. Every node starts holding it, and holds it again once its pending update has been pushed down.
    /// </summary>
    TUpdate Identity { get; }

    /// <summary>
    /// Merges two updates into one that has the effect of applying <paramref name="earlier"/> and then
    /// <paramref name="later"/>.
    /// </summary>
    /// <param name="earlier">The update applied first.</param>
    /// <param name="later">The update applied second.</param>
    /// <returns>An update equivalent to <paramref name="earlier"/> followed by <paramref name="later"/>.</returns>
    TUpdate Compose(TUpdate earlier, TUpdate later);

    /// <summary>
    /// Returns what a range's aggregate becomes when <paramref name="update"/> is applied to every element of the
    /// range.
    /// </summary>
    /// <param name="update">The update applied to every element of the range.</param>
    /// <param name="aggregate">The range's aggregate before the update, under the paired monoid.</param>
    /// <param name="count">The number of elements the range holds; always at least <c>1</c>.</param>
    /// <returns>The range's aggregate after the update.</returns>
    T Apply(TUpdate update, T aggregate, int count);
}
