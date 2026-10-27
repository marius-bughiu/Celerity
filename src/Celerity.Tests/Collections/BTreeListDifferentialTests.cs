using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="BTreeList{T}"/> against <see cref="List{T}"/>, which
/// has exactly the positional semantics this type promises and none of its tree. CsCheck generates the
/// starting size, how the list is built (the bulk constructor or one <c>Add</c> at a time, whose splits are
/// biased differently), and a stream of positional edits, and asserts after every operation that the two
/// agree on count and on one read, and on the whole sequence at the end of the script.
///
/// <para>
/// The starting size reaches past 8,192 elements on purpose: that is where a three-level tree first
/// appears, so the branch-level split, borrow and merge paths are driven, not only the leaf ones. The
/// edit positions are skewed toward both ends because the split point depends on whether an insert lands
/// at a node's first or last slot, and a uniform position almost never does.
/// </para>
/// </summary>
public class BTreeListDifferentialTests
{
    private enum Op { Insert, RemoveAt, Set, Add, Read, RemoveValue, Clear }

    // Edits outnumber reads, and removals are common enough that underflow fires; the clear stays rare.
    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 199].Select(n => n < 60 ? Op.Insert
                                  : n < 130 ? Op.RemoveAt
                                  : n < 145 ? Op.Set
                                  : n < 165 ? Op.Add
                                  : n < 190 ? Op.Read
                                  : n < 199 ? Op.RemoveValue
                                  : Op.Clear);

    // 0 = front, 1 = back, otherwise uniform.
    private static readonly Gen<(Op Kind, int Value, int Where, uint Position)> GenOp =
        Gen.Select(GenKind, Gen.Int[0, 50_000], Gen.Int[0, 3], Gen.UInt);

    private static readonly Gen<(int Size, bool Bulk, List<(Op Kind, int Value, int Where, uint Position)> Ops)> GenScript =
        Gen.Select(Gen.OneOf(Gen.Int[0, 300], Gen.Int[300, 3_000], Gen.Int[8_000, 20_000]), Gen.Bool, GenOp.List[0, 1_500]);

    [Fact]
    public void BTreeList_ShouldMatch_AList()
    {
        GenScript.Sample(script =>
        {
            var oracle = new List<int>(script.Size);
            for (int i = 0; i < script.Size; i++)
                oracle.Add(i);

            BTreeList<int> list;
            if (script.Bulk)
            {
                list = new BTreeList<int>(oracle);
            }
            else
            {
                list = new BTreeList<int>();
                foreach (int v in oracle)
                    list.Add(v);
            }

            foreach (var (kind, value, where, position) in script.Ops)
            {
                int count = oracle.Count;
                switch (kind)
                {
                    case Op.Insert:
                    {
                        int i = Pick(where, position, count + 1);
                        list.Insert(i, value);
                        oracle.Insert(i, value);
                        break;
                    }

                    case Op.RemoveAt when count > 0:
                    {
                        int i = Pick(where, position, count);
                        list.RemoveAt(i);
                        oracle.RemoveAt(i);
                        break;
                    }

                    case Op.Set when count > 0:
                    {
                        int i = Pick(where, position, count);
                        list[i] = value;
                        oracle[i] = value;
                        break;
                    }

                    case Op.Add:
                        list.Add(value);
                        oracle.Add(value);
                        break;

                    case Op.Read when count > 0:
                    {
                        int i = Pick(where, position, count);
                        Assert.Equal(oracle[i], list[i]);
                        break;
                    }

                    case Op.RemoveValue:
                        Assert.Equal(oracle.Remove(value), list.Remove(value));
                        break;

                    case Op.Clear:
                        list.Clear();
                        oracle.Clear();
                        break;
                }

                Assert.Equal(oracle.Count, list.Count);
            }

            Assert.Equal(oracle, list.ToArray());
            Assert.Equal(oracle, list);
            for (int i = 0; i < oracle.Count; i += 97)
                Assert.Equal(oracle[i], list[i]);
        }, iter: 60);
    }

    private static int Pick(int where, uint position, int range) => where switch
    {
        0 => 0,
        1 => range - 1,
        _ => (int)(position % (uint)range),
    };
}
