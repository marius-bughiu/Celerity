using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Two residual guards that ordinary usage never trips, pinned here because the only way to reach either is
/// to drive the API in a way no <c>foreach</c> or well-behaved collection ever would.
///
/// <para>
/// <b>1. <see cref="FenwickTree{T}"/>'s oversized-source rejection.</b> The
/// <see cref="FenwickTree{T}(IEnumerable{T})"/> constructor takes a deliberate fast path for a counted
/// source: it reads <see cref="ICollection{T}.Count"/> and applies the <c>Array.MaxLength - 1</c> ceiling
/// <i>before</i> allocating the 1-based backing array, so a source that is too long reports the documented
/// <see cref="ArgumentException"/> instead of dying inside an impossible allocation. That ordering is the
/// whole point of the fast path and it is invisible from the outside — an oversized real collection cannot
/// be built to prove it. So the test supplies a counted source that only <i>claims</i> to be enormous: a
/// stub whose <c>Count</c> reports <see cref="int.MaxValue"/> and whose every other member throws
/// <see cref="NotSupportedException"/>. Reaching the documented <see cref="ArgumentException"/> — rather
/// than a <see cref="NotSupportedException"/> from <c>CopyTo</c>/<c>GetEnumerator</c>, or an
/// <see cref="OutOfMemoryException"/> — is precisely the evidence that the length check runs first and that
/// nothing is allocated or enumerated on the way to it. The whole test costs no memory at all.
/// </para>
///
/// <para>
/// <b>2. <see cref="Trie{TValue}.Enumerator"/>'s exhausted state is sticky.</b> The
/// <see cref="IEnumerator"/> contract says <c>MoveNext</c> keeps returning <c>false</c> once it has
/// returned <c>false</c>, but a <c>foreach</c> stops at the first <c>false</c> and therefore never
/// exercises it. The trie enumerator reaches its terminal state through two structurally distinct exits —
/// the no-traversal exit (nothing to walk past the subtree root) and the drained-stack exit — and both must
/// latch. These tests drive the struct enumerator by hand past the end and assert it stays exhausted,
/// covering both exits, and additionally pin the two things the terminal state must <i>not</i> swallow: a
/// concurrent modification detected after exhaustion still throws (the version check runs ahead of the
/// exhausted short-circuit), and <see cref="Trie{TValue}.Enumerator.Reset"/> clears the state so the same
/// enumerator replays the full sequence.
/// </para>
/// </summary>
public class OversizedSourceAndResidualGuardTests
{
    // ---- FenwickTree: the counted-source ceiling ---------------------------------------------------

    /// <summary>
    /// A counted source that lies about its size. Only <see cref="Count"/> is usable; every other member
    /// throws, so any test that reaches the documented ArgumentException has proven that the constructor
    /// consulted the count and rejected the source without allocating, copying, or enumerating.
    /// </summary>
    private sealed class LyingCountCollection : ICollection<int>
    {
        public LyingCountCollection(int count) => Count = count;

        public int Count { get; }

        public bool IsReadOnly => throw new NotSupportedException();

        public void Add(int item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(int item) => throw new NotSupportedException();

        public void CopyTo(int[] array, int arrayIndex) => throw new NotSupportedException();

        public bool Remove(int item) => throw new NotSupportedException();

        public IEnumerator<int> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenCountedSourceExceedsTheMaximumLength()
    {
        // int.MaxValue is comfortably above Array.MaxLength - 1, the largest logical length the 1-based
        // layout can hold.
        var oversized = new LyingCountCollection(int.MaxValue);

        ArgumentException ex = Assert.Throws<ArgumentException>(() => new FenwickTree<int>(oversized));

        Assert.Equal("values", ex.ParamName);
        Assert.Contains("maximum supported length", ex.Message);
    }

    [Fact]
    public void Constructor_ShouldNotEnumerateOrCopy_WhenCountedSourceExceedsTheMaximumLength()
    {
        // Every member other than Count throws NotSupportedException, so an ArgumentException — not a
        // NotSupportedException — is the observable proof that the ceiling is applied before the backing
        // array is allocated and before a single element is read.
        var oversized = new LyingCountCollection(int.MaxValue);

        Assert.Throws<ArgumentException>(() => new FenwickTree<int>(oversized));
    }

    [Fact]
    public void Constructor_ShouldAcceptCountedSource_WhenCountIsWithinTheMaximumLength()
    {
        // The companion case: a genuinely counted source of a sane size takes the same fast path and is
        // copied straight into the 1-based array, so the ceiling only rejects the impossible.
        var tree = new FenwickTree<int>(new List<int> { 3, 1, 4, 1, 5 });

        Assert.Equal(5, tree.Count);
        Assert.Equal(14, tree.Total);
        Assert.Equal(4, tree[2]);
        Assert.Equal(8, tree.PrefixSum(3));
    }

    // ---- Trie.Enumerator: the exhausted state latches ----------------------------------------------

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_WhenCalledRepeatedlyAfterTheWalkDrained()
    {
        var trie = new Trie<int>
        {
            ["apple"] = 1,
            ["app"] = 2,
            ["banana"] = 3,
        };

        Trie<int>.Enumerator e = trie.GetEnumerator();

        var keys = new List<string>();
        while (e.MoveNext())
            keys.Add(e.Current.Key);

        Assert.Equal(new[] { "app", "apple", "banana" }, keys);

        // Past the end the enumerator must stay exhausted rather than restarting the traversal or throwing.
        Assert.False(e.MoveNext());
        Assert.False(e.MoveNext());
        Assert.False(e.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_WhenTheTrieIsEmpty()
    {
        // The no-traversal exit: no root value and no children, so the enumerator never allocates a stack
        // and latches on the very first call. Subsequent calls must take the exhausted short-circuit.
        var trie = new Trie<int>();

        Trie<int>.Enumerator e = trie.GetEnumerator();

        Assert.False(e.MoveNext());
        Assert.False(e.MoveNext());
        Assert.False(e.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_WhenTheOnlyEntryIsTheEmptyStringKey()
    {
        // The other way into the no-traversal exit: the subtree root itself carries the only value, so the
        // enumerator yields exactly one entry and then latches without ever building a traversal stack.
        var trie = new Trie<int>();
        trie.Add(string.Empty, 42);

        Trie<int>.Enumerator e = trie.GetEnumerator();

        Assert.True(e.MoveNext());
        Assert.Equal(string.Empty, e.Current.Key);
        Assert.Equal(42, e.Current.Value);

        Assert.False(e.MoveNext());
        Assert.False(e.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrowInvalidOperationException_WhenTheTrieIsModifiedAfterExhaustion()
    {
        // The version check runs ahead of the exhausted short-circuit, so a stale enumerator reports the
        // modification instead of quietly answering false.
        var trie = new Trie<int>();
        trie.Add("a", 1);

        Trie<int>.Enumerator e = trie.GetEnumerator();
        while (e.MoveNext())
        {
        }

        Assert.False(e.MoveNext());

        trie.Add("b", 2);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => e.MoveNext());
        Assert.Contains("Collection was modified", ex.Message);
    }

    [Fact]
    public void Reset_ShouldClearTheExhaustedState_WhenCalledAfterTheWalkDrained()
    {
        var trie = new Trie<int>
        {
            ["ax"] = 1,
            ["ay"] = 2,
        };

        Trie<int>.Enumerator e = trie.GetEnumerator();

        var first = new List<string>();
        while (e.MoveNext())
            first.Add(e.Current.Key);

        Assert.False(e.MoveNext());

        e.Reset();

        var second = new List<string>();
        while (e.MoveNext())
            second.Add(e.Current.Key);

        Assert.Equal(new[] { "ax", "ay" }, first);
        Assert.Equal(first, second);

        // And the replayed walk latches exactly as the first one did.
        Assert.False(e.MoveNext());
        Assert.False(e.MoveNext());
    }
}
