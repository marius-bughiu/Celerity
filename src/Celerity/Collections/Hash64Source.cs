using System.Runtime.CompilerServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// Resolves, once per closed generic instantiation, whether <typeparamref name="THasher"/>
/// carries genuine 64-bit entropy — i.e. whether it also implements
/// <see cref="IHashProvider64{T}"/> — and gives the probabilistic sketches a single place
/// to call it.
/// </summary>
/// <remarks>
/// <para>
/// The sketches never store the elements they see, so two elements whose hashes collide are
/// indistinguishable forever and the collision shows up directly in the error budget. A
/// 32-bit <see cref="IHashProvider{T}"/> code widened by a bijective finalizer occupies the
/// full 64-bit range but reaches only 2^32 distinct values, which puts a floor under that
/// budget no amount of extra memory can lift. When the hasher implements
/// <see cref="IHashProvider64{T}"/> the sketch takes its 64-bit result directly and the
/// floor disappears.
/// </para>
/// <para>
/// <see cref="IsNative64"/> is a pure type test, so the JIT folds it to a constant for every
/// concrete <typeparamref name="THasher"/> and compiles exactly one of the two arms into the
/// caller. The interface reference is materialized once in the static initializer
/// and reused, so the 64-bit path never allocates: the sketches hash through
/// <c>default(THasher)</c> — they construct their hasher field with <c>default</c> and every
/// built-in hasher is a stateless struct — so one shared instance is indistinguishable from
/// the field they hold.
/// </para>
/// </remarks>
/// <typeparam name="T">The element type being hashed.</typeparam>
/// <typeparam name="THasher">The sketch's hasher type.</typeparam>
internal static class Hash64Source<T, THasher> where THasher : struct, IHashProvider<T>
{
    private static readonly IHashProvider64<T>? Native = (object)default(THasher) as IHashProvider64<T>;

    /// <summary>
    /// <c>true</c> when <typeparamref name="THasher"/> also implements
    /// <see cref="IHashProvider64{T}"/>. A JIT-time constant.
    /// </summary>
    internal static bool IsNative64
    {
        // `default(THasher) is IFoo` is the canonical JIT-folded generic-specialization test: for a
        // concrete THasher the type check is decidable at compile time, so the JIT (and ILC under
        // Native AOT) folds it to a constant and removes the box. Written this way rather than with
        // Type.IsAssignableFrom so no reflection API is involved on a trimmed or AOT-published path.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => default(THasher) is IHashProvider64<T>;
    }

    /// <summary>
    /// Returns the hasher's native 64-bit code. Only valid when <see cref="IsNative64"/> is
    /// <c>true</c>; callers guard on it, and the guard is folded away.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Hash64(T item) => Native!.Hash64(item);
}
