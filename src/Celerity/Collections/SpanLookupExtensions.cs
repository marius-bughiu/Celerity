using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// Allocation-free lookups on the string-keyed Celerity collections from a
/// <see cref="ReadOnlySpan{T}"/> of <see cref="char"/> — a slice of a buffer a parser
/// already holds — without first materializing a <see cref="string"/>.
/// </summary>
/// <remarks>
/// <para>
/// A tokenizer, CSV/log reader, or route dispatcher that has a
/// <c>ReadOnlySpan&lt;char&gt;</c> over its input buffer would otherwise have to call
/// <c>new string(span)</c> to probe a string-keyed collection: one allocation plus a copy
/// <em>per lookup</em>, on the hot path of exactly the workloads these types exist for.
/// These overloads delete both. The stored keys are compared ordinally against the span,
/// which is what <see cref="EqualityComparer{T}.Default"/> does for <see cref="string"/>,
/// so a span lookup and the equivalent string lookup always agree.
/// </para>
/// <para>
/// <strong>Why extension methods.</strong> The collections are generic in their hasher
/// (<c>where THasher : struct, IHashProvider&lt;string&gt;</c>) and the span probe needs
/// <see cref="ISpanHashProvider"/> as well. Adding that to the class constraint would break
/// every existing instantiation, so the extra constraint lives on these methods instead:
/// they bind only when the hasher supplies both, are resolved statically (no boxing, and the
/// JIT still devirtualizes the hash call through the struct type parameter), and leave the
/// collections' own signatures untouched.
/// </para>
/// <para>
/// <strong>The empty span.</strong> A span has no <c>null</c> state, so an empty span means
/// the empty string <c>""</c> — an ordinary key — and never the out-of-band <c>null</c> key.
/// Look the <c>null</c> key up through the string overload.
/// </para>
/// <para>
/// <see cref="Trie{TValue}"/> takes no hasher and carries its span overloads as ordinary
/// instance methods; see <see cref="Trie{TValue}.TryGetValue(ReadOnlySpan{char}, out TValue)"/>.
/// To go the other way — turning a span into a <see cref="string"/> that is allocated only
/// the first time it is seen — use <see cref="StringInternTable"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var routes = new FrozenCelerityDictionary&lt;int, StringXxHash3Hasher&gt;(pairs);
///
/// ReadOnlySpan&lt;char&gt; path = requestLine.AsSpan(4, length);
/// if (routes.TryGetValue(path, out int handler))
///     Dispatch(handler);   // no string was allocated
/// </code>
/// </example>
public static class SpanLookupExtensions
{
    /// <summary>
    /// Attempts to get the value associated with the characters in <paramref name="key"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of the stored values.</typeparam>
    /// <typeparam name="THasher">The dictionary's hasher; must also hash spans.</typeparam>
    /// <param name="dictionary">The dictionary to probe.</param>
    /// <param name="key">The characters to look up. An empty span means the key <c>""</c>.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with <paramref name="key"/>
    /// if found; otherwise the default value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <c>null</c>.</exception>
    public static bool TryGetValue<TValue, THasher>(
        this FrozenCelerityDictionary<TValue, THasher> dictionary,
        ReadOnlySpan<char> key,
        out TValue? value)
        where THasher : struct, IHashProvider<string>, ISpanHashProvider
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        int index = dictionary.FindSlot(key, dictionary.Hasher);
        if (index < 0)
        {
            value = default;
            return false;
        }

        value = dictionary.ValueAt(index);
        return true;
    }

    /// <summary>
    /// Determines whether the characters in <paramref name="key"/> are present as a key.
    /// </summary>
    /// <typeparam name="TValue">The type of the stored values.</typeparam>
    /// <typeparam name="THasher">The dictionary's hasher; must also hash spans.</typeparam>
    /// <param name="dictionary">The dictionary to probe.</param>
    /// <param name="key">The characters to look up. An empty span means the key <c>""</c>.</param>
    /// <returns><c>true</c> if the key is found; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <c>null</c>.</exception>
    public static bool ContainsKey<TValue, THasher>(
        this FrozenCelerityDictionary<TValue, THasher> dictionary,
        ReadOnlySpan<char> key)
        where THasher : struct, IHashProvider<string>, ISpanHashProvider
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return dictionary.FindSlot(key, dictionary.Hasher) >= 0;
    }

    /// <summary>
    /// Determines whether the characters in <paramref name="item"/> are present in the set.
    /// </summary>
    /// <typeparam name="THasher">The set's hasher; must also hash spans.</typeparam>
    /// <param name="set">The set to probe.</param>
    /// <param name="item">The characters to look up. An empty span means the element <c>""</c>.</param>
    /// <returns><c>true</c> if the element is found; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="set"/> is <c>null</c>.</exception>
    public static bool Contains<THasher>(
        this FrozenCeleritySet<THasher> set,
        ReadOnlySpan<char> item)
        where THasher : struct, IHashProvider<string>, ISpanHashProvider
    {
        ArgumentNullException.ThrowIfNull(set);
        return set.FindSlot(item, set.Hasher) >= 0;
    }

    /// <summary>
    /// Attempts to get the value associated with the characters in <paramref name="key"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of the stored values.</typeparam>
    /// <typeparam name="THasher">The dictionary's hasher; must also hash spans.</typeparam>
    /// <param name="dictionary">The dictionary to probe.</param>
    /// <param name="key">The characters to look up. An empty span means the key <c>""</c>.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with <paramref name="key"/>
    /// if found; otherwise the default value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <c>null</c>.</exception>
    public static bool TryGetValue<TValue, THasher>(
        this CelerityDictionary<string, TValue, THasher> dictionary,
        ReadOnlySpan<char> key,
        out TValue? value)
        where THasher : struct, IHashProvider<string>, ISpanHashProvider
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        int index = dictionary.ProbeForKey(key, dictionary.Hasher);
        if (index < 0)
        {
            value = default;
            return false;
        }

        value = dictionary.ValueAt(index);
        return true;
    }

    /// <summary>
    /// Determines whether the characters in <paramref name="key"/> are present as a key.
    /// </summary>
    /// <typeparam name="TValue">The type of the stored values.</typeparam>
    /// <typeparam name="THasher">The dictionary's hasher; must also hash spans.</typeparam>
    /// <param name="dictionary">The dictionary to probe.</param>
    /// <param name="key">The characters to look up. An empty span means the key <c>""</c>.</param>
    /// <returns><c>true</c> if the key is found; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <c>null</c>.</exception>
    public static bool ContainsKey<TValue, THasher>(
        this CelerityDictionary<string, TValue, THasher> dictionary,
        ReadOnlySpan<char> key)
        where THasher : struct, IHashProvider<string>, ISpanHashProvider
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return dictionary.ProbeForKey(key, dictionary.Hasher) >= 0;
    }

    /// <summary>
    /// Determines whether the characters in <paramref name="item"/> are present in the set.
    /// </summary>
    /// <typeparam name="THasher">The set's hasher; must also hash spans.</typeparam>
    /// <param name="set">The set to probe.</param>
    /// <param name="item">The characters to look up. An empty span means the element <c>""</c>.</param>
    /// <returns><c>true</c> if the element is found; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="set"/> is <c>null</c>.</exception>
    public static bool Contains<THasher>(
        this CeleritySet<string, THasher> set,
        ReadOnlySpan<char> item)
        where THasher : struct, IHashProvider<string>, ISpanHashProvider
    {
        ArgumentNullException.ThrowIfNull(set);
        return set.ProbeForItem(item, set.Hasher) >= 0;
    }
}
