using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Regression coverage for issue #94: the <c>IEnumerable</c> constructor on
/// every collection must surface a <c>null</c> source as
/// <see cref="ArgumentNullException"/>, even when the caller also passed an
/// invalid <c>loadFactor</c> (which would otherwise be reported by the chained
/// primary ctor's validation as <see cref="ArgumentOutOfRangeException"/>).
/// </summary>
public class IEnumerableConstructorNullPriorityTests
{
    // ──────────────────────────────────────────────────────────────
    //  Dictionaries
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void IntDictionary_NullSourceWithInvalidLoadFactor_ShouldThrow_ArgumentNullException()
    {
        IEnumerable<KeyValuePair<int, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new IntDictionary<string>(source!, capacity: 16, loadFactor: 2.0f));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void IntDictionary_Generic_NullSourceWithInvalidLoadFactor_ShouldThrow_ArgumentNullException()
    {
        IEnumerable<KeyValuePair<int, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new IntDictionary<string, Int32WangNaiveHasher>(source!, capacity: 16, loadFactor: 2.0f));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void LongDictionary_NullSourceWithInvalidLoadFactor_ShouldThrow_ArgumentNullException()
    {
        IEnumerable<KeyValuePair<long, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new LongDictionary<string>(source!, capacity: 16, loadFactor: 2.0f));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void LongDictionary_Generic_NullSourceWithInvalidLoadFactor_ShouldThrow_ArgumentNullException()
    {
        IEnumerable<KeyValuePair<long, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new LongDictionary<string, Int64WangHasher>(source!, capacity: 16, loadFactor: 2.0f));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void CelerityDictionary_NullSourceWithInvalidLoadFactor_ShouldThrow_ArgumentNullException()
    {
        IEnumerable<KeyValuePair<int, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new CelerityDictionary<int, string, Int32WangNaiveHasher>(source!, capacity: 16, loadFactor: 2.0f));

        Assert.Equal("source", ex.ParamName);
    }

    // ──────────────────────────────────────────────────────────────
    //  Sets
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void IntSet_NullSourceWithInvalidLoadFactor_ShouldThrow_ArgumentNullException()
    {
        IEnumerable<int>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new IntSet(source!, capacity: 16, loadFactor: 2.0f));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void IntSet_Generic_NullSourceWithInvalidLoadFactor_ShouldThrow_ArgumentNullException()
    {
        IEnumerable<int>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new IntSet<Int32WangNaiveHasher>(source!, capacity: 16, loadFactor: 2.0f));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void CeleritySet_NullSourceWithInvalidLoadFactor_ShouldThrow_ArgumentNullException()
    {
        IEnumerable<string>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new CeleritySet<string, StringFnV1AHasher>(source!, capacity: 16, loadFactor: 2.0f));

        Assert.Equal("source", ex.ParamName);
    }

    // ──────────────────────────────────────────────────────────────
    //  Boundary: invalid loadFactor = 0f still surfaces null-source first
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void IntDictionary_NullSourceWithZeroLoadFactor_ShouldThrow_ArgumentNullException()
    {
        IEnumerable<KeyValuePair<int, string>>? source = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new IntDictionary<string>(source!, capacity: 16, loadFactor: 0f));

        Assert.Equal("source", ex.ParamName);
    }
}
