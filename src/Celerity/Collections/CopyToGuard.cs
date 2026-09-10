namespace Celerity.Collections;

/// <summary>
/// The one place the <c>CopyTo(T[] array, int arrayIndex)</c> argument contract is written.
/// Every public <c>CopyTo</c> overload in the library routes its validation through
/// <see cref="Validate{T}(T[], int, long, string)"/>, so the three checks and the exception
/// each one throws cannot drift apart between collections.
/// </summary>
/// <remarks>
/// <para>
/// The contract is <c>Dictionary&lt;TKey, TValue&gt;.CopyTo</c>'s: a <see langword="null"/> array is
/// <see cref="ArgumentNullException"/>; an <c>arrayIndex</c> that is negative <i>or past the end
/// of the array</i> is <see cref="ArgumentOutOfRangeException"/>; and only a valid index with too
/// little room after it is <see cref="ArgumentException"/>. It is deliberately <b>stricter than
/// <see cref="HashSet{T}.CopyTo(T[], int)"/></b>, which folds a past-the-end index into the
/// insufficient-space case and reports it as <see cref="ArgumentException"/> — the library's thirty
/// dictionary overloads already answered the dictionary way, so the sets follow them rather than
/// the other way round.
/// </para>
/// <para>
/// The middle check is the one worth having a shared helper for. Without it, an
/// <c>arrayIndex</c> beyond the end falls through to <c>array.Length - arrayIndex</c>, goes
/// negative, and trips the insufficient-space branch — so the caller gets an
/// <see cref="ArgumentException"/> where every other collection in the library gives an
/// <see cref="ArgumentOutOfRangeException"/> for the same mistake. Because
/// <see cref="ArgumentOutOfRangeException"/> derives from <see cref="ArgumentException"/> a
/// <c>catch (ArgumentException)</c> is unaffected — but a <c>catch (ArgumentOutOfRangeException)</c>
/// is, and so is an exact-type test (xUnit's <c>Assert.Throws&lt;T&gt;</c>), an exception filter, or
/// any code telling "bad index" from "array too small" apart.
/// <c>Deque&lt;T&gt;</c> and <c>PersistentVector&lt;T&gt;</c> both shipped without it.
/// </para>
/// </remarks>
internal static class CopyToGuard
{
    // The insufficient-space message names what is being copied, which is the only part that
    // varies across the family. Consts rather than a noun parameter so the strings stay
    // greppable and a new collection picks one from a menu instead of inventing wording.
    internal const string EntriesMessage = "The destination array has insufficient space for the entries.";
    internal const string KeysMessage = "The destination array has insufficient space for the keys.";
    internal const string ValuesMessage = "The destination array has insufficient space for the values.";
    internal const string SetElementsMessage = "The destination array has insufficient space to copy the set's elements.";
    internal const string ElementsMessage = "The destination array has insufficient space for the elements.";

    /// <summary>
    /// Validates the arguments of a <c>CopyTo(T[] array, int arrayIndex)</c> overload against the
    /// BCL contract, throwing before the copy begins.
    /// </summary>
    /// <typeparam name="T">The destination array's element type.</typeparam>
    /// <param name="array">The destination array, as the caller received it.</param>
    /// <param name="arrayIndex">The index copying would begin at.</param>
    /// <param name="count">
    /// How many elements the copy would write. Widened to <see cref="long"/> so
    /// <c>CompressedIntSet</c> can pass its <c>long</c> cardinality without first evaluating an
    /// <c>int</c> <c>Count</c> that would throw <see cref="OverflowException"/> ahead of argument
    /// validation. The subtraction below cannot overflow either way: the two checks above leave
    /// <paramref name="arrayIndex"/> in <c>[0, array.Length]</c>.
    /// </param>
    /// <param name="insufficientSpaceMessage">
    /// One of the message constants on this class, naming what is being copied.
    /// </param>
    internal static void Validate<T>(T[] array, int arrayIndex, long count, string insufficientSpaceMessage)
    {
        // ThrowIfNull's [CallerArgumentExpression] reads this call's own argument expression,
        // which is `array` — the same name every CopyTo overload gives the parameter, so the
        // ArgumentNullException still names the caller's parameter rather than a helper's.
        ArgumentNullException.ThrowIfNull(array);
        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex,
                "Array index must be non-negative.");
        if (arrayIndex > array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex,
                "Array index is beyond the end of the destination array.");
        if (array.Length - arrayIndex < count)
            throw new ArgumentException(insufficientSpaceMessage, nameof(array));
    }
}
