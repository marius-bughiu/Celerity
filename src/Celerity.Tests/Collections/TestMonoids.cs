using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Monoids that exist only for the SegmentTree suites. Every fold shipped with the library is commutative, so
// none of them can observe whether the tree preserves index order; these fill that gap, plus the stateful case
// the instance-taking constructors exist for.

/// <summary>
/// String concatenation. Non-commutative and reference-typed, so it pins both that the tree folds in index
/// order and that <c>T</c> is not restricted to value types.
/// </summary>
internal readonly struct ConcatMonoid : IMonoid<string>
{
    public string Identity => string.Empty;

    public string Combine(string left, string right) => left + right;
}

/// <summary>
/// "The first non-zero value wins" — associative, identity <c>0</c>, and non-commutative: <c>Combine(1, 2)</c>
/// is <c>1</c> while <c>Combine(2, 1)</c> is <c>2</c>. A value-typed counterpart to <see cref="ConcatMonoid"/>.
/// </summary>
internal readonly struct FirstNonZeroMonoid : IMonoid<int>
{
    public int Identity => 0;

    public int Combine(int left, int right) => left != 0 ? left : right;
}

/// <summary>
/// The greatest common divisor, transcribed verbatim from the "write your own fold" example in
/// <see cref="IMonoid{T}"/>'s docs, the API reference and the README. A doc sample a reader is invited to
/// copy has to actually work, so it is pinned here rather than only inspected. It is written over
/// <see cref="uint"/> for the reason the docs state: a signed gcd has to normalize its sign, and
/// <c>Math.Abs(int.MinValue)</c> throws.
/// </summary>
internal readonly struct GcdMonoid : IMonoid<uint>
{
    public uint Identity => 0;   // gcd(0, a) == a

    public uint Combine(uint left, uint right)
    {
        while (right != 0)
            (left, right) = (right, left % right);

        return left;
    }
}

/// <summary>A monoid that carries state, so the instance-taking constructors have something to prove.</summary>
internal readonly struct SaturatingSumMonoid : IMonoid<int>
{
    private readonly int _ceiling;

    public SaturatingSumMonoid(int ceiling) => _ceiling = ceiling;

    public int Identity => 0;

    public int Combine(int left, int right) => Math.Min(left + right, _ceiling);
}
