using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="string"/> keys using the FNV-1a 32-bit algorithm.
/// </summary>
/// <remarks>
/// FNV-1a has good distribution for short strings and is simple to implement.
/// Note that this implementation hashes only the lower byte of each character,
/// so strings that differ only in high bytes of non-ASCII characters may collide.
/// For Unicode-heavy workloads, a full UTF-8 or UTF-16 hash is preferable.
/// </remarks>
public struct StringFnV1AHasher : IHashProvider<string>
{
    /// <summary>
    /// Computes the FNV-1a 32-bit hash of the specified string.
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit FNV-1a hash of <paramref name="key"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/> is <c>null</c>. Celerity dictionaries store the
    /// out-of-band <c>null</c>-key entry without calling the hasher, so this
    /// check only surfaces when the hasher is used directly or plugged into a
    /// consumer that does not handle <c>null</c> keys out-of-band.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        // The FNV-1a 32-bit parameters
        const uint fnvPrime = 16777619;
        const uint offsetBasis = 2166136261;

        uint hash = offsetBasis;
        foreach (char c in key)
        {
            // Convert char to its lower byte; you may also want to consider
            // encoding specifics if you deal with non-ASCII characters.
            hash ^= (byte)(c & 0xFF);
            hash *= fnvPrime;
        }

        // Cast back to int
        return unchecked((int)hash);
    }
}
