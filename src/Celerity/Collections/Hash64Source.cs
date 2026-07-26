using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
/// caller. On the 64-bit arm the interface reference is boxed <strong>once</strong>, in the
/// static initializer for the closed generic type, and reused for every subsequent call — so
/// hashing itself allocates nothing, at the cost of a single upfront box. A 32-bit-only
/// <typeparamref name="THasher"/> boxes nothing at all: the initializer is guarded by the same
/// folded type test, so its boxing arm is never compiled into that instantiation.
/// </para>
/// <para>
/// <strong>Precondition:</strong> the shared instance is <c>default(THasher)</c>, so a caller
/// may only use this type when it hashes through <c>default(THasher)</c> itself. Every sketch
/// does: each assigns <c>_hasher = default</c> in its constructor, the field is
/// <c>readonly</c>, and none of them exposes a constructor that accepts a hasher instance — so
/// the shared instance is indistinguishable from the field they hold, even for a hasher struct
/// that carries state. Should a sketch ever gain a hasher-instance constructor, that
/// equivalence breaks and the 64-bit path would silently hash with the default instead; the
/// invariant is pinned by <c>SketchHashProvider64Tests.Sketches_DoNotExposeAHasherInstanceConstructor</c>
/// so it cannot rot unnoticed.
/// </para>
/// </remarks>
/// <typeparam name="T">The element type being hashed.</typeparam>
/// <typeparam name="THasher">The sketch's hasher type.</typeparam>
internal static class Hash64Source<T, THasher> where THasher : struct, IHashProvider<T>
{
    private static readonly IHashProvider64<T>? Native = CreateNative();

    // Guarded by the same folded type test the callers use, so a 32-bit-only THasher compiles
    // to a plain `null` here and never boxes: writing this as an unconditional
    // `(object)default(THasher) as IHashProvider64<T>` would leave the boxing to the JIT's
    // box-then-isinst pattern match to elide, which is an optimization to rely on rather than
    // a guarantee. The cast on the taken arm cannot fail — IsNative64 is exactly the test for
    // it — and on the other arm it is not compiled at all.
    //
    // The `: null` arm is unobservable and so carries no test. `Native` is read only by Hash64,
    // and every caller guards that on IsNative64 being true, so this class is never initialized
    // for a 32-bit-only THasher — the false arm is only ever evaluated if the runtime chooses to
    // run the beforefieldinit initializer eagerly, which is its option and not a contract. The
    // arm stays because the field must be null for such a THasher on any path that does reach it.
    [ExcludeFromCodeCoverage(Justification = "The false arm is unobservable: Native is read only " +
        "by Hash64, which callers invoke only when IsNative64 is true, so a 32-bit-only THasher " +
        "never triggers this initializer.")]
    private static IHashProvider64<T>? CreateNative() =>
        IsNative64 ? (IHashProvider64<T>)(object)default(THasher) : null;

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
    internal static ulong Hash64(T item)
    {
        // Names the precondition so a caller that forgets the IsNative64 guard fails in a
        // debug build with the reason, rather than as a bare NullReferenceException from a
        // helper five call sites away. Compiled out of release builds entirely.
        Debug.Assert(Native is not null,
            $"{nameof(Hash64)} requires {nameof(IsNative64)}; {typeof(THasher)} does not implement IHashProvider64<{typeof(T)}>.");

        return Native!.Hash64(item);
    }
}
