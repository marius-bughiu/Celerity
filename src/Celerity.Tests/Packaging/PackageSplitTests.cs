using System;
using System.Linq;
using System.Reflection;
using Celerity.Collections;
using Celerity.Hashing;
using Celerity.Primitives;
using Xunit;

namespace Celerity.Tests.Packaging;

/// <summary>
/// Guards the milestone 2.0.0 multi-package split (#186, #187, #188). The library is now three
/// assemblies — <c>Celerity.Primitives</c> ← <c>Celerity.Hashing</c> ← <c>Celerity</c> (the
/// collections assembly, <c>PackageId</c> <c>Celerity.Collections</c>) — and these tests pin both
/// that every public type lives in the assembly its package ships, and that the dependency direction
/// stays acyclic (the lower layers must never reference an upper one). The whole test project reaches
/// all three through a single <c>Celerity.Collections</c> project reference, so its very compilation
/// exercises the transitive source back-compat contract.
/// </summary>
public class PackageSplitTests
{
    private const string CollectionsAssembly = "Celerity";
    private const string HashingAssembly = "Celerity.Hashing";
    private const string PrimitivesAssembly = "Celerity.Primitives";

    private static string AssemblyOf(Type t) => t.Assembly.GetName().Name!;

    [Fact]
    public void Collections_live_in_the_collections_assembly()
    {
        Assert.Equal(CollectionsAssembly, AssemblyOf(typeof(BitSet)));
        Assert.Equal(CollectionsAssembly, AssemblyOf(typeof(IntDictionary<int>)));
        Assert.Equal(CollectionsAssembly, AssemblyOf(typeof(CelerityDictionary<int, int, Int32Murmur3Hasher>)));
    }

    [Fact]
    public void Hashers_and_evaluators_live_in_the_hashing_assembly()
    {
        Assert.Equal(HashingAssembly, AssemblyOf(typeof(IHashProvider<int>)));
        Assert.Equal(HashingAssembly, AssemblyOf(typeof(Int32Murmur3Hasher)));
        Assert.Equal(HashingAssembly, AssemblyOf(typeof(StringMurmur3Hasher)));
        Assert.Equal(HashingAssembly, AssemblyOf(typeof(DefaultHasher<int>)));
        Assert.Equal(HashingAssembly, AssemblyOf(typeof(HashQualityEvaluator)));
        Assert.Equal(HashingAssembly, AssemblyOf(typeof(ProbeStatisticsEvaluator)));
    }

    [Fact]
    public void Primitives_live_in_the_primitives_assembly()
    {
        Assert.Equal(PrimitivesAssembly, AssemblyOf(typeof(FastUtils)));
        Assert.Equal(PrimitivesAssembly, AssemblyOf(typeof(VarInt)));
        Assert.Equal(PrimitivesAssembly, AssemblyOf(typeof(FastGuid)));
        Assert.Equal(PrimitivesAssembly, AssemblyOf(typeof(SplitMix64)));
        Assert.Equal(PrimitivesAssembly, AssemblyOf(typeof(IRandomSource)));
    }

    [Fact]
    public void FastUtils_now_lives_in_the_Celerity_Primitives_namespace()
    {
        // The one intentional source-breaking move of the split (#187): FastUtils left the root
        // Celerity namespace to join the other primitives.
        Assert.Equal("Celerity.Primitives", typeof(FastUtils).Namespace);
    }

    [Fact]
    public void Primitives_does_not_reference_hashing_or_collections()
    {
        string[] referenced = typeof(FastUtils).Assembly
            .GetReferencedAssemblies().Select(a => a.Name!).ToArray();

        Assert.DoesNotContain(HashingAssembly, referenced);
        Assert.DoesNotContain(CollectionsAssembly, referenced);
    }

    [Fact]
    public void Hashing_references_primitives_but_not_collections()
    {
        string[] referenced = typeof(IHashProvider<>).Assembly
            .GetReferencedAssemblies().Select(a => a.Name!).ToArray();

        Assert.Contains(PrimitivesAssembly, referenced);
        Assert.DoesNotContain(CollectionsAssembly, referenced);
    }

    [Fact]
    public void Collections_reference_both_lower_packages()
    {
        string[] referenced = typeof(BitSet).Assembly
            .GetReferencedAssemblies().Select(a => a.Name!).ToArray();

        Assert.Contains(HashingAssembly, referenced);
        Assert.Contains(PrimitivesAssembly, referenced);
    }

    [Fact]
    public void All_three_layers_interoperate_through_one_collections_reference()
    {
        // A collection (Celerity) parameterised on a hasher (Celerity.Hashing) and a primitive
        // (Celerity.Primitives), all reachable from the single Celerity.Collections project ref.
        var dict = new CelerityDictionary<int, int, Int32Murmur3Hasher>();
        dict.Add(FastUtils.NextPowerOfTwo(5), 42); // NextPowerOfTwo(5) == 8
        Assert.True(dict.TryGetValue(8, out int value));
        Assert.Equal(42, value);

        IHashProvider<int> hasher = default(Int32Murmur3Hasher);
        Assert.Equal(hasher.Hash(8), default(Int32Murmur3Hasher).Hash(8));
    }
}
