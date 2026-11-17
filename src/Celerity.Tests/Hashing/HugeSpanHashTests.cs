using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

/// <summary>
/// The block string hashers read their words without bounds checks, so their loop bounds must not
/// overflow for a span near <see cref="int.MaxValue"/> chars: a bound written <c>i + 4 &lt;= length</c>
/// wraps negative once <c>i</c> passes <c>int.MaxValue - 4</c> and reads past the end of the span. No
/// <c>char[]</c> or <see cref="string"/> is that long, but a span over native memory, or a
/// <c>MemoryMarshal.Cast</c> of a wider array, can be. These tests hash a span over a 4 GiB native
/// buffer and check that the result ignores the memory just past the span's end.
/// </summary>
/// <remarks>
/// <para>
/// The buffer is never written apart from those few trailing chars. Where the OS backs untouched pages
/// with a shared zero page, as Linux does, reading it costs address space rather than resident memory;
/// elsewhere it can commit the full 4 GiB, hence the <see cref="MemoryIntensiveFactAttribute"/>.
/// </para>
/// <para>
/// The tests run in Release builds only. An unoptimised Debug build of the hashers, asserting every
/// word, stretched the four XxHash64 and XxHash32 passes to about half a minute; CI tests Release. They cover XxHash64
/// and XxHash32, whose loop bounds were once written in the overflowing form, and XxHash3, whose
/// vectorized loop for long inputs computes its byte offsets from the length: every block hasher at
/// this length would take about a minute, since a 4 GiB pass of a SipHash variant takes seconds.
/// </para>
/// </remarks>
public class HugeSpanHashTests
{
    // int.MaxValue chars leaves a 64-bit lane loop three chars and a 32-bit one a single char after
    // its last whole word, the lengths at which an overflowing bound reads past the end.
    private const int Length = int.MaxValue;
    private const int SlackChars = 16;

#if DEBUG
    private const string ReleaseOnly = "Runs in Release builds only: a Debug build is far slower.";

    [Fact(Skip = ReleaseOnly)]
#else
    [MemoryIntensiveFact(4200)]
#endif
    public void XxHash64_ShouldReadOnlyInsideTheSpan_WhenTheSpanIsIntMaxValueCharsLong() =>
        AssertHashIgnoresTheMemoryPastTheEnd<StringXxHash64Hasher>();

#if DEBUG
    [Fact(Skip = ReleaseOnly)]
#else
    [MemoryIntensiveFact(4200)]
#endif
    public void XxHash32_ShouldReadOnlyInsideTheSpan_WhenTheSpanIsIntMaxValueCharsLong() =>
        AssertHashIgnoresTheMemoryPastTheEnd<StringXxHash32Hasher>();

#if DEBUG
    [Fact(Skip = ReleaseOnly)]
#else
    [MemoryIntensiveFact(4200)]
#endif
    public void XxHash3_ShouldReadOnlyInsideTheSpan_WhenTheSpanIsIntMaxValueCharsLong() =>
        AssertHashIgnoresTheMemoryPastTheEnd<StringXxHash3Hasher>();

    private static void AssertHashIgnoresTheMemoryPastTheEnd<THasher>()
        where THasher : struct, ISpanHashProvider
    {
        long bytes = ((long)Length + SlackChars) * sizeof(char);
        nint buffer = Marshal.AllocHGlobal(checked((nint)bytes));
        try
        {
            // A managed reference to native memory, without an unsafe context.
            ref char first = ref Unsafe.AddByteOffset(ref Unsafe.NullRef<char>(), buffer);
            ReadOnlySpan<char> span = MemoryMarshal.CreateReadOnlySpan(ref first, Length);
            Span<char> pastTheEnd = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref first, Length), SlackChars);

            pastTheEnd.Fill('\0');
            int withZerosAfter = default(THasher).Hash(span);

            pastTheEnd.Fill('￿');
            int withOnesAfter = default(THasher).Hash(span);

            Assert.Equal(withZerosAfter, withOnesAfter);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
