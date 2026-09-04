using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Formatting;
using Expressif.LanguageServer.Core.Syntax;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class DocumentFormatterTests
{
    private readonly DocumentFormatter formatter = new();

    [Test]
    public void Format_CompactExpression_NormalizesSpacing()
    {
        var formatted = Format("10|add( 5,2 )");

        Assert.That(formatted, Is.EqualTo("10 | add(5, 2)"));
    }

    [Test]
    public void Format_MultilinePipeline_PlacesEveryStageOnItsOwnLine()
    {
        var formatted = Format("10  \r\n   |add(5)| multiply( 2 )", newLine: "\r\n");

        Assert.That(formatted, Is.EqualTo("10\r\n| add(5)\r\n| multiply(2)"));
    }

    [Test]
    public void Format_MultilineFunctionCall_IndentsArguments()
    {
        var formatted = Format("record(\nname:=\"Alice\",\nage :=30\n)");

        Assert.That(formatted, Is.EqualTo(
            "record(\n    name := \"Alice\",\n    age := 30\n)"));
    }

    [Test]
    public void Format_WithTabs_UsesTabsForNestedLines()
    {
        var formatted = Format("record(\nname := \"Alice\"\n)", insertSpaces: false);

        Assert.That(formatted, Is.EqualTo("record(\n\tname := \"Alice\"\n)"));
    }

    [Test]
    public void Format_CommentsAndQuotedOperators_PreservesTheirContents()
    {
        const string source = "// leading\nlower(/* inner */ \"a | b\")  | trim // trailing";

        var formatted = Format(source);

        Assert.That(formatted, Is.EqualTo(
            "// leading\nlower(/* inner */ \"a | b\") | trim // trailing"));
    }

    [Test]
    public void Format_InvalidExpression_ReturnsOriginalText()
    {
        const string source = "lower(";

        Assert.That(Format(source), Is.EqualTo(source));
    }

    [Test]
    public void Format_WhenFinalNewLineRequested_AddsOneConfiguredNewLine()
    {
        var formatted = Format("10 | add(5)\n\n", newLine: "\r\n", insertFinalNewLine: true);

        Assert.That(formatted, Is.EqualTo("10 | add(5)\r\n"));
    }

    [Test]
    public void Format_AlreadyFormattedExpression_IsIdempotent()
    {
        const string source = "10\n| add(\n    5,\n    multiply(2, 3)\n)\n";
        var options = new DocumentFormattingOptions(4, true, "\n", true);
        var document = Open(source);

        var once = formatter.Format(document, options);
        var twice = formatter.Format(Open(once), options);

        Assert.That(twice, Is.EqualTo(once));
    }

    [TestCase("foo(name:=1, values:={1,2})")]
    [TestCase("(\"BE\"=>42)")]
    [TestCase("10|AND 20")]
    [TestCase("@items|>absolute")]
    [TestCase("I[1,10]")]
    [TestCase("I]1,10[")]
    [TestCase("{1,...@args,3}")]
    [TestCase("#{(\"BE\"=>{\"Alice\",\"Bob\"})}")]
    public void Format_SupportedSyntax_RemainsValidAndIdempotent(string source)
    {
        var formatted = Format(source);

        Assert.Multiple(() =>
        {
            Assert.That(Open(formatted).SyntaxErrors, Is.Empty);
            Assert.That(Format(formatted), Is.EqualTo(formatted));
        });
    }

    [TestCase("I[1,10]", "I[1, 10]")]
    [TestCase("I]1,10[", "I]1, 10[")]
    [TestCase("I(1,10]", "I(1, 10]")]
    public void Format_IntervalDelimiters_PreserveTheirAuthoredForm(string source, string expected)
    {
        Assert.That(Format(source), Is.EqualTo(expected));
    }

    private string Format(
        string source,
        int tabSize = 4,
        bool insertSpaces = true,
        string newLine = "\n",
        bool insertFinalNewLine = false)
        => formatter.Format(Open(source),
            new DocumentFormattingOptions(tabSize, insertSpaces, newLine, insertFinalNewLine));

    private static DocumentSnapshot Open(string source)
    {
        var documents = new DocumentStore(new SyntaxService());
        return documents.Open(new Uri("file:///workspace/example.expr"), source, 1);
    }
}
