using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Formatting;
using Expressif.LanguageServer.Core.Syntax;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class DocumentRangeFormatterTests
{
    private readonly DocumentFormatter formatter = new();

    [Test]
    public void FormatRange_InnerCall_LeavesOuterPipelineUnchanged()
    {
        const string source = "@items\n| map(record(\nname:=\"Ada\",\nscore:=add( 1,2 )\n))\n|  distinct";
        var start = source.IndexOf("record", StringComparison.Ordinal);
        var end = source.IndexOf("\n))", start, StringComparison.Ordinal) + 2;

        var edits = FormatRange(source, start, end);
        var result = Apply(source, edits);

        Assert.That(result, Is.EqualTo(
            "@items\n| map(record(\n        name := \"Ada\",\n        score := add(1, 2)\n    ))\n|  distinct"));
    }

    [Test]
    public void FormatRange_CompleteArguments_FormatsOnlySelectedArguments()
    {
        const string source = "10  | add( 1,2 )  |  multiply( 3,4 )";
        var start = source.IndexOf('1', source.IndexOf('('));
        var end = source.IndexOf(')', start);

        var edits = FormatRange(source, start, end);
        var result = Apply(source, edits);

        Assert.That(result, Is.EqualTo("10  | add( 1, 2)  |  multiply( 3,4 )"));
    }

    [Test]
    public void FormatRange_CompletePipelineStages_FormatsOnlyThoseStages()
    {
        const string source = "10  |  add( 1,2 )  | multiply( 3,4 )  |  lower";
        var start = source.IndexOf('|');
        var end = source.LastIndexOf('|');

        var result = Apply(source, FormatRange(source, start, end));

        Assert.That(result, Is.EqualTo("10  | add(1, 2) | multiply(3, 4) |  lower"));
    }

    [TestCase("foo( 1,2 )", "foo(1, 2)")]
    [TestCase("{1,2}", "{1, 2}")]
    [TestCase("(\"BE\"=>42)", "(\"BE\" => 42)")]
    [TestCase("I[1,10]", "I[1, 10]")]
    [TestCase("10|#>add( 1,2 )", "10 |#> add(1, 2)")]
    public void FormatRange_CompleteConstruct_UsesCanonicalLayout(string source, string expected)
    {
        var result = Apply(source, FormatRange(source, 0, source.Length));

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void FormatRange_CompleteInputBindingBody_LeavesBindingPrefixUnchanged()
    {
        const string source = "10  | source :> add( 1,2 )";
        var start = source.IndexOf("add", StringComparison.Ordinal);

        var result = Apply(source, FormatRange(source, start, source.Length));

        Assert.That(result, Is.EqualTo("10  | source :> add(1, 2)"));
    }

    [TestCase("lower(\"a | b\")", 8, 11)]
    [TestCase("10 | add( 1,2", 0, 13)]
    public void FormatRange_UnsafeOrPartialSelection_ReturnsNoEdits(string source, int start, int end)
    {
        Assert.That(FormatRange(source, start, end), Is.Empty);
    }

    [TestCase(0, 0)]
    [TestCase(-1, 1)]
    [TestCase(0, 100)]
    [TestCase(5, 2)]
    public void FormatRange_EmptyOrInvalidOffsets_ReturnsNoEdits(int start, int end)
    {
        Assert.That(FormatRange("add(1,2)", start, end), Is.Empty);
    }

    [Test]
    public void FormatRange_NestedMultilineCall_UsesSurroundingTabIndentation()
    {
        const string source = "record(\nfield:=add(\n1,\n2\n)\n)";
        var start = source.IndexOf("add", StringComparison.Ordinal);
        var end = source.LastIndexOf(')');

        var result = Apply(source, FormatRange(source, start, end, insertSpaces: false));

        Assert.That(result, Is.EqualTo("record(\nfield:=add(\n\t\t1,\n\t\t2\n\t)\n)"));
    }

    [Test]
    public void FormatRange_ProtectedText_PreservesLiteralAndCommentContents()
    {
        const string source = "lower( /* a  b */ \"a  |  b\" )";

        var result = Apply(source, FormatRange(source, 0, source.Length));

        Assert.That(result, Is.EqualTo("lower(/* a  b */ \"a  |  b\")"));
    }

    [Test]
    public void FormatRange_FullDocument_PreservesRequestedFinalNewline()
    {
        const string source = "add( 1,2 )\n";

        var result = Apply(source, FormatRange(source, 0, source.Length, insertFinalNewLine: true));

        Assert.That(result, Is.EqualTo("add(1, 2)\n"));
    }

    [Test]
    public void FormatRange_CrlfDocument_PreservesNewlineConventionAndIsIdempotent()
    {
        const string source = "record(\r\nname:=1,\r\nage:=add( 2,3 )\r\n)";
        var start = source.IndexOf("\r\n", StringComparison.Ordinal);
        var end = source.LastIndexOf("\r\n", StringComparison.Ordinal) + 2;

        var once = Apply(source, FormatRange(source, start, end, newLine: "\r\n"));
        var secondEnd = once.LastIndexOf("\r\n", StringComparison.Ordinal) + 2;
        var twice = Apply(once, FormatRange(once, start, secondEnd, newLine: "\r\n"));

        Assert.Multiple(() =>
        {
            Assert.That(once, Is.EqualTo("record(\r\n    name := 1,\r\n    age := add(2, 3)\r\n)"));
            Assert.That(twice, Is.EqualTo(once));
        });
    }

    private IReadOnlyList<SourceTextEdit> FormatRange(
        string source,
        int start,
        int end,
        bool insertSpaces = true,
        string newLine = "\n",
        bool insertFinalNewLine = false)
        => formatter.FormatRange(Open(source), start, end,
            new DocumentFormattingOptions(4, insertSpaces, newLine, insertFinalNewLine));

    private static string Apply(string source, IReadOnlyList<SourceTextEdit> edits)
    {
        foreach (var edit in edits.OrderByDescending(edit => edit.Start))
            source = source.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.NewText);
        return source;
    }

    private static DocumentSnapshot Open(string source)
    {
        var documents = new DocumentStore(new SyntaxService());
        return documents.Open(new Uri("file:///workspace/example.expr"), source, 1);
    }
}
