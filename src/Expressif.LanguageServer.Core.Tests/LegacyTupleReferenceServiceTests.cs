using Expressif.LanguageServer.Core.Diagnostics;
using Expressif.LanguageServer.Core.Syntax;
using Expressif.Syntax;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class LegacyTupleReferenceServiceTests
{
    private readonly LegacyTupleReferenceService service = new();

    [TestCase("$^0", "$-1")]
    [TestCase("$^1", "$-2")]
    [TestCase("$^12", "$-13")]
    [TestCase("$^2147483646", "$-2147483647")]
    public void Replacement_PreservesFromEndMeaning(string source, string expected)
    {
        var reference = service.GetReferences(ExpressifSyntax.Parse(source)).Single();
        Assert.That(reference.Replacement, Is.EqualTo(expected));
        Assert.That(reference.Message, Is.EqualTo($"Tuple reference '{source}' is deprecated. Use '{expected}' instead."));
        Assert.That(service.GetReferences(ExpressifSyntax.Parse(expected)), Is.Empty);
    }

    [TestCase("{0}")]
    [TestCase("  {0}  ")]
    [TestCase("{0} | upper")]
    [TestCase("lower | {0}")]
    [TestCase("T(1, 2) | {0}")]
    [TestCase("select({0})")]
    [TestCase("select(({0}))")]
    [TestCase("apply(T(1, 2) | add({0}))")]
    [TestCase("select({{{0} | upper}})")]
    [TestCase("select(\n  {0}\n)")]
    [TestCase("select(\"é😀\", {0})")]
    [TestCase("/* é😀 */\r\n  {0}")]
    public void Replacement_PreservesSurroundings(string format)
    {
        var source = string.Format(format, "$^12");
        var reference = service.GetReferences(ExpressifSyntax.Parse(source)).Single();
        Assert.That(reference.Start, Is.EqualTo(source.IndexOf("$^12", StringComparison.Ordinal)));
        Assert.That(reference.Length, Is.EqualTo(4));
        var edited = source.Remove(reference.Start, reference.Length).Insert(reference.Start, reference.Replacement!);
        Assert.That(edited, Is.EqualTo(string.Format(format, "$-13")));
        Assert.That(service.GetReferences(ExpressifSyntax.Parse(edited)), Is.Empty);
    }

    [TestCase("$-1")]
    [TestCase("$0")]
    [TestCase("^$1")]
    [TestCase("^^$1")]
    [TestCase("select(\"$^0\", `$^1`) /* $^2 */ // $^3")]
    public void NonLegacySyntax_HasNoDeprecation(string source)
        => Assert.That(service.GetReferences(ExpressifSyntax.Parse(source)), Is.Empty);

    [TestCase("$-0")]
    [TestCase("$^")]
    [TestCase("$^-1")]
    [TestCase("^^$^1")]
    public void MalformedSyntax_RemainsAParserError(string source)
    {
        var parsed = new SyntaxService().Parse(source);
        Assert.That(parsed.SyntaxTree, Is.Null);
        Assert.That(parsed.Errors, Is.Not.Empty);
    }

    [Test]
    public void MaximumIndex_HasHintWithoutUnsafeReplacement()
    {
        var reference = service.GetReferences(ExpressifSyntax.Parse("$^2147483647")).Single();
        Assert.That(reference.Replacement, Is.Null);
        Assert.That(reference.Message, Does.Contain("deprecated").And.Contain("exceeds the supported range"));
    }

    [TestCase(1, 0, false)]
    [TestCase(2, 0, true)]
    [TestCase(5, 0, true)]
    [TestCase(6, 0, false)]
    [TestCase(0, 2, false)]
    [TestCase(0, 3, true)]
    [TestCase(5, 1, false)]
    public void Selection_UsesReferenceBoundaries(int start, int length, bool expected)
    {
        var reference = service.GetReferences(ExpressifSyntax.Parse("  $^0  ")).Single();
        Assert.That(reference.Overlaps(start, length), Is.EqualTo(expected));
    }
}
