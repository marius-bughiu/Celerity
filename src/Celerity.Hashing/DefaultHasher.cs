using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A general-purpose hash provider that delegates to
/// <see cref="EqualityComparer{T}.Default"/>.<see cref="EqualityComparer{T}.GetHashCode(T)"/>.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="DefaultHasher{T}"/> when no specialized hasher exists for a type, or
/// when you want the same hash distribution as the BCL's default equality comparer
/// (e.g. for <see cref="Guid"/>, custom structs, or reference types).
/// </para>
/// <para>
/// Because <see cref="DefaultHasher{T}"/> is a struct, the JIT can still devirtualize
/// the call to <see cref="Hash"/> on the collection's hot probe path.
/// The inner call to <see cref="EqualityComparer{T}.Default"/> may itself involve a
/// virtual dispatch, so specialized hashers (e.g. <see cref="Int32WangNaiveHasher"/>,
/// <see cref="Int64Murmur3Hasher"/>) will be faster when available.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of key to hash.</typeparam>
public struct DefaultHasher<T> : IHashProvider<T>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(T key) => EqualityComparer<T>.Default.GetHashCode(key!);
}
