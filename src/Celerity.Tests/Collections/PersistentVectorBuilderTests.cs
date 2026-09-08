using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// <see cref="PersistentVector{T}.Builder"/>'s contract.
///
/// <para>
/// The builder exists to skip the vector-per-element cost of repeated <see cref="PersistentVector{T}.Add"/>,
/// and it does that by <b>owning its tail buffer and writing into it in place</b>. That is the one place the
/// library's usual "immutable means nothing is ever written" argument does not hold on its own, so what is on
/// trial here is the isolation: a vector handed out by <see cref="PersistentVector{T}.Builder.ToImmutable"/>
/// must not see any later change to the builder, whether that change lands in the tail (an ordinary append),
/// pushes the tail into the trie (every thirty-second append), or path-copies through the trie (an indexed
/// set). Each of the three is driven across a <c>ToImmutable</c> below.
/// </para>
/// </summary>
public class PersistentVectorBuilderTests
{
    [Fact]
    public void Builder_ShouldStartEmpty()
    {
        var builder = new PersistentVector<int>.Builder();

        Assert.Equal(0, builder.Count);
        Assert.Same(PersistentVector<int>.Empty, builder.ToImmutable());
    }

    [Fact]
    public void ToBuilder_ShouldStartFromTheVectorsElements()
    {
        PersistentVector<int> vector = new(Enumerable.Range(0, 1_100));

        PersistentVector<int>.Builder builder = vector.ToBuilder();

        Assert.Equal(1_100, builder.Count);
        Assert.Equal(vector.ToArray(), builder.ToImmutable().ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(1_057)]
    [InlineData(2_000)]
    public void Add_ShouldProduceTheSameVectorAsRepeatedAdd(int length)
    {
        var builder = new PersistentVector<int>.Builder();
        for (int i = 0; i < length; i++)
            builder.Add(i);

        Assert.Equal(length, builder.Count);
        Assert.Equal(Enumerable.Range(0, length).ToArray(), builder.ToImmutable().ToArray());
    }

    [Fact]
    public void Indexer_ShouldReadFromBothTheTrieAndTheOwnedTail()
    {
        PersistentVector<int>.Builder builder = new PersistentVector<int>(Enumerable.Range(0, 1_100)).ToBuilder();

        Assert.Equal(0, builder[0]);
        Assert.Equal(500, builder[500]);
        Assert.Equal(1_099, builder[1_099]);
    }

    [Fact]
    public void IndexerSet_ShouldReplaceInBothTheTrieAndTheOwnedTail()
    {
        PersistentVector<int>.Builder builder = new PersistentVector<int>(Enumerable.Range(0, 1_100)).ToBuilder();

        builder[500] = -1;
        builder[1_099] = -2;

        Assert.Equal(-1, builder[500]);
        Assert.Equal(-2, builder[1_099]);

        int[] expected = Enumerable.Range(0, 1_100).ToArray();
        expected[500] = -1;
        expected[1_099] = -2;
        Assert.Equal(expected, builder.ToImmutable().ToArray());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Indexer_ShouldThrowArgumentOutOfRangeException_WhenTheIndexIsOutsideTheBuilder(int index)
    {
        PersistentVector<int>.Builder builder = new PersistentVector<int>(new[] { 1, 2, 3 }).ToBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder[index]);
        Assert.Throws<ArgumentOutOfRangeException>(() => builder[index] = 0);
    }

    [Fact]
    public void AddRange_ShouldAppendInOrder()
    {
        var builder = new PersistentVector<int>.Builder();

        builder.AddRange(new[] { 1, 2 });
        builder.AddRange(Enumerable.Range(3, 60));

        Assert.Equal(62, builder.Count);
        Assert.Equal(Enumerable.Range(1, 62).ToArray(), builder.ToImmutable().ToArray());
    }

    [Fact]
    public void AddRange_ShouldThrowArgumentNullException_WhenTheSourceIsNull()
    {
        var builder = new PersistentVector<int>.Builder();

        Assert.Throws<ArgumentNullException>(() => builder.AddRange(null!));
    }

    [Fact]
    public void ToImmutable_ShouldIsolateEarlierVectors_FromAnAppendIntoTheOwnedTail()
    {
        var builder = new PersistentVector<int>.Builder();
        builder.AddRange(Enumerable.Range(0, 5));

        PersistentVector<int> snapshot = builder.ToImmutable();
        builder.Add(99);

        Assert.Equal(5, snapshot.Count);
        Assert.Equal(Enumerable.Range(0, 5).ToArray(), snapshot.ToArray());
        Assert.Equal(6, builder.Count);
    }

    [Fact]
    public void ToImmutable_ShouldIsolateEarlierVectors_WhenTheNextAppendPushesTheTailIntoTheTrie()
    {
        // Stop with a full tail, so the very next append is the one that hands the tail to the trie. If
        // ToImmutable had adopted that buffer by reference instead of copying it, the snapshot would still
        // agree here — but the vector built after the push would be sharing a live array with it, which is
        // what the second half checks.
        var builder = new PersistentVector<int>.Builder();
        builder.AddRange(Enumerable.Range(0, 32));

        PersistentVector<int> snapshot = builder.ToImmutable();

        builder.Add(99);
        builder[31] = -1;

        Assert.Equal(Enumerable.Range(0, 32).ToArray(), snapshot.ToArray());

        int[] expected = Enumerable.Range(0, 33).ToArray();
        expected[31] = -1;
        expected[32] = 99;
        Assert.Equal(expected, builder.ToImmutable().ToArray());
    }

    [Fact]
    public void ToImmutable_ShouldIsolateEarlierVectors_FromAnIndexedSetThroughTheTrie()
    {
        var builder = new PersistentVector<int>.Builder();
        builder.AddRange(Enumerable.Range(0, 1_100));

        PersistentVector<int> snapshot = builder.ToImmutable();
        builder[10] = -1;
        builder[1_099] = -2;

        Assert.Equal(Enumerable.Range(0, 1_100).ToArray(), snapshot.ToArray());
        Assert.Equal(-1, builder[10]);
    }

    [Fact]
    public void ToImmutable_ShouldStayUsable_AcrossManyRounds()
    {
        var builder = new PersistentVector<int>.Builder();
        var snapshots = new List<PersistentVector<int>>();

        for (int i = 0; i < 200; i++)
        {
            builder.Add(i);
            snapshots.Add(builder.ToImmutable());
        }

        for (int i = 0; i < snapshots.Count; i++)
            Assert.Equal(Enumerable.Range(0, i + 1).ToArray(), snapshots[i].ToArray());
    }

    [Fact]
    public void AddRangeOnAVector_ShouldNotDisturbTheReceiversTail()
    {
        // PersistentVector.AddRange goes through a builder seeded from the receiver, so the receiver's own
        // (exactly sized, shared) tail must be copied into the builder's owned buffer rather than written to.
        PersistentVector<int> original = new(Enumerable.Range(0, 5));

        PersistentVector<int> extended = original.AddRange(Enumerable.Range(5, 60));

        Assert.Equal(Enumerable.Range(0, 5).ToArray(), original.ToArray());
        Assert.Equal(Enumerable.Range(0, 65).ToArray(), extended.ToArray());
    }
}
