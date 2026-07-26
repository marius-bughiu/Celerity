namespace Celerity.Hashing;

/// <summary>
/// Provides a hash function over a <see cref="ReadOnlySpan{T}"/> of <see cref="char"/>,
/// so a caller holding a slice of an existing buffer can probe a string-keyed collection
/// without first materializing a <see cref="string"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The contract.</strong> For every <c>string s</c> an implementation must satisfy
/// <c>Hash(s) == Hash(s.AsSpan())</c>. A collection that probes with a span and compares
/// against stored <see cref="string"/> keys relies on this: a divergence would not merely
/// be slow, it would report a stored key as absent. Implementations are expected to share
/// a single body between the two overloads rather than maintain two copies.
/// </para>
/// <para>
/// This interface deliberately does <strong>not</strong> derive from
/// <see cref="IHashProvider{T}"/>, mirroring the independent
/// <see cref="IHashProvider64{T}"/> sibling: <see cref="IHashProvider{T}"/> is generic in
/// its key type, and a <c>ref struct</c> such as <see cref="ReadOnlySpan{T}"/> could not be
/// used as a generic type argument before <c>allows ref struct</c> (C# 13 / .NET 9) —
/// while <c>net8.0</c> remains this library's floor. Expressing the span overload as a
/// separate non-generic interface sidesteps that entirely, because the span is a method
/// parameter rather than a type argument. In practice every built-in
/// <c>String*Hasher</c> implements both, so a single struct serves both call shapes.
/// </para>
/// <para>
/// Like <see cref="IHashProvider{T}"/>, implementations must be value types (structs) so
/// the JIT can devirtualize and inline calls made through a generic type parameter
/// constrained to <c>where THasher : struct, IHashProvider&lt;string&gt;, ISpanHashProvider</c>.
/// </para>
/// </remarks>
public interface ISpanHashProvider
{
    /// <summary>
    /// Computes a hash code for the specified character span.
    /// </summary>
    /// <param name="key">The characters to hash.</param>
    /// <returns>
    /// A 32-bit signed integer hash code, equal to the code the same hasher's
    /// <c>Hash(string)</c> overload returns for a string with the same contents.
    /// </returns>
    int Hash(ReadOnlySpan<char> key);
}
