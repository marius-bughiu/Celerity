using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

// Issue #287: differential coverage for SparseSet against a BCL HashSet<int> oracle.
//
// SparseSet cannot join the shared SetAlgebraDifferentialTests: that harness draws
// from a universe that includes negatives (UniverseLow = -2), which SparseSet rejects
// by design (its universe is [0, Universe)). So — exactly like EnumSet's own
// EnumSetAlgebraDifferentialTests — SparseSet carries a dedicated non-negative-universe
// differential here.
//
// CsCheck generates the operation script and the `other` operand of every set-algebra
// call, and a SparseSet(Universe) is driven through it in lockstep with a HashSet<int>
// oracle: add / try-add / remove / contains / clear plus the full ISet<int> surface
// (union / intersect / except / symmetric-except / subset / superset / overlap /
// equality). Any divergence in resulting contents or a query result fails, and shrinks
// to the operations and operands that caused it rather than to a seed.
//
// The small, dense universe forces frequent overlaps, dense-array growth, swap-removes,
// and — crucially — clear-then-reuse cycles, which stress the stale-sparse round-trip
// membership check that makes Clear O(1).
public class SparseSetDifferentialTests
{
    private const int Universe = 40;

    private enum Op
    {
        UnionWith, IntersectWith, ExceptWith, SymmetricExceptWith,
        IsSubsetOf, IsSupersetOf, IsProperSubsetOf, IsProperSupersetOf,
        Overlaps, SetEquals, SelfAlias, ClearAndReuse, ContainsProbe, Churn,
    }

    private static readonly Gen<Op> GenKind = Gen.Int[0, 13].Select(n => (Op)n);

    // The `other` operand, over the non-negative universe and with duplicates. `Lazy` picks
    // between a plain array — an ICollection<int>, exercising the count-based fast paths — and a
    // non-ICollection enumerable, so both operand shapes are reached by generation rather than by
    // a coin flip inside the harness.
    private static readonly Gen<(Op Kind, List<int> Other, bool Lazy, int Aux, int Element, int Probe)> GenOp =
        Gen.Select(
            GenKind,
            Gen.Int[0, Universe - 1].List[0, 15],
            Gen.Bool,
            Gen.Int[0, 3],
            Gen.Int[0, Universe - 1],
            Gen.Int[-2, Universe + 1]);

    [Fact]
    public void SparseSet_ShouldMatch_AHashSet()
    {
        GenOp.List[0, 300].Sample(ops =>
        {
            var set = new SparseSet(Universe);
            var oracle = new HashSet<int>();
            int step = 0;

            foreach (var (kind, otherItems, lazy, aux, element, probe) in ops)
            {
                IEnumerable<int> other = lazy ? otherItems.Select(x => x) : otherItems.ToArray();

                switch (kind)
                {
                    case Op.UnionWith:
                        set.UnionWith(other);
                        oracle.UnionWith(other);
                        AssertSame(set, oracle, step);
                        break;

                    case Op.IntersectWith:
                        set.IntersectWith(other);
                        oracle.IntersectWith(other);
                        AssertSame(set, oracle, step);
                        break;

                    case Op.ExceptWith:
                        set.ExceptWith(other);
                        oracle.ExceptWith(other);
                        AssertSame(set, oracle, step);
                        break;

                    case Op.SymmetricExceptWith:
                        set.SymmetricExceptWith(other);
                        oracle.SymmetricExceptWith(other);
                        AssertSame(set, oracle, step);
                        break;

                    case Op.IsSubsetOf:
                        Assert.Equal(oracle.IsSubsetOf(other), set.IsSubsetOf(other));
                        break;

                    case Op.IsSupersetOf:
                        Assert.Equal(oracle.IsSupersetOf(other), set.IsSupersetOf(other));
                        break;

                    case Op.IsProperSubsetOf:
                        Assert.Equal(oracle.IsProperSubsetOf(other), set.IsProperSubsetOf(other));
                        break;

                    case Op.IsProperSupersetOf:
                        Assert.Equal(oracle.IsProperSupersetOf(other), set.IsProperSupersetOf(other));
                        break;

                    case Op.Overlaps:
                        Assert.Equal(oracle.Overlaps(other), set.Overlaps(other));
                        break;

                    case Op.SetEquals:
                        Assert.Equal(oracle.SetEquals(other), set.SetEquals(other));
                        break;

                    case Op.SelfAlias:
                        // Self-aliasing: `other` is the set itself.
                        switch (aux)
                        {
                            case 0: set.UnionWith(set); oracle.UnionWith(oracle); break;
                            case 1: set.IntersectWith(set); oracle.IntersectWith(oracle); break;
                            case 2: set.ExceptWith(set); oracle.ExceptWith(oracle); break;
                            default: set.SymmetricExceptWith(set); oracle.SymmetricExceptWith(oracle); break;
                        }

                        AssertSame(set, oracle, step);
                        break;

                    case Op.ClearAndReuse:
                        // Clear-then-reuse: the O(1) clear must leave no stale membership.
                        set.Clear();
                        oracle.Clear();
                        AssertSame(set, oracle, step);

                        // Re-add a few so the sparse array holds live-again slots that were
                        // stale a moment ago.
                        foreach (int v in otherItems.Take(4))
                            Assert.Equal(oracle.Add(v), set.TryAdd(v));

                        AssertSame(set, oracle, step);
                        break;

                    case Op.ContainsProbe:
                    {
                        // Including out-of-range probes, which must read as absent.
                        bool expected = probe >= 0 && probe < Universe && oracle.Contains(probe);
                        Assert.Equal(expected, set.Contains(probe));
                        break;
                    }

                    case Op.Churn:
                        // Single-element churn via ISet<int>.Add / Remove; the bool results agree too.
                        if (aux % 2 == 0)
                            Assert.Equal(oracle.Add(element), ((ISet<int>)set).Add(element));
                        else
                            Assert.Equal(oracle.Remove(element), set.Remove(element));

                        AssertSame(set, oracle, step);
                        break;
                }

                step++;
            }
        }, iter: 40);
    }

    private static void AssertSame(SparseSet actual, HashSet<int> expected, int step)
    {
        Assert.True(expected.Count == actual.Count,
            $"step {step}: count mismatch — expected {expected.Count}, got {actual.Count}");
        foreach (int e in expected)
            Assert.True(actual.Contains(e), $"step {step}: actual missing element {e}");
        foreach (int e in actual)
            Assert.True(expected.Contains(e), $"step {step}: actual has extra element {e}");
    }
}
