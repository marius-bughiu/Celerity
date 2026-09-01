using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

/// <summary>
/// <see cref="Rope"/> against <see cref="StringBuilder"/>, which is what a caller reaches for when a block of
/// text has to change, and against <see cref="List{T}"/> of <see cref="char"/> as the naive arm.
/// </summary>
/// <remarks>
/// <para>
/// The unit that matters is <c>Edit</c>: a run of five-character insertions and removals at scattered
/// positions in a document that is already large, with each insertion paired with a removal elsewhere so the
/// document ends the round at the length it started. That is the workload the type exists for, and the rope
/// pays its automatic defragmenting rebuild inside this group rather than beside it, which is the honest place
/// for it. <c>Insert</c> and <c>Remove</c> take the round apart.
/// </para>
/// <para>
/// <b>Every mutating group rebuilds its containers per iteration</b>, which costs an
/// <see cref="IterationSetupAttribute"/> and one invocation per iteration — so those four groups carry
/// visibly more error than the read groups, and should be read as directions rather than as ratios to three
/// figures. The reason is the baseline rather than this type: a <see cref="StringBuilder"/> splits a chunk on
/// every mid-document insertion and never merges them back, so its chunk list grows without bound even
/// though its <i>length</i> does not, and an arm left to run against a persistent instance gets steadily
/// slower the more invocations BenchmarkDotNet happens to choose. Left alone, that makes the baseline a
/// function of the harness rather than of the workload — the per-op cost was still climbing after sixteen
/// warmup iterations at a million characters.
/// </para>
/// <para>
/// The read groups need no such reset. <c>Index</c> measures random access against a
/// <see cref="StringBuilder"/> in its <i>best</i> shape — built from one string, so it is a single chunk that
/// indexes directly rather than the multi-chunk walk an edited one degrades into — and <c>Materialize</c> is
/// <see cref="StringBuilder.ToString()"/> against <see cref="Rope.ToString()"/>, where both are a full copy.
/// Both are losses, and both are published as such.
/// </para>
/// <para>
/// <c>Append</c> is in here to be lost. Appending to the end of a <see cref="StringBuilder"/> is a bounds
/// check and a store, a rope has to descend a tree first, and publishing that is the house rule.
/// <c>SplitJoin</c> is the mirror image: cutting a document in two and rejoining it is <c>O(log n)</c> for a
/// rope and a full copy for a <see cref="StringBuilder"/>, which has no such operation and has to go through
/// <see cref="StringBuilder.ToString()"/> to fake one. It rebuilds per iteration for the same reason the edit
/// groups do — a split leaves a leaf boundary behind it, so repeating one at a fixed position would measure
/// the boundary path rather than a real cut.
/// </para>
/// <para>
/// The two document sizes are chosen to straddle the crossover rather than to flatter the type. At ten
/// thousand characters a <see cref="StringBuilder"/> edit is one short <c>memmove</c> and the tree descent is
/// pure overhead; at a million it is not. Both arms ship.
/// </para>
/// <para>
/// Only the <see cref="StringBuilder"/> arms carry the dashboard's op names; the <see cref="List{T}"/> arm is
/// suffixed (<c>EditNaive</c>) so each dashboard card still resolves to one BCL measurement and one Celerity
/// one, while the report keeps all three in the same category with ratios against the same baseline.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RopeBenchmark
{
    // Edits per invocation. Large enough that the per-invocation overhead is noise even for the rope arms,
    // small enough that the StringBuilder arms at a million characters stay in milliseconds.
    private const int EditCount = 200;

    // Probes per invocation for the read groups, which are far cheaper per operation than an edit.
    private const int ProbeCount = 500;

    // Split-and-rejoin cycles per invocation. Each cuts at a different position, because cutting repeatedly at
    // the *same* one measures the wrong thing: the first cut splits the leaf it lands in and leaves a leaf
    // boundary there, so every later cut at that position takes the boundary path and never pays for the
    // one-leaf copy a real split does. The rope also gains a leaf per cycle, which is why this group rebuilds
    // its containers per iteration rather than running against a persistent instance.
    private const int SplitCount = 100;

    // The Remove arms shrink the document as they go, so their positions are taken modulo what is left. The
    // floor keeps that modulus positive whatever the document size is set to.
    private const int MinimumRemainder = 1;

    // Five characters is the size of an edit a text editor actually produces — a keystroke, a paste of a
    // word, an autocomplete. It is also short enough to fit in a leaf that has room, which is the path the
    // rope is written around.
    private const string Fragment = "hello";

    private string source = null!;
    private int[] insertPositions = null!;
    private int[] removePositions = null!;
    private int[] probePositions = null!;

    private Rope rope = null!;
    private StringBuilder builder = null!;
    private List<char> list = null!;

    // Named ItemCount rather than something more descriptive because the dashboard's benchmark-name parser
    // matches that suffix strictly, and a differently named params property renders a blank card. The unit
    // here is characters of document.
    [Params(10_000, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        var chars = new char[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            chars[i] = (char)('a' + rand.Next(26));

        source = new string(chars);

        insertPositions = new int[EditCount];
        removePositions = new int[EditCount];
        for (int i = 0; i < EditCount; i++)
        {
            insertPositions[i] = rand.Next(ItemCount);
            removePositions[i] = rand.Next(ItemCount - Fragment.Length);
        }

        probePositions = new int[ProbeCount];
        for (int i = 0; i < ProbeCount; i++)
            probePositions[i] = rand.Next(ItemCount);

        rope = new Rope(source);
        builder = new StringBuilder(source);
        list = [.. source];
    }

    // ---- Edit: the round, and the reason the type exists ---------------------------------------------

    [IterationSetup(Target = nameof(StringBuilder_Edit))]
    public void SetupForBuilderEdit() => builder = new StringBuilder(source);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Edit")]
    public int StringBuilder_Edit()
    {
        for (int i = 0; i < EditCount; i++)
        {
            builder.Insert(insertPositions[i], Fragment);
            builder.Remove(removePositions[i], Fragment.Length);
        }

        return builder.Length;
    }

    [IterationSetup(Target = nameof(List_EditNaive))]
    public void SetupForListEdit() => list = [.. source];

    [Benchmark]
    [BenchmarkCategory("Edit")]
    public int List_EditNaive()
    {
        for (int i = 0; i < EditCount; i++)
        {
            list.InsertRange(insertPositions[i], Fragment);
            list.RemoveRange(removePositions[i], Fragment.Length);
        }

        return list.Count;
    }

    [IterationSetup(Target = nameof(Rope_Edit))]
    public void SetupForRopeEdit() => rope = new Rope(source);

    [Benchmark]
    [BenchmarkCategory("Edit")]
    public int Rope_Edit()
    {
        for (int i = 0; i < EditCount; i++)
        {
            rope.Insert(insertPositions[i], Fragment);
            rope.Remove(removePositions[i], Fragment.Length);
        }

        return rope.Length;
    }

    // ---- Insert on its own -----------------------------------------------------------------------------

    [IterationSetup(Target = nameof(StringBuilder_Insert))]
    public void SetupForBuilderInsert() => builder = new StringBuilder(source);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert")]
    public int StringBuilder_Insert()
    {
        for (int i = 0; i < EditCount; i++)
            builder.Insert(insertPositions[i], Fragment);

        return builder.Length;
    }

    [IterationSetup(Target = nameof(Rope_Insert))]
    public void SetupForRopeInsert() => rope = new Rope(source);

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public int Rope_Insert()
    {
        for (int i = 0; i < EditCount; i++)
            rope.Insert(insertPositions[i], Fragment);

        return rope.Length;
    }

    // ---- Remove on its own -----------------------------------------------------------------------------

    [IterationSetup(Target = nameof(StringBuilder_Remove))]
    public void SetupForBuilderRemove() => builder = new StringBuilder(source);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public int StringBuilder_Remove()
    {
        for (int i = 0; i < EditCount; i++)
            builder.Remove(
                removePositions[i] % Math.Max(builder.Length - Fragment.Length, MinimumRemainder),
                Fragment.Length);

        return builder.Length;
    }

    [IterationSetup(Target = nameof(Rope_Remove))]
    public void SetupForRopeRemove() => rope = new Rope(source);

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public int Rope_Remove()
    {
        for (int i = 0; i < EditCount; i++)
            rope.Remove(
                removePositions[i] % Math.Max(rope.Length - Fragment.Length, MinimumRemainder),
                Fragment.Length);

        return rope.Length;
    }

    // ---- Index: the read StringBuilder is quietly bad at -----------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Index")]
    public int StringBuilder_Index()
    {
        int sum = 0;
        for (int i = 0; i < ProbeCount; i++)
            sum += builder[probePositions[i] % builder.Length];

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Index")]
    public int Rope_Index()
    {
        int sum = 0;
        for (int i = 0; i < ProbeCount; i++)
            sum += rope[probePositions[i] % rope.Length];

        return sum;
    }

    // ---- Append: StringBuilder's home turf, published as the loss it is --------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Append")]
    public int StringBuilder_Append()
    {
        var target = new StringBuilder(ItemCount);
        for (int i = 0; i < ItemCount; i += Fragment.Length)
            target.Append(Fragment);

        return target.Length;
    }

    [Benchmark]
    [BenchmarkCategory("Append")]
    public int Rope_Append()
    {
        var target = new Rope();
        for (int i = 0; i < ItemCount; i += Fragment.Length)
            target.Append(Fragment);

        return target.Length;
    }

    // ---- Materialize: both are a full copy -------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Materialize")]
    public int StringBuilder_Materialize() => builder.ToString().Length;

    [Benchmark]
    [BenchmarkCategory("Materialize")]
    public int Rope_Materialize() => rope.ToString().Length;

    // ---- SplitJoin: the operation StringBuilder has to fake --------------------------------------------

    [IterationSetup(Target = nameof(StringBuilder_SplitJoin))]
    public void SetupForBuilderSplitJoin() => builder = new StringBuilder(source);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SplitJoin")]
    public int StringBuilder_SplitJoin()
    {
        for (int i = 0; i < SplitCount; i++)
        {
            int at = probePositions[i] % builder.Length;
            string head = builder.ToString(0, at);
            string tail = builder.ToString(at, builder.Length - at);
            builder = new StringBuilder(head.Length + tail.Length);
            builder.Append(head).Append(tail);
        }

        return builder.Length;
    }

    [IterationSetup(Target = nameof(Rope_SplitJoin))]
    public void SetupForRopeSplitJoin() => rope = new Rope(source);

    [Benchmark]
    [BenchmarkCategory("SplitJoin")]
    public int Rope_SplitJoin()
    {
        for (int i = 0; i < SplitCount; i++)
        {
            Rope tail = rope.Split(probePositions[i] % rope.Length);
            rope.AppendAndClear(tail);
        }

        return rope.Length;
    }
}
