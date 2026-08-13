using Expressif.LanguageServer.Diagnostics;
using Expressif.Syntax;
using NUnit.Framework;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Expressif.LanguageServer.Tests;

[TestFixture]
public sealed class SyntaxDiagnosticMapperTests
{
    [Test]
    public void Map_ParserSpan_ProducesExactLspRange()
    {
        const string source = "add(1";
        var diagnostic = SyntaxDiagnosticMapper.Map(
            source, new SyntaxError("ERROR", new SourceSpan(4, 1), "1", false));

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Range, Is.EqualTo(new Range(0, 4, 0, 5)));
            Assert.That(diagnostic.Message, Is.EqualTo("Unexpected syntax '1'."));
            Assert.That(diagnostic.Source, Is.EqualTo("expressif"));
        });
    }

    [Test]
    public void Map_MultilineUtf8Span_UsesZeroBasedUtf16Position()
    {
        const string source = "é\nadd(";
        var diagnostic = SyntaxDiagnosticMapper.Map(
            source, new SyntaxError(")", new SourceSpan(7, 0), "", true));

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Range, Is.EqualTo(new Range(1, 4, 1, 4)));
            Assert.That(diagnostic.Message, Is.EqualTo("Missing )."));
        });
    }

    [Test]
    public void Map_SpanPastEndOfDocument_ClampsToEnd()
    {
        const string source = "add(";
        var diagnostic = SyntaxDiagnosticMapper.Map(
            source, new SyntaxError(")", new SourceSpan(100, 2), "", true));

        Assert.That(diagnostic.Range, Is.EqualTo(new Range(0, 4, 0, 4)));
    }
}
