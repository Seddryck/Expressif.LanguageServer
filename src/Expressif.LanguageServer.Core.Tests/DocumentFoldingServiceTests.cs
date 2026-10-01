using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Folding;
using Expressif.LanguageServer.Core.Syntax;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class DocumentFoldingServiceTests
{
    private readonly DocumentFoldingService service = new();

    [Test]
    public void GetRanges_MultilinePipelines_ReturnsOuterAndNestedRanges()
    {
        const string text = "@items\n| map(.name\n    | upper)\n| distinct\n| sort";

        var ranges = service.GetRanges(CreateDocument(text));

        Assert.That(ranges, Does.Contain(new DocumentFoldingRange(
            new FoldingPosition(0, 6), new FoldingPosition(4, 6))));
        Assert.That(ranges, Does.Contain(new DocumentFoldingRange(
            new FoldingPosition(1, 11), new FoldingPosition(2, 11))));
    }

    [TestCase("|")]
    [TestCase("|>")]
    [TestCase("|#>")]
    public void GetRanges_AllPipelineOperators_ReturnsRange(string pipelineOperator)
    {
        var text = $"@items\n{pipelineOperator} sort";

        var ranges = service.GetRanges(CreateDocument(text));

        Assert.That(ranges, Does.Contain(new DocumentFoldingRange(
            new FoldingPosition(0, 6),
            new FoldingPosition(1, pipelineOperator.Length + 5))));
    }

    [Test]
    public void GetRanges_OpenPipeline_ReturnsRangeAfterFirstStage()
    {
        const string text = "trim\n| upper\n| lower";

        var ranges = service.GetRanges(CreateDocument(text));

        Assert.That(ranges, Does.Contain(new DocumentFoldingRange(
            new FoldingPosition(0, 4), new FoldingPosition(2, 7))));
    }

    [Test]
    public void GetRanges_DelimitedConstructs_ReturnsProperlyNestedRanges()
    {
        const string text = "record(\n    person := {\n        name := \"Ada\",\n        scores := T(\n            12,\n            18\n        )\n    }\n)";

        var ranges = service.GetRanges(CreateDocument(text));

        Assert.That(ranges, Does.Contain(new DocumentFoldingRange(
            new FoldingPosition(0, 6), new FoldingPosition(8, 1))));
        Assert.That(ranges, Does.Contain(new DocumentFoldingRange(
            new FoldingPosition(1, 14), new FoldingPosition(7, 5))));
        Assert.That(ranges, Does.Contain(new DocumentFoldingRange(
            new FoldingPosition(3, 19), new FoldingPosition(6, 9))));
        Assert.That(ranges.Select(range => (range.Start.Line, range.Start.Character)),
            Is.EqualTo(ranges
                .OrderBy(range => range.Start.Line)
                .ThenBy(range => range.Start.Character)
                .Select(range => (range.Start.Line, range.Start.Character))));
    }

    [Test]
    public void GetRanges_TaggedRecord_ReturnsRecordDelimiterRange()
    {
        const string text = "SortTable{\n    headers := {1, 2}\n}";

        var ranges = service.GetRanges(CreateDocument(text));

        Assert.That(ranges, Does.Contain(new DocumentFoldingRange(
            new FoldingPosition(0, 9), new FoldingPosition(2, 1))));
    }

    [TestCase("(\n@items\n| sort\n)", 0, 0, 3, 1)]
    [TestCase("{\n1,\n2\n}", 0, 0, 3, 1)]
    [TestCase("#{\n(\"BE\" => {\"Alice\"})\n}", 0, 1, 2, 1)]
    [TestCase("!{\n(\"BE\" => \"Belgium\")\n}", 0, 1, 2, 1)]
    [TestCase("{{1, 2}\n| sum}", 0, 0, 1, 6)]
    public void GetRanges_OtherDelimitedExpressions_ReturnsOuterRange(
        string text,
        int startLine,
        int startCharacter,
        int endLine,
        int endCharacter)
    {
        var ranges = service.GetRanges(CreateDocument(text));

        Assert.That(ranges, Does.Contain(new DocumentFoldingRange(
            new FoldingPosition(startLine, startCharacter),
            new FoldingPosition(endLine, endCharacter))));
    }

    [Test]
    public void GetRanges_MultilinePairLiteral_ReturnsNestedMappingRange()
    {
        const string text = "#{(\n    \"BE\" => {\"Alice\"}\n)}";

        var ranges = service.GetRanges(CreateDocument(text));

        Assert.That(ranges, Does.Contain(new DocumentFoldingRange(
            new FoldingPosition(0, 2), new FoldingPosition(2, 1))));
    }

    [Test]
    public void GetRanges_InputBoundBody_KeepsIntroductionVisible()
    {
        const string text = "@customer\n| :>\n    .orders\n    | map(.total)\n    | sum";

        var ranges = service.GetRanges(CreateDocument(text));

        Assert.That(ranges, Does.Contain(new DocumentFoldingRange(
            new FoldingPosition(1, 4), new FoldingPosition(4, 9))));
    }

    [Test]
    public void GetRanges_BlockComment_ReturnsCommentKindButLineCommentsDoNotFold()
    {
        const string text = "// first\n// second\n@value /* | ({ [\ncomment */ | trim";

        var ranges = service.GetRanges(CreateDocument(text));

        Assert.That(ranges.Where(range => range.Kind == FoldingKind.Comment),
            Is.EqualTo(new[]
            {
                new DocumentFoldingRange(
                    new FoldingPosition(2, 7),
                    new FoldingPosition(3, 10),
                    FoldingKind.Comment)
            }));
    }

    [Test]
    public void GetRanges_Crlf_UsesZeroBasedUtf16Positions()
    {
        const string emoji = "\ud83d\ude00";
        const string expression = "consume(\r\n    \"\ud83d\ude00\",\r\n    2\r\n)";

        var ranges = service.GetRanges(CreateDocument(expression));

        Assert.That(ranges, Does.Contain(new DocumentFoldingRange(
            new FoldingPosition(0, 7), new FoldingPosition(3, 1))));
        Assert.That(emoji.Length, Is.EqualTo(2), "The test fixture confirms .NET/LSP UTF-16 indexing.");
    }

    [Test]
    public void GetRanges_ProtectedTextAndSingleLineConstructs_DoNotCreateFalseRanges()
    {
        const string text = "consume(\"| ({ })\", `name|with|pipes`, #2024-01-01#, {1, 2})";

        var ranges = service.GetRanges(CreateDocument(text));

        Assert.That(ranges, Is.Empty);
    }

    [Test]
    public void GetRanges_IncompleteDocument_ReturnsNoUnsafeRangesAndDoesNotThrow()
    {
        var document = CreateDocument("record(\n    name := \"Ada\"");

        Assert.That(document.SyntaxDocument, Is.Null);
        Assert.That(service.GetRanges(document), Is.Empty);
    }

    private static DocumentSnapshot CreateDocument(string text)
    {
        var parsed = new SyntaxService().Parse(text);
        return new(new Uri("file:///folding.exif"), text, 1, parsed.SyntaxDocument, parsed.Errors);
    }
}
