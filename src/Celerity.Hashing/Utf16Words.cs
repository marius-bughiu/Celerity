using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Celerity.Hashing;

/// <summary>
/// Reads the 32- and 64-bit little-endian words of a character span's UTF-16 byte stream: the unit
/// the block-oriented string hashers in this assembly consume.
/// </summary>
/// <remarks>
/// <para>
/// Those hashers are defined over the string's little-endian UTF-16 byte stream (what
/// <c>Encoding.Unicode.GetBytes</c> produces), so the 64-bit word at char index <c>i</c> is
/// <c>s[i] | s[i + 1] &lt;&lt; 16 | s[i + 2] &lt;&lt; 32 | s[i + 3] &lt;&lt; 48</c>. On a
/// little-endian host that word is the span's own memory, read here with one unaligned load.
/// Composing it from the chars instead compiles, on .NET 10 for ARM64, to four 16-bit loads and
/// three shifted ORs, plus a bounds check on every char the JIT cannot prove in range.
/// </para>
/// <para>
/// On a big-endian host each char's two bytes sit high byte first, so the memory no longer holds
/// the stream's word. There the reader composes the word from the chars instead: the same value, at
/// the old cost. <see cref="BitConverter.IsLittleEndian"/> is a JIT-time constant, so a host compiles
/// only its own arm.
/// </para>
/// <para>
/// The loads are not bounds-checked. Each caller reads only words its own length dispatch has
/// already placed inside the span, and a Debug build asserts it. Every hasher's tests reconcile all
/// of its length classes against an independent byte-oriented reference, and the span parity tests
/// hash slices of a padded buffer, so a read past either end of the span changes a tested value.
/// </para>
/// </remarks>
internal static class Utf16Words
{
    private const string DispatchJustification =
        "Endianness dispatch: BitConverter.IsLittleEndian is a JIT-time constant and the test hosts " +
        "are little-endian, so the big-endian arm never runs there. The little-endian arm is Load, " +
        "which stays measured.";

    private const string BigEndianOnlyJustification =
        "Big-endian hosts only: the little-endian test hosts read the word with Load instead.";

    /// <summary>
    /// Reads the 64-bit little-endian word formed by the four chars starting at char index
    /// <paramref name="i"/>: bytes <c>2i</c> to <c>2i + 7</c> of the UTF-16 stream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [ExcludeFromCodeCoverage(Justification = DispatchJustification)]
    internal static ulong ReadUInt64(ReadOnlySpan<char> s, int i) =>
        BitConverter.IsLittleEndian ? Load<ulong>(s, i) : ComposeUInt64(s, i);

    /// <summary>
    /// Reads the 32-bit little-endian word formed by the two chars starting at char index
    /// <paramref name="i"/>: bytes <c>2i</c> to <c>2i + 3</c> of the UTF-16 stream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [ExcludeFromCodeCoverage(Justification = DispatchJustification)]
    internal static uint ReadUInt32(ReadOnlySpan<char> s, int i) =>
        BitConverter.IsLittleEndian ? Load<uint>(s, i) : ComposeUInt32(s, i);

    /// <summary>
    /// One unaligned load of the span's memory at char index <paramref name="i"/>, which on a
    /// little-endian host is the stream's word.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T Load<T>(ReadOnlySpan<char> s, int i)
        where T : unmanaged
    {
        Debug.Assert(
            ((ulong)(uint)i * sizeof(char)) + (ulong)Unsafe.SizeOf<T>() <= (ulong)(uint)s.Length * sizeof(char),
            "The word must lie inside the span.");
        return Unsafe.ReadUnaligned<T>(
            ref Unsafe.As<char, byte>(ref Unsafe.Add(ref MemoryMarshal.GetReference(s), (nint)(uint)i)));
    }

    /// <summary>The 64-bit word composed from its four chars, low char first.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [ExcludeFromCodeCoverage(Justification = BigEndianOnlyJustification)]
    private static ulong ComposeUInt64(ReadOnlySpan<char> s, int i) =>
        (ulong)s[i]
        | ((ulong)s[i + 1] << 16)
        | ((ulong)s[i + 2] << 32)
        | ((ulong)s[i + 3] << 48);

    /// <summary>The 32-bit word composed from its two chars, low char first.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [ExcludeFromCodeCoverage(Justification = BigEndianOnlyJustification)]
    private static uint ComposeUInt32(ReadOnlySpan<char> s, int i) =>
        (uint)s[i] | ((uint)s[i + 1] << 16);
}
