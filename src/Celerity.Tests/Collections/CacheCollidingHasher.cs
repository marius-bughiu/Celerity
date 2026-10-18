using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// A deliberately bad <see cref="int"/> hasher for the cache differential suites. Every key lands in
/// bucket 0 of the caches' key index, for any capacity those suites build, and the keys split between
/// two full hash values by parity. So one chain holds every resident key. A lookup then walks past
/// slots whose hash differs and past slots whose hash matches but whose key does not, and a removal
/// usually unlinks a slot from the middle of the chain.
/// </summary>
internal struct CacheCollidingHasher : IHashProvider<int>
{
    public int Hash(int key) => (key & 1) << 20;
}

/// <summary>
/// A <see cref="string"/> hasher that throws on a <c>null</c> key, as a strict hasher may. The caches'
/// null-key tests use it to pin that a <c>null</c> key never reaches the hasher.
/// </summary>
internal struct NullRejectingStringHasher : IHashProvider<string>
{
    public int Hash(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.GetHashCode();
    }
}

/// <summary>
/// The <see cref="string"/> counterpart of <see cref="CacheCollidingHasher"/>: every key lands in bucket
/// 0 of the caches' key index, split between two full hash values by length parity. Reference keys take
/// the index's stored-hash test before <c>Equals</c>, so a chain of these exercises both outcomes of it.
/// </summary>
internal struct CacheCollidingStringHasher : IHashProvider<string>
{
    public int Hash(string key) => (key.Length & 1) << 20;
}

/// <summary>
/// A <see cref="Nullable{T}"/> hasher that reads <c>key.Value</c>, so it throws on a null key. It pins
/// that a null nullable key, which is a value type, never reaches the hasher either.
/// </summary>
internal struct NullRejectingNullableIntHasher : IHashProvider<int?>
{
    public int Hash(int? key) => key!.Value;
}

/// <summary>
/// An <see cref="int"/> hasher that throws on <c>0</c>, <c>default(int)</c>. It pins that the caches
/// never hand the default key to the hasher, as the dictionary-backed index they replaced did not.
/// </summary>
internal struct DefaultRejectingIntHasher : IHashProvider<int>
{
    public int Hash(int key)
    {
        if (key == 0)
            throw new ArgumentOutOfRangeException(nameof(key));
        return key;
    }
}
