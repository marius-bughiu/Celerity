using System.Reflection;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Pins the declared return type of each dictionary's primary indexer get to the
/// non-nullable <c>TValue</c>. Closes the divergence flagged in issue #88 where
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/>'s indexer returned
/// <c>TValue?</c> while <see cref="IntDictionary{TValue}"/> and
/// <see cref="LongDictionary{TValue}"/> already returned <c>TValue</c>. Also
/// covers <see cref="RobinHoodDictionary{TKey, TValue, THasher}"/>.
/// </summary>
public class IndexerReturnTypeTests
{
    [Fact]
    public void IntDictionary_PrimaryIndexer_ReturnsNonNullableTValue()
    {
        AssertPrimaryIndexerReturnsTValue(typeof(IntDictionary<int, Int32WangNaiveHasher>), keyType: typeof(int), valueGenericIndex: 0);
    }

    [Fact]
    public void LongDictionary_PrimaryIndexer_ReturnsNonNullableTValue()
    {
        AssertPrimaryIndexerReturnsTValue(typeof(LongDictionary<int, Int64WangHasher>), keyType: typeof(long), valueGenericIndex: 0);
    }

    [Fact]
    public void CelerityDictionary_PrimaryIndexer_ReturnsNonNullableTValue()
    {
        AssertPrimaryIndexerReturnsTValue(typeof(CelerityDictionary<string, int, StringFnV1AHasher>), keyType: typeof(string), valueGenericIndex: 1);
    }

    [Fact]
    public void CelerityDictionary_StringValue_IndexerAssignsToNonNullableLocal_WithoutWarning()
    {
        // Compile-time evidence of the fix: this assignment would emit CS8600
        // ("Converting null literal or possible null value to non-nullable type")
        // if the indexer were still declared as TValue?.
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>
        {
            [1] = "hello",
        };

        string value = map[1];

        Assert.Equal("hello", value);
    }

    [Fact]
    public void RobinHoodDictionary_PrimaryIndexer_ReturnsNonNullableTValue()
    {
        AssertPrimaryIndexerReturnsTValue(typeof(RobinHoodDictionary<string, int, StringFnV1AHasher>), keyType: typeof(string), valueGenericIndex: 1);
    }

    [Fact]
    public void RobinHoodDictionary_StringValue_IndexerAssignsToNonNullableLocal_WithoutWarning()
    {
        // Compile-time evidence of the fix: this assignment would emit CS8600
        // ("Converting null literal or possible null value to non-nullable type")
        // if the indexer were still declared as TValue?.
        var map = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>
        {
            [1] = "hello",
        };

        string value = map[1];

        Assert.Equal("hello", value);
    }

    [Fact]
    public void PooledCelerityDictionary_PrimaryIndexer_ReturnsNonNullableTValue()
    {
        AssertPrimaryIndexerReturnsTValue(typeof(PooledCelerityDictionary<string, int, StringFnV1AHasher>), keyType: typeof(string), valueGenericIndex: 1);
    }

    [Fact]
    public void PooledCelerityDictionary_StringValue_IndexerAssignsToNonNullableLocal_WithoutWarning()
    {
        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>
        {
            [1] = "hello",
        };

        string value = map[1];

        Assert.Equal("hello", value);
    }

    [Fact]
    public void SwissDictionary_PrimaryIndexer_ReturnsNonNullableTValue()
    {
        AssertPrimaryIndexerReturnsTValue(typeof(SwissDictionary<string, int, StringFnV1AHasher>), keyType: typeof(string), valueGenericIndex: 1);
    }

    [Fact]
    public void SwissDictionary_StringValue_IndexerAssignsToNonNullableLocal_WithoutWarning()
    {
        // Compile-time evidence of the fix: this assignment would emit CS8600
        // ("Converting null literal or possible null value to non-nullable type")
        // if the indexer were still declared as TValue?.
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>
        {
            [1] = "hello",
        };

        string value = map[1];

        Assert.Equal("hello", value);
    }

    [Fact]
    public void IntDictionary_StringValue_IndexerAssignsToNonNullableLocal_WithoutWarning()
    {
        var map = new IntDictionary<string>
        {
            [1] = "hello",
        };

        string value = map[1];

        Assert.Equal("hello", value);
    }

    [Fact]
    public void LongDictionary_StringValue_IndexerAssignsToNonNullableLocal_WithoutWarning()
    {
        var map = new LongDictionary<string>
        {
            [1L] = "hello",
        };

        string value = map[1L];

        Assert.Equal("hello", value);
    }

    [Fact]
    public void FrozenCelerityDictionary_PrimaryIndexer_ReturnsNonNullableTValue()
    {
        AssertPrimaryIndexerReturnsTValue(typeof(FrozenCelerityDictionary<int, StringFnV1AHasher>), keyType: typeof(string), valueGenericIndex: 0);
    }

    [Fact]
    public void FrozenCelerityDictionary_StringValue_IndexerAssignsToNonNullableLocal_WithoutWarning()
    {
        var map = new FrozenCelerityDictionary<string>(new[]
        {
            new KeyValuePair<string, string>("k", "hello"),
        });

        string value = map["k"];

        Assert.Equal("hello", value);
    }

    [Fact]
    public void SmallDictionary_PrimaryIndexer_ReturnsNonNullableTValue()
    {
        AssertPrimaryIndexerReturnsTValue(typeof(SmallDictionary<string, int>), keyType: typeof(string), valueGenericIndex: 1);
    }

    [Fact]
    public void SmallDictionary_StringValue_IndexerAssignsToNonNullableLocal_WithoutWarning()
    {
        var map = new SmallDictionary<int, string>
        {
            [1] = "hello",
        };

        string value = map[1];

        Assert.Equal("hello", value);
    }

    private static void AssertPrimaryIndexerReturnsTValue(Type closed, Type keyType, int valueGenericIndex)
    {
        PropertyInfo? indexer = closed
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(p => p.GetIndexParameters().Length == 1
                                  && p.GetIndexParameters()[0].ParameterType == keyType);

        Assert.NotNull(indexer);

        // The primary (non-interface) indexer's PropertyType is the closed
        // TValue type (not the nullable TValue?). The TValue? interface
        // member is implemented explicitly and is therefore non-public.
        Type valueType = closed.GetGenericArguments()[valueGenericIndex];
        Assert.Equal(valueType, indexer!.PropertyType);
    }
}
