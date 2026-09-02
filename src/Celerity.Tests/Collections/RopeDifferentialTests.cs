using System.Reflection;
using System.Text;
using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="Rope"/> against <see cref="StringBuilder"/>, which is
/// both the type it exists to beat and a perfect oracle for it: the two have the same contract on every
/// operation that changes text, so any divergence is a bug in the tree rather than a judgement call.
///
/// <para>
/// The failure mode on trial is the one an example suite cannot reach by inspection. A rope's correctness
/// lives in the AVL joins and splits that run when an edit crosses a leaf boundary or collapses a subtree, and
/// those paths are selected by the <i>arithmetic relationship</i> between the edit and the current leaf
/// layout — which is itself the accumulated history of every previous edit. A hand-written case fixes that
/// history; a generated sequence does not.
/// </para>
///
/// <para>
/// The chunk size is drawn from the legal floor upwards on purpose. At eight characters a leaf, a few hundred
/// characters is already a tree a dozen nodes deep, so leaf overflow, leaf collapse, rebalancing and the
/// automatic defragmenting rebuild are the common case rather than something a long run might eventually
/// reach. The default 512 would need hundreds of thousands of characters to exercise the same code.
/// </para>
///
/// <para>
/// Balance is asserted structurally alongside the text. An unbalanced rope still returns the right characters
/// — it just returns them slowly — so a tree that silently degenerated into a list would pass a
/// content-only comparison while losing the entire point of the type.
/// </para>
/// </summary>
public class RopeDifferentialTests
{
    private static readonly Gen<(int ChunkSize, uint Seed)> GenScenario =
        Gen.Select(Gen.Int[Rope.MinChunkSize, 24], Gen.UInt);

    // The *exact* AVL bound rather than a logarithmic approximation of it: a minimal AVL tree of height h has
    // Fib(h + 1) leaves, so the tallest a tree of L leaves may be is the largest h with Fib(h + 1) <= L. The
    // looser 1.45 log2(L) + 2 form is what this started as, and it was too slack to be worth much — it misses
    // a tree that is one whole level past legal, which is exactly the shape a one-step rebalance applied to a
    // child that grew by many levels at once produces. Pinning the real bound is what makes this able to fail.
    private static void AssertBalanced(Rope rope)
    {
        if (rope.LeafCount <= 1)
        {
            Assert.Equal(rope.LeafCount, rope.Depth);
            return;
        }

        int bound = MaxAvlHeight(rope.LeafCount);
        Assert.True(
            rope.Depth <= bound,
            $"Depth {rope.Depth} exceeds the exact AVL bound {bound} for {rope.LeafCount} leaves.");
    }

    // The tallest an AVL tree of this many leaves can be. N(h), the fewest leaves at height h, runs 1, 2, 3,
    // 5, 8, 13, ... — leaves are height 1 and every internal node has exactly two children, so
    // N(h) = N(h-1) + N(h-2). The answer is the largest h whose N(h) still fits in the leaf count.
    private static int MaxAvlHeight(int leaves)
    {
        int previous = 1;   // N(1)
        int current = 2;    // N(2)
        int height = 1;
        while (current <= leaves)
        {
            int next = previous + current;
            previous = current;
            current = next;
            height++;
        }

        return height;
    }

    private static string Alphabet(Random rand, int length)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = (char)('a' + rand.Next(26));

        return new string(chars);
    }

    // ---- structural validation over the private node graph -------------------------------------------
    //
    // Reflection into the tree, following the pattern DequeEnumerationTests and RankedSetTests already use to
    // reach a collection's internals. The public Depth / LeafCount pair only bounds the *root* height, and
    // that is necessary but not sufficient for the AVL invariant: a locally unbalanced node can sit inside a
    // tree whose overall height is still legal, which is exactly the form the large-insert defect took before
    // it was found — the rope was one level past legal only for some shapes, and structurally wrong for many
    // more. This walks every node instead, and checks the cached metadata each one carries as well, since a
    // stale Length or Leaves would corrupt every descent that trusts it.

    private static readonly FieldInfo RootField =
        typeof(Rope).GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static FieldInfo Field(object node, string name) =>
        node.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public)!;

    private static void AssertStructurallyValid(Rope rope)
    {
        object? root = RootField.GetValue(rope);
        if (root is null)
        {
            Assert.Equal(0, rope.Length);
            Assert.Equal(0, rope.LeafCount);
            Assert.Equal(0, rope.Depth);
            return;
        }

        (int length, int leaves, int height) = ValidateNode(root, rope.ChunkSize);

        Assert.Equal(rope.Length, length);
        Assert.Equal(rope.LeafCount, leaves);
        Assert.Equal(rope.Depth, height);
    }

    private static (int Length, int Leaves, int Height) ValidateNode(object node, int chunkSize)
    {
        var text = (char[]?)Field(node, "Text").GetValue(node);
        object? left = Field(node, "Left").GetValue(node);
        object? right = Field(node, "Right").GetValue(node);
        int length = (int)Field(node, "Length").GetValue(node)!;
        int leaves = (int)Field(node, "Leaves").GetValue(node)!;
        int height = (int)Field(node, "Height").GetValue(node)!;

        if (text is not null)
        {
            Assert.Null(left);
            Assert.Null(right);
            Assert.Equal(chunkSize, text.Length);       // ChunkSize is a real bound, not a preference
            Assert.InRange(length, 1, text.Length);     // an emptied leaf is unlinked, never kept
            Assert.Equal(1, leaves);
            Assert.Equal(1, height);
            return (length, leaves, height);
        }

        Assert.NotNull(left);
        Assert.NotNull(right);

        (int leftLength, int leftLeaves, int leftHeight) = ValidateNode(left!, chunkSize);
        (int rightLength, int rightLeaves, int rightHeight) = ValidateNode(right!, chunkSize);

        // The AVL invariant itself, at this node rather than at the root.
        Assert.InRange(leftHeight - rightHeight, -1, 1);

        // And the cached metadata every descent trusts.
        Assert.Equal(leftLength + rightLength, length);
        Assert.Equal(leftLeaves + rightLeaves, leaves);
        Assert.Equal(1 + Math.Max(leftHeight, rightHeight), height);

        return (length, leaves, height);
    }

    /// <summary>
    /// Randomized operation sequences validated <i>per node</i> after every step, rather than by the root-height
    /// bound the other properties use. This is the assertion that can see a locally unbalanced node, and it is
    /// deliberately run over shorter sequences and smaller ropes than
    /// <see cref="AnEditSequence_ShouldAgreeWithStringBuilder_WhenDrivenRandomly"/>, because walking the whole
    /// tree through reflection on every step is far more expensive than comparing two strings.
    /// </summary>
    [Fact]
    public void EveryNode_ShouldSatisfyTheAvlInvariantAndItsCachedMetadata_WhenDrivenRandomly()
    {
        Gen<(int ChunkSize, uint Seed)> gen = Gen.Select(Gen.Int[Rope.MinChunkSize, 16], Gen.UInt);
        gen.Sample(spec =>
        {
            var rand = new Random((int)spec.Seed);
            var rope = new Rope(spec.ChunkSize);
            var oracle = new StringBuilder();

            for (int step = 0; step < 40; step++)
            {
                switch (rand.Next(6))
                {
                    case 0:
                    case 1:
                    {
                        int index = rand.Next(oracle.Length + 1);
                        string text = Alphabet(rand, rand.Next(1, 10));
                        rope.Insert(index, text);
                        oracle.Insert(index, text);
                        break;
                    }

                    case 2:
                    {
                        // The large insert: this is the operation that grows a child by many levels at once,
                        // and the one whose rebalancing was wrong.
                        int index = rand.Next(oracle.Length + 1);
                        string text = Alphabet(rand, rand.Next(60, 400));
                        rope.Insert(index, text);
                        oracle.Insert(index, text);
                        break;
                    }

                    case 3:
                    {
                        if (oracle.Length == 0)
                            break;

                        int index = rand.Next(oracle.Length);
                        int count = rand.Next(1, oracle.Length - index + 1);
                        rope.Remove(index, count);
                        oracle.Remove(index, count);
                        break;
                    }

                    case 4:
                    {
                        int index = rand.Next(oracle.Length + 1);
                        Rope tail = rope.Split(index);
                        AssertStructurallyValid(tail);
                        rope.AppendAndClear(tail);
                        break;
                    }

                    default:
                    {
                        rope.TrimExcess();
                        break;
                    }
                }

                AssertStructurallyValid(rope);
                Assert.Equal(oracle.Length, rope.Length);
            }

            Assert.Equal(oracle.ToString(), rope.ToString());
        }, iter: 200);
    }

    [Fact]
    public void AnEditSequence_ShouldAgreeWithStringBuilder_WhenDrivenRandomly()
    {
        GenScenario.Sample(spec =>
        {
            var rand = new Random((int)spec.Seed);
            var rope = new Rope(spec.ChunkSize);
            var oracle = new StringBuilder();

            for (int step = 0; step < 250; step++)
            {
                switch (rand.Next(10))
                {
                    case 0:
                    case 1:
                    case 2:
                    {
                        // Insert somewhere, including both ends.
                        int index = rand.Next(oracle.Length + 1);
                        string text = Alphabet(rand, rand.Next(1, 20));
                        rope.Insert(index, text);
                        oracle.Insert(index, text);
                        break;
                    }

                    case 3:
                    {
                        // An insertion long enough to span several leaves on its own.
                        int index = rand.Next(oracle.Length + 1);
                        string text = Alphabet(rand, rand.Next(20, 120));
                        rope.Insert(index, text);
                        oracle.Insert(index, text);
                        break;
                    }

                    case 4:
                    case 5:
                    {
                        if (oracle.Length == 0)
                            break;

                        int index = rand.Next(oracle.Length);
                        int count = rand.Next(1, oracle.Length - index + 1);
                        rope.Remove(index, count);
                        oracle.Remove(index, count);
                        break;
                    }

                    case 6:
                    {
                        string appended = Alphabet(rand, rand.Next(1, 40));
                        rope.Append(appended);
                        oracle.Append(appended);
                        break;
                    }

                    case 7:
                    {
                        if (oracle.Length == 0)
                            break;

                        int index = rand.Next(oracle.Length);
                        char replacement = (char)('A' + rand.Next(26));
                        rope[index] = replacement;
                        oracle[index] = replacement;
                        break;
                    }

                    case 8:
                    {
                        // Split and rejoin in the opposite order: a rotation, which exercises the AVL join
                        // between two arbitrary trees rather than the append-shaped one.
                        int index = rand.Next(oracle.Length + 1);
                        Rope tail = rope.Split(index);
                        string head = rope.ToString();
                        tail.AppendAndClear(rope);
                        rope.AppendAndClear(tail);

                        string all = oracle.ToString();
                        oracle.Clear();
                        oracle.Append(all[index..]).Append(all[..index]);

                        Assert.Equal(0, tail.Length);
                        Assert.Equal(head, all[..index]);
                        break;
                    }

                    default:
                    {
                        rope.TrimExcess();
                        break;
                    }
                }

                Assert.Equal(oracle.Length, rope.Length);
                AssertBalanced(rope);
            }

            Assert.Equal(oracle.ToString(), rope.ToString());

            // Every position agrees, not only the whole string: the indexer descends the tree independently of
            // the chunk walk ToString uses, so the two can disagree.
            for (int i = 0; i < oracle.Length; i++)
                Assert.Equal(oracle[i], rope[i]);
        });
    }

    [Fact]
    public void TrimExcess_ShouldPreserveTheTextAndCompactTheLeaves_WhenTheRopeIsFragmented()
    {
        GenScenario.Sample(spec =>
        {
            var rand = new Random((int)spec.Seed);
            var rope = new Rope(spec.ChunkSize);
            var oracle = new StringBuilder();

            for (int step = 0; step < 60; step++)
            {
                int index = rand.Next(oracle.Length + 1);
                string text = Alphabet(rand, rand.Next(1, 6));
                rope.Insert(index, text);
                oracle.Insert(index, text);
            }

            rope.TrimExcess();

            int fill = rope.ChunkSize - (rope.ChunkSize / 4);
            int ideal = (rope.Length + fill - 1) / fill;
            Assert.Equal(ideal, rope.LeafCount);
            Assert.Equal(oracle.ToString(), rope.ToString());
            AssertBalanced(rope);
        });
    }

    [Fact]
    public void Chunks_ShouldConcatenateToTheWholeText_WhenTheRopeIsFragmented()
    {
        GenScenario.Sample(spec =>
        {
            var rand = new Random((int)spec.Seed);
            var rope = new Rope(spec.ChunkSize);
            var oracle = new StringBuilder();

            for (int step = 0; step < 80; step++)
            {
                int index = rand.Next(oracle.Length + 1);
                string text = Alphabet(rand, rand.Next(1, 30));
                rope.Insert(index, text);
                oracle.Insert(index, text);

                if (rand.Next(4) == 0 && oracle.Length > 0)
                {
                    int at = rand.Next(oracle.Length);
                    int count = rand.Next(1, oracle.Length - at + 1);
                    rope.Remove(at, count);
                    oracle.Remove(at, count);
                }
            }

            var rebuilt = new StringBuilder();
            int leaves = 0;
            foreach (ReadOnlySpan<char> chunk in rope.GetChunks())
            {
                Assert.False(chunk.IsEmpty);
                rebuilt.Append(chunk);
                leaves++;
            }

            Assert.Equal(oracle.ToString(), rebuilt.ToString());
            Assert.Equal(rope.LeafCount, leaves);
        });
    }
}
