using System.Reflection;
using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="GapBuffer{T}"/> against <see cref="List{T}"/>, which
/// has exactly the positional semantics this type promises and none of its gap. CsCheck generates the
/// starting size, how the buffer is built (from a collection, which leaves an empty gap at the end, or one
/// <c>Add</c> at a time from a pre-sized or an empty buffer), and a stream of positional edits, and asserts
/// after every operation that the two agree on count and on one read, and on the whole sequence at the end
/// of the script.
///
/// <para>
/// Edit positions are mostly a short random walk from the previous one, the cursor-local workload the type is
/// built for, which keeps the gap small and moving by a few slots at a time; the rest jump to either end or
/// anywhere, which moves it far, across itself, and past a gap shorter than the move. The elements are
/// strings so the property also checks the buffer's GC contract: after every operation, every slot inside the
/// gap is <c>null</c>, so a removed or moved-from element is not kept alive by a slot the buffer no longer
/// uses.
/// </para>
/// </summary>
public class GapBufferDifferentialTests
{
    private enum Op
    {
        Insert, RemoveAt, Set, Add, Read, InsertRange, InsertOwnSpan, RemoveRange, RemoveValue, IndexOf,
        EnsureCapacity, TrimExcess, Clear,
    }

    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 199].Select(n => n < 50 ? Op.Insert
                                  : n < 100 ? Op.RemoveAt
                                  : n < 112 ? Op.Set
                                  : n < 125 ? Op.Add
                                  : n < 145 ? Op.Read
                                  : n < 155 ? Op.InsertRange
                                  : n < 162 ? Op.InsertOwnSpan
                                  : n < 172 ? Op.RemoveRange
                                  : n < 180 ? Op.RemoveValue
                                  : n < 188 ? Op.IndexOf
                                  : n < 193 ? Op.EnsureCapacity
                                  : n < 198 ? Op.TrimExcess
                                  : Op.Clear);

    // Where: 0 = front, 1 = back, 2 = anywhere, otherwise a step of [-8, 8] from the previous position.
    private static readonly Gen<(Op Kind, int Value, int Where, uint Position, int Length)> GenOp =
        Gen.Select(GenKind, Gen.Int[0, 5_000], Gen.Int[0, 9], Gen.UInt, Gen.Int[0, 40]);

    private static readonly Gen<(int Size, int Build, List<(Op Kind, int Value, int Where, uint Position, int Length)> Ops)> GenScript =
        Gen.Select(Gen.OneOf(Gen.Int[0, 20], Gen.Int[20, 500], Gen.Int[2_000, 6_000]), Gen.Int[0, 2], GenOp.List[0, 800]);

    private static readonly FieldInfo ItemsField =
        typeof(GapBuffer<string>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!;

    [Fact]
    public void GapBuffer_ShouldMatch_AList()
    {
        GenScript.Sample(script =>
        {
            var oracle = new List<string>(script.Size);
            for (int i = 0; i < script.Size; i++)
                oracle.Add(i.ToString());

            GapBuffer<string> buffer;
            if (script.Build == 0)
            {
                buffer = new GapBuffer<string>(oracle);
            }
            else
            {
                buffer = script.Build == 1 ? new GapBuffer<string>(script.Size) : new GapBuffer<string>();
                foreach (string v in oracle)
                    buffer.Add(v);
            }

            int cursor = oracle.Count / 2;
            foreach (var (kind, value, where, position, length) in script.Ops)
            {
                int count = oracle.Count;
                string item = value.ToString();
                switch (kind)
                {
                    case Op.Insert:
                    {
                        int i = cursor = Pick(where, position, cursor, count + 1);
                        buffer.Insert(i, item);
                        oracle.Insert(i, item);
                        cursor++;
                        break;
                    }

                    case Op.RemoveAt when count > 0:
                    {
                        int i = cursor = Pick(where, position, cursor, count);
                        buffer.RemoveAt(i);
                        oracle.RemoveAt(i);
                        break;
                    }

                    case Op.Set when count > 0:
                    {
                        int i = Pick(where, position, cursor, count);
                        buffer[i] = item;
                        oracle[i] = item;
                        break;
                    }

                    case Op.Add:
                        buffer.Add(item);
                        oracle.Add(item);
                        break;

                    case Op.Read when count > 0:
                    {
                        int i = Pick(where, position, cursor, count);
                        Assert.Equal(oracle[i], buffer[i]);
                        break;
                    }

                    case Op.InsertRange:
                    {
                        int i = cursor = Pick(where, position, cursor, count + 1);
                        string[] run = new string[length];
                        for (int k = 0; k < length; k++)
                            run[k] = (value + k).ToString();
                        buffer.InsertRange(i, run);
                        oracle.InsertRange(i, run);
                        cursor += length;
                        break;
                    }

                    case Op.InsertOwnSpan:
                    {
                        // A span over the buffer's own array, inserted back into it: the gap move must not
                        // rewrite the source before it is read.
                        int i = cursor = Pick(where, position, cursor, count + 1);
                        buffer.GetSpans(out ReadOnlySpan<string> before, out ReadOnlySpan<string> after);
                        ReadOnlySpan<string> source = (value & 1) == 0 ? before : after;
                        int take = Math.Min(length, source.Length);
                        int offset = source.Length == take ? 0 : (int)(position % (uint)(source.Length - take + 1));
                        ReadOnlySpan<string> slice = source.Slice(offset, take);
                        string[] expected = slice.ToArray();
                        buffer.InsertRange(i, slice);
                        oracle.InsertRange(i, expected);
                        break;
                    }

                    case Op.RemoveRange:
                    {
                        int i = cursor = Pick(where, position, cursor, count + 1);
                        int n = Math.Min(length, count - i);
                        buffer.RemoveRange(i, n);
                        oracle.RemoveRange(i, n);
                        break;
                    }

                    case Op.RemoveValue:
                        Assert.Equal(oracle.Remove(item), buffer.Remove(item));
                        break;

                    case Op.IndexOf:
                        Assert.Equal(oracle.IndexOf(item), buffer.IndexOf(item));
                        Assert.Equal(oracle.Contains(item), buffer.Contains(item));
                        break;

                    case Op.EnsureCapacity:
                        Assert.True(buffer.EnsureCapacity(count + length) >= count + length);
                        break;

                    case Op.TrimExcess:
                        buffer.TrimExcess();
                        Assert.Equal(count, buffer.Capacity);
                        break;

                    case Op.Clear:
                        buffer.Clear();
                        oracle.Clear();
                        cursor = 0;
                        break;
                }

                Assert.Equal(oracle.Count, buffer.Count);
                Assert.InRange(buffer.GapPosition, 0, buffer.Count);
                AssertGapHoldsNoReferences(buffer);
                cursor = Math.Min(cursor, oracle.Count);
            }

            Assert.Equal(oracle, buffer.ToArray());
            Assert.Equal(oracle, buffer);
            buffer.GetSpans(out ReadOnlySpan<string> head, out ReadOnlySpan<string> tail);
            Assert.Equal(oracle, head.ToArray().Concat(tail.ToArray()));
            for (int i = 0; i < oracle.Count; i += 37)
                Assert.Equal(oracle[i], buffer[i]);
        }, iter: 60);
    }

    // The gap is [GapPosition, GapPosition + Capacity - Count) in the backing array.
    private static void AssertGapHoldsNoReferences(GapBuffer<string> buffer)
    {
        var items = (string?[])ItemsField.GetValue(buffer)!;
        int gapLength = buffer.Capacity - buffer.Count;
        for (int i = buffer.GapPosition; i < buffer.GapPosition + gapLength; i++)
            Assert.Null(items[i]);
    }

    private static int Pick(int where, uint position, int cursor, int range) => where switch
    {
        0 => 0,
        1 => range - 1,
        2 => (int)(position % (uint)range),
        _ => Math.Clamp(cursor + (int)(position % 17) - 8, 0, range - 1),
    };
}
