using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A fast hash provider for <see cref="uint"/> keys using a Wang/Jenkins-style
/// integer bit-mixer.
/// </summary>
/// <remarks>
/// This is the <see cref="uint"/> counterpart to <see cref="Int32WangNaiveHasher"/>
/// and the cheap-default tier of the <see cref="uint"/> escalation ladder. It folds
/// the high bits of the value into the low bits (<c>key ^ (key &gt;&gt; 16)</c>), then
/// reinterprets the 32-bit result as a signed integer. Prefer this when key
/// distribution is already reasonably uniform and latency matters more than
/// collision resistance; escalate to <see cref="UInt32WangHasher"/> (the full
/// Thomas-Wang <c>hash32shift</c> finalizer) or <see cref="UInt32Murmur3Hasher"/>
/// (the Murmur3 <c>fmix32</c> finalizer) for clustered or adversarial inputs.
/// <para>
/// The shift is logical here and arithmetic in <see cref="Int32WangNaiveHasher"/>,
/// so the two do <em>not</em> agree on a bit pattern whose top bit is set — unlike
/// the other unsigned/signed pairs in the family, which are bit-for-bit identical.
/// </para>
/// <para>
/// This type replaces <c>UInt32Hasher</c>, whose bare name said nothing about which
/// tier of the ladder it occupied — and named the cheapest tier for <see cref="uint"/>
/// while the identically-shaped <c>UInt64Hasher</c> named the strongest one for
/// <see cref="ulong"/>. The old name shipped as an obsolete alias through v3.0.0 and
/// no longer exists.
/// </para>
/// </remarks>
public struct UInt32WangNaiveHasher : IHashProvider<uint>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(uint key) => (int)(key ^ (key >> 16));
}
