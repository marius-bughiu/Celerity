namespace Celerity.Hashing;

/// <summary>
/// Provides a <em>64-bit</em> hash function for values of type <typeparamref name="T"/>,
/// for consumers whose accuracy depends on the size of the hash space rather than on the
/// quality of a 32-bit bucket index.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IHashProvider{T}"/> returns 32 bits, which is all a hash table needs: the
/// table masks the code down to a bucket index and resolves the rest by comparing keys, so
/// a collision costs a probe, not a wrong answer. The probabilistic sketches
/// (<c>HyperLogLog</c>, <c>BloomFilter</c>, <c>CuckooFilter</c>, <c>XorFilter</c>,
/// <c>CountMinSketch</c>) are different: they never store the element, so two elements that
/// hash alike are <em>indistinguishable forever</em>. Their error budgets are derived
/// assuming the hash space is large enough that such collisions are negligible.
/// </para>
/// <para>
/// A 32-bit hash gives a space of only 2^32 ≈ 4.3&#160;billion values. Widening it — by
/// avalanching it into 64 bits with a bijective finalizer such as SplitMix64 — spreads
/// those values over the 64-bit range but <strong>creates no entropy that was not there</strong>:
/// the reachable set is still 2^32 values. For <c>n</c> distinct elements the expected number
/// of distinct 32-bit codes is <c>2^32 · (1 − e^(−n / 2^32))</c>, a systematic shortfall of
/// roughly <c>n / 2^33</c> — about 0.12% at 10^7 elements, 1.2% at 10^8, and 10.8% at 10^9.
/// A <c>HyperLogLog</c> at the default precision has a 0.81% standard error, so past roughly
/// 10^8 distinct elements the entropy floor dominates the estimator's own noise.
/// </para>
/// <para>
/// Implementing this interface says the hasher produces a genuine 64-bit result — one
/// derived from at least 64 bits of internal state and avalanched so every input bit
/// influences every output bit. Widening a 32-bit code does <em>not</em> qualify, and a
/// hasher whose key type has fewer than 64 bits of information (an <see cref="int"/>, say)
/// cannot qualify no matter how it mixes. Consumers rely on that contract: they use
/// <see cref="Hash64"/> as-is, without re-mixing it.
/// </para>
/// <para>
/// Like <see cref="IHashProvider{T}"/>, implementations must be value types (structs) so the
/// JIT can devirtualize and inline calls made through a generic type parameter. This
/// interface deliberately does <strong>not</strong> derive from <see cref="IHashProvider{T}"/>:
/// the two contracts are independent, and inheriting would force every 64-bit hasher to also
/// publish a lossy 32-bit fold. In practice the built-in hashers implement both, so a single
/// struct serves the collections and the sketches alike.
/// </para>
/// <para>
/// Hashers that implement it are listed in
/// <see href="https://github.com/marius-bughiu/Celerity/blob/main/docs/api/hashing.md">the hashing API reference</see>;
/// a sketch parameterized on a hasher that does not simply keeps the widened 32-bit path,
/// with the accuracy consequences documented on the sketch.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of value to hash.</typeparam>
public interface IHashProvider64<T>
{
    /// <summary>
    /// Computes a 64-bit hash code for the specified value.
    /// </summary>
    /// <param name="key">The value to hash.</param>
    /// <returns>
    /// A 64-bit unsigned hash code carrying genuine 64-bit entropy — consumers use it
    /// without further mixing, so implementations must avalanche their result.
    /// </returns>
    ulong Hash64(T key);
}
