using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Monoids that exist only for the SegmentTree and SparseTable suites. Every fold shipped with the library is
// commutative, so none of them can observe whether a range query preserves index order; these fill that gap,
// plus the stateful case the instance-taking constructors exist for.
//
// Which interface each declares is load-bearing, not incidental. SparseTable<T, TMonoid> accepts only
// IIdempotentMonoid<T>, so the folds below that are idempotent declare it and the ones that are not stay on
// plain IMonoid<T> — ConcatMonoid and SaturatingSumMonoid are the two that are not, and a SparseTable over
// either would not compile.

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
/// Also <b>idempotent</b> (the first non-zero of <c>a, a</c> is <c>a</c>), so it declares
/// <see cref="IIdempotentMonoid{T}"/> and is the fold that pins <see cref="SparseTable{T, TMonoid}"/>'s claim
/// to preserve index order across its two overlapping windows.
/// </summary>
internal readonly struct FirstNonZeroMonoid : IIdempotentMonoid<int>
{
    public int Identity => 0;

    public int Combine(int left, int right) => left != 0 ? left : right;
}

/// <summary>
/// The greatest common divisor, transcribed verbatim from the "write your own fold" example in
/// <see cref="IMonoid{T}"/>'s docs, the API reference and the collections guide. A doc sample a reader is invited to
/// copy has to actually work, so it is pinned here rather than only inspected. It is written over
/// <see cref="uint"/> for the reason the docs state: a signed gcd has to normalize its sign, and
/// <c>Math.Abs(int.MinValue)</c> throws.
/// <para>
/// The one deviation from those samples is the interface: they show <c>IMonoid&lt;uint&gt;</c> under
/// <see cref="IMonoid{T}"/> and <c>IIdempotentMonoid&lt;uint&gt;</c> under
/// <see cref="IIdempotentMonoid{T}"/>, where the same body appears as the "write your own idempotent fold"
/// sample. Declaring the stronger of the two pins both, since it satisfies every constraint the weaker one
/// does — and <c>gcd(a, a) == a</c>, so the stronger claim is true.
/// </para>
/// </summary>
internal readonly struct GcdMonoid : IIdempotentMonoid<uint>
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

/// <summary>
/// "The first non-empty string wins" — the reference-typed counterpart to <see cref="FirstNonZeroMonoid"/>.
/// Idempotent, associative, non-commutative, and over a reference type, so it pins that
/// <see cref="SparseTable{T, TMonoid}"/> neither restricts <c>T</c> to value types nor reorders its two
/// windows. <see cref="ConcatMonoid"/> cannot serve here: concatenation is not idempotent.
/// </summary>
internal readonly struct FirstNonEmptyMonoid : IIdempotentMonoid<string>
{
    public string Identity => string.Empty;

    public string Combine(string left, string right) => left.Length != 0 ? left : right;
}

/// <summary>
/// Maximum with a caller-supplied floor as its identity — an idempotent monoid that carries state, so the
/// instance-taking <see cref="SparseTable{T, TMonoid}"/> constructors have something to prove.
/// <see cref="SaturatingSumMonoid"/> cannot serve: a saturating sum is neither idempotent nor accepted by that
/// type.
/// <para>
/// <b>Domain.</b> Values greater than or equal to the floor. The identity law is exactly
/// <c>Max(floor, a) == a</c>, which is what that restriction says, and <see cref="IMonoid{T}"/> permits a
/// declared domain for this case.
/// </para>
/// </summary>
internal readonly struct FlooredMaxMonoid : IIdempotentMonoid<int>
{
    private readonly int _floor;

    public FlooredMaxMonoid(int floor) => _floor = floor;

    public int Identity => _floor;

    public int Combine(int left, int right) => Math.Max(left, right);
}

// Range actions that exist only for the LazySegmentTree suites. The shipped actions are all commutative in how
// they compose (two adds, or a later assign that simply wins), so none of them can observe whether pending updates
// are composed in the order they were applied; AffineSumAction can. RepeatAssignAction pairs a reference-type,
// non-commutative fold with an action that needs the node's element count, and ScaledMinMaxAddAction carries state
// for the instance-taking constructors.

/// <summary>x → x · Mul + Add over sums. Composition is not commutative: (×2, +1) then (×3, +0) is not the reverse.</summary>
internal readonly struct AffineSumAction : IRangeAction<long, (long Mul, long Add)>
{
    public (long Mul, long Add) Identity => (1, 0);

    public (long Mul, long Add) Compose((long Mul, long Add) earlier, (long Mul, long Add) later) =>
        (earlier.Mul * later.Mul, earlier.Add * later.Mul + later.Add);

    public long Apply((long Mul, long Add) update, long aggregate, int count) =>
        aggregate * update.Mul + update.Add * count;
}

/// <summary>Sets every element to one string; over <see cref="ConcatMonoid"/> a range of n becomes n copies.</summary>
internal readonly struct RepeatAssignAction : IRangeAction<string, string?>
{
    public string? Identity => null;

    public string? Compose(string? earlier, string? later) => later ?? earlier;

    public string Apply(string? update, string aggregate, int count) =>
        update is null ? aggregate : string.Concat(Enumerable.Repeat(update, count));
}

/// <summary>Adds update × scale to every element, over min or max. The scale is instance state.</summary>
internal readonly struct ScaledMinMaxAddAction : IRangeAction<int, int>
{
    private readonly int _scale;

    public ScaledMinMaxAddAction(int scale) => _scale = scale;

    public int Identity => 0;

    public int Compose(int earlier, int later) => earlier + later;

    public int Apply(int update, int aggregate, int count) => aggregate + update * _scale;
}
