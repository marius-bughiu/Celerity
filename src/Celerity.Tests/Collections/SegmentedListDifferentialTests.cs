using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="SegmentedList{T}"/> against <see cref="List{T}"/>,
/// which has exactly the positional semantics this type promises and none of its segments. CsCheck generates
/// the starting size, how the list is built (the bulk constructor or one <c>Add</c> at a time, whose first
/// segments grow differently), and a stream of edits — appends, every <c>AddRange</c> source shape including
/// the list itself, positional inserts and removals, sets, searches, <c>Clear</c> and the capacity calls —
/// and asserts after every operation that the two agree on count, on one read and on the segment geometry,
/// and on the whole sequence (by enumeration and by a <c>GetSegment</c> walk) at the end of the script.
///
/// <para>
/// It runs twice. Over <see cref="int"/>, whose segments hold 16,384 elements, the starting sizes straddle the
/// first one and two segment boundaries. Over the 16 KB <see cref="SegmentedBig"/>, whose segments hold four,
/// every few operations cross a boundary, so the multi-segment shift, the reuse of segments retained by
/// <c>Clear</c> and removal, and the trim of a partly used spine are all driven densely. Edit positions are
/// skewed toward both ends because the first and last slots of a segment are where the shifts hand an element
/// across a boundary, and a uniform position almost never lands there.
/// </para>
/// </summary>
public class SegmentedListDifferentialTests
{
    private enum Op { Add, AddRange, Insert, RemoveAt, Set, Read, RemoveValue, IndexOf, Clear, TrimExcess, EnsureCapacity }

    // Edits outnumber reads; the clear and the capacity calls stay rare enough that the list gets to grow.
    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 199].Select(n => n < 40 ? Op.Add
                                  : n < 55 ? Op.AddRange
                                  : n < 95 ? Op.Insert
                                  : n < 135 ? Op.RemoveAt
                                  : n < 150 ? Op.Set
                                  : n < 165 ? Op.Read
                                  : n < 177 ? Op.RemoveValue
                                  : n < 185 ? Op.IndexOf
                                  : n < 189 ? Op.Clear
                                  : n < 195 ? Op.TrimExcess
                                  : Op.EnsureCapacity);

    // Value: the element, drawn from a small range so searches and Remove(T) hit. Where: 0 = front,
    // 1 = back, otherwise uniform. Position: the uniform draw. Shape: the AddRange source, and its length.
    private static readonly Gen<(Op Kind, int Value, int Where, uint Position, int Shape, uint Length)> GenOp =
        Gen.Select(GenKind, Gen.Int[0, 60], Gen.Int[0, 3], Gen.UInt, Gen.Int[0, 5], Gen.UInt);

    [Fact]
    public void SegmentedList_OfInt_ShouldMatch_AList()
    {
        // 16,384 per segment: sizes around one and two full segments, plus small ones whose first segment is
        // still growing.
        var genScript = Gen.Select(
            Gen.OneOf(Gen.Int[0, 100], Gen.Int[16_300, 16_500], Gen.Int[32_700, 32_900]),
            Gen.Bool,
            GenOp.List[0, 400]);

        genScript.Sample(script => Run(script.Item1, script.Item2, script.Item3, v => v, v => v, maxRange: 400),
            iter: 40);
    }

    [Fact]
    public void SegmentedList_OfASixteenKilobyteStruct_ShouldMatch_AList()
    {
        // Four per segment: any of these sizes already spans several segments.
        var genScript = Gen.Select(Gen.Int[0, 24], Gen.Bool, GenOp.List[0, 120]);

        genScript.Sample(script => Run(script.Item1, script.Item2, script.Item3, SegmentedBig.Of, b => b.Value, maxRange: 10),
            iter: 60);
    }

    private static void Run<T>(
        int size,
        bool bulk,
        List<(Op Kind, int Value, int Where, uint Position, int Shape, uint Length)> ops,
        Func<int, T> make,
        Func<T, int> value,
        int maxRange)
    {
        var oracle = new List<T>(size);
        for (int i = 0; i < size; i++)
            oracle.Add(make(i));

        SegmentedList<T> list;
        if (bulk)
        {
            list = new SegmentedList<T>(oracle);
        }
        else
        {
            list = new SegmentedList<T>();
            foreach (T v in oracle)
                list.Add(v);
        }

        int segmentLength = list.SegmentLength;
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;

        foreach (var (kind, raw, where, position, shape, length) in ops)
        {
            T item = make(raw);
            int count = oracle.Count;
            switch (kind)
            {
                case Op.Add:
                    list.Add(item);
                    oracle.Add(item);
                    break;

                case Op.AddRange:
                {
                    int n = (int)(length % (uint)(maxRange + 1));
                    T[] source = Enumerable.Range(raw, n).Select(make).ToArray();
                    switch (shape)
                    {
                        case 0: list.AddRange(source); break;
                        case 1: list.AddRange(new List<T>(source)); break;
                        case 2: list.AddRange(new SegmentedList<T>(source)); break;
                        case 3: list.AddRange(new LinkedList<T>(source)); break;
                        case 4: list.AddRange(source.Select(x => x)); break;
                        default:
                            // The list appended to itself: one copy of what it held before the call.
                            source = oracle.ToArray();
                            list.AddRange(list);
                            break;
                    }
                    oracle.AddRange(source);
                    break;
                }

                case Op.Insert:
                {
                    int i = Pick(where, position, count + 1);
                    list.Insert(i, item);
                    oracle.Insert(i, item);
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
                    list[i] = item;
                    oracle[i] = item;
                    break;
                }

                case Op.Read when count > 0:
                {
                    int i = Pick(where, position, count);
                    Assert.True(comparer.Equals(oracle[i], list[i]));
                    break;
                }

                case Op.RemoveValue:
                    Assert.Equal(oracle.Remove(item), list.Remove(item));
                    break;

                case Op.IndexOf:
                    Assert.Equal(oracle.IndexOf(item), list.IndexOf(item));
                    Assert.Equal(oracle.Contains(item), list.Contains(item));
                    break;

                case Op.Clear:
                    list.Clear();
                    oracle.Clear();
                    break;

                case Op.TrimExcess:
                    list.TrimExcess();
                    break;

                case Op.EnsureCapacity:
                {
                    int wanted = count + (int)(length % (uint)(maxRange + 1));
                    Assert.True(list.EnsureCapacity(wanted) >= wanted);
                    break;
                }
            }

            Assert.Equal(oracle.Count, list.Count);
            Assert.True(list.Capacity >= list.Count);
            Assert.Equal(oracle.Count == 0 ? 0 : (oracle.Count - 1) / segmentLength + 1, list.SegmentCount);
            if (oracle.Count > 0)
                Assert.Equal(value(oracle[^1]), value(list[list.Count - 1]));
        }

        Assert.Equal(oracle.Select(value), list.Select(value));
        Assert.Equal(oracle.Select(value), list.ToArray().Select(value));

        var walked = new List<int>(oracle.Count);
        for (int s = 0; s < list.SegmentCount; s++)
        {
            ReadOnlySpan<T> segment = list.GetSegment(s);
            for (int i = 0; i < segment.Length; i++)
                walked.Add(value(segment[i]));
        }
        Assert.Equal(oracle.Select(value), walked);
    }

    private static int Pick(int where, uint position, int range) => where switch
    {
        0 => 0,
        1 => range - 1,
        _ => (int)(position % (uint)range),
    };
}
