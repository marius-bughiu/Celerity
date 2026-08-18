using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Celerity.Collections;
using Celerity.Hashing;
using Celerity.Primitives;
using Celerity.Sorting;
using Celerity.Statistics;
using Xunit;

namespace Celerity.Tests.Packaging;

/// <summary>
/// Guards the XML documentation file each package ships alongside its assembly (#356).
/// <para>
/// A doc comment that is not well-formed XML does not reach the consumer in truncated form — the doc
/// writer drops the <em>entire</em> member element, so the type loses its summary and remarks in every
/// IDE tooltip and generated reference page while the build still succeeds. That is how
/// <see cref="MaxMonoid{T}"/> shipped with no documentation at all: an unclosed <c>&lt;para&gt;</c>
/// produced two CS1570 warnings that were easy to scroll past.
/// </para>
/// <para>
/// The shipping packages now promote CS1570 to an error, which stops the same slip at the compiler.
/// These tests close the loop from the other end, asserting against the artifact that actually ships:
/// every public type this test project can reach must be present in its package's <c>.xml</c>.
/// The three showcase packages (<c>Celerity.Ring</c>, <c>Celerity.Sentinel</c>,
/// <c>Celerity.Cardinality</c>) are not referenced from here and rely on the same compiler gate.
/// </para>
/// </summary>
public class XmlDocumentationTests
{
    /// <summary>One anchor type per shipping assembly reachable from this test project.</summary>
    private static readonly Type[] AssemblyAnchors =
    [
        typeof(BitSet),             // Celerity            (package Celerity.Collections)
        typeof(IHashProvider<int>), // Celerity.Hashing
        typeof(FastUtils),          // Celerity.Primitives
        typeof(RadixSort),          // Celerity.Sorting
        typeof(DDSketch),           // Celerity.Statistics
    ];

    public static TheoryData<string> ShippingAssemblies
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var anchor in AssemblyAnchors)
                data.Add(anchor.Assembly.GetName().Name!);

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ShippingAssemblies))]
    public void DocumentationFile_ShouldParseAsWellFormedXml_WhenShippedWithTheAssembly(string assemblyName)
    {
        var path = DocumentationPathFor(assemblyName);

        Assert.True(File.Exists(path), $"{assemblyName} ships no XML documentation file at '{path}'.");

        var exception = Record.Exception(() => XDocument.Load(path));

        Assert.Null(exception);
    }

    [Theory]
    [MemberData(nameof(ShippingAssemblies))]
    public void DocumentationFile_ShouldDescribeEveryPublicType_WhenShippedWithTheAssembly(string assemblyName)
    {
        var assembly = AssemblyNamed(assemblyName);
        var documented = DocumentedMemberIds(assemblyName);

        var undocumented = PublicTypesOf(assembly)
            .Select(DocumentationIdOf)
            .Where(id => !documented.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            undocumented.Length == 0,
            $"{assemblyName}.xml is missing an entry for {undocumented.Length} public type(s). A doc "
                + "comment that is not well-formed XML is dropped whole rather than truncated: "
                + string.Join(", ", undocumented));
    }

    [Fact]
    public void MaxMonoid_ShouldShipItsSummaryAndRemarks_WhenTheDocumentationFileIsGenerated()
    {
        var id = DocumentationIdOf(typeof(MaxMonoid<>));
        var member = MemberElement("Celerity", id);

        Assert.NotNull(member);
        Assert.False(string.IsNullOrWhiteSpace(member!.Element("summary")?.Value));

        var remarks = member.Element("remarks");

        Assert.NotNull(remarks);

        // The floating-point caveat is the one thing a caller most needs from this type, and it sat in
        // the <para> the unclosed tag took with it.
        Assert.Contains("NaN", remarks!.Value, StringComparison.Ordinal);
    }

    private static Assembly AssemblyNamed(string name) =>
        AssemblyAnchors.Select(anchor => anchor.Assembly).First(a => a.GetName().Name == name);

    private static string DocumentationPathFor(string assemblyName) =>
        Path.ChangeExtension(AssemblyNamed(assemblyName).Location, ".xml");

    /// <summary>
    /// The public types an assembly declares. <see cref="Assembly.GetExportedTypes"/> also reports the
    /// types the 2.0.0 split forwards on to a lower package, and those are documented where they are
    /// declared, so they are filtered out here.
    /// </summary>
    private static IEnumerable<Type> PublicTypesOf(Assembly assembly) =>
        assembly.GetExportedTypes().Where(t => t.Assembly == assembly);

    /// <summary>
    /// The ID the compiler writes for a type: the metadata name with nested types joined by <c>.</c>
    /// rather than reflection's <c>+</c>, prefixed by <c>T:</c>.
    /// </summary>
    private static string DocumentationIdOf(Type type) => "T:" + type.FullName!.Replace('+', '.');

    private static HashSet<string> DocumentedMemberIds(string assemblyName) =>
        XDocument.Load(DocumentationPathFor(assemblyName))
            .Descendants("member")
            .Select(m => (string?)m.Attribute("name"))
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

    private static XElement? MemberElement(string assemblyName, string id) =>
        XDocument.Load(DocumentationPathFor(assemblyName))
            .Descendants("member")
            .FirstOrDefault(m => (string?)m.Attribute("name") == id);
}
