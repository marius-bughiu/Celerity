namespace Celerity.Hashing;

/// <summary>
/// Provides a hash function for values of type <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// Implementations must be value types (structs). This is a load-bearing constraint:
/// the JIT can devirtualize calls made through a generic type parameter constrained to
/// <c>where THasher : struct, IHashProvider&lt;T&gt;</c>, eliminating virtual dispatch
/// on the hot probe path.
/// </remarks>
/// <typeparam name="T">The type of value to hash.</typeparam>
public interface IHashProvider<T>
{
    /// <summary>
    /// Computes a hash code for the specified value.
    /// </summary>
    /// <param name="key">The value to hash.</param>
    /// <returns>A 32-bit signed integer hash code.</returns>
    int Hash(T key);
}
