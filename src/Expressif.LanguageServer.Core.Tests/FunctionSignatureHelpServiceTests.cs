using Expressif.LanguageServer.Core.Functions;
using Expressif.LanguageServer.Core.SignatureHelp;
using Expressif.LanguageServer.Core.Syntax;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class FunctionSignatureHelpServiceTests
{
    private static readonly IFunctionCatalog Catalog = new TestFunctionCatalog(
    [
        new("foo", [],
        [
            new("first", false, "First value."),
            new("second", false, "Second value."),
            new("third", true, "Third value.")
        ], "Combines values.", "Test"),
        new("inner", [],
        [
            new("left", false, "Left value."),
            new("right", false, "Right value.")
        ], "Inner function.", "Test"),
        new("outer", [],
        [
            new("nested", false, "Nested value."),
            new("other", false, "Other value.")
        ], "Outer function.", "Test"),
        new("concat", [],
        [
            new("value", false, "First value."),
            new("values", true, "Zero or more additional values.", true, 0)
        ], "Concatenates values.", "Test"),
        new("spread", [],
        [
            new("values", false, "One or more values.", true),
            new("final", false, "Final value.")
        ], "Spreads values.", "Test")
    ]);

    private readonly FunctionSignatureHelpService service = new(Catalog);
    private readonly SyntaxService syntax = new();

    [TestCase("foo(|)", 0)]
    [TestCase("foo(1|)", 0)]
    [TestCase("foo(1, |)", 1)]
    [TestCase("foo(1, 2|)", 1)]
    [TestCase("foo(inner(1, 2), |)", 1)]
    [TestCase("foo(\"a,b\", |)", 1)]
    public void GetSignatureHelp_ArgumentPosition_ReturnsActiveParameter(string textWithCursor, int expected)
    {
        var (text, cursor) = RemoveCursor(textWithCursor);

        var result = service.GetSignatureHelp(Parse(text), cursor);

        Assert.That(result?.ActiveParameter, Is.EqualTo(expected));
    }

    [Test]
    public void GetSignatureHelp_NestedCall_SelectsInnermostCall()
    {
        var (text, cursor) = RemoveCursor("outer(inner(1, |), 2)");

        var result = service.GetSignatureHelp(Parse(text), cursor);

        Assert.Multiple(() =>
        {
            Assert.That(result?.Signature, Is.EqualTo("inner(left, right)"));
            Assert.That(result?.ActiveParameter, Is.EqualTo(1));
        });
    }

    [Test]
    public void GetSignatureHelp_AfterNestedCall_SelectsOuterCall()
    {
        var (text, cursor) = RemoveCursor("outer(inner(1, 2), |2)");

        var result = service.GetSignatureHelp(Parse(text), cursor);

        Assert.Multiple(() =>
        {
            Assert.That(result?.Signature, Is.EqualTo("outer(nested, other)"));
            Assert.That(result?.ActiveParameter, Is.EqualTo(1));
        });
    }

    [Test]
    public void GetSignatureHelp_OptionalAndDocumentedParameters_RendersCatalogMetadata()
    {
        var (text, cursor) = RemoveCursor("foo(1, 2, |3)");

        var result = service.GetSignatureHelp(Parse(text), cursor);

        Assert.Multiple(() =>
        {
            Assert.That(result?.Signature, Is.EqualTo("foo(first, second, third?)"));
            Assert.That(result?.Description, Is.EqualTo("Combines values."));
            Assert.That(result?.Parameters[2], Is.EqualTo(new SignatureParameter("third?", "Third value.")));
        });
    }

    [Test]
    public void GetSignatureHelp_ExcessArgument_ClampsToLastParameter()
    {
        var (text, cursor) = RemoveCursor("inner(1, 2, |3)");

        var result = service.GetSignatureHelp(Parse(text), cursor);

        Assert.That(result?.ActiveParameter, Is.EqualTo(1));
    }

    [TestCase("concat(1, |2)")]
    [TestCase("concat(1, 2, |3)")]
    [TestCase("concat(1, 2, 3, |4)")]
    public void GetSignatureHelp_VariadicArgument_KeepsVariadicParameterActive(string textWithCursor)
    {
        var (text, cursor) = RemoveCursor(textWithCursor);

        var result = service.GetSignatureHelp(Parse(text), cursor);

        Assert.Multiple(() =>
        {
            Assert.That(result?.Signature, Is.EqualTo("concat(value, values?...)"));
            Assert.That(result?.ActiveParameter, Is.EqualTo(1));
            Assert.That(result?.Parameters[1], Is.EqualTo(
                new SignatureParameter("values?...", "Zero or more additional values.")));
        });
    }

    [TestCase("spread(|1, 3)", 0)]
    [TestCase("spread(1, |2, 3)", 0)]
    [TestCase("spread(1, 2, |3)", 1)]
    public void GetSignatureHelp_NonFinalVariadicParameter_ReservesTrailingArguments(
        string textWithCursor,
        int expected)
    {
        var (text, cursor) = RemoveCursor(textWithCursor);

        var result = service.GetSignatureHelp(Parse(text), cursor);

        Assert.That(result?.ActiveParameter, Is.EqualTo(expected));
    }

    [TestCase("foo|(1)")]
    [TestCase("foo(1)|")]
    [TestCase("unsupported(|1)")]
    [TestCase("\"foo(1|)\"")]
    public void GetSignatureHelp_OutsideSupportedArgumentList_ReturnsNull(string textWithCursor)
    {
        var (text, cursor) = RemoveCursor(textWithCursor);

        var result = service.GetSignatureHelp(Parse(text), cursor);

        Assert.That(result, Is.Null);
    }

    private Expressif.Syntax.RootExpressionSyntax Parse(string text)
    {
        var result = syntax.Parse(text);
        Assert.That(result.SyntaxTree, Is.Not.Null, string.Join(Environment.NewLine, result.Errors));
        return result.SyntaxTree!;
    }

    private static (string Text, int Cursor) RemoveCursor(string textWithCursor)
    {
        var cursor = textWithCursor.IndexOf('|');
        Assert.That(cursor, Is.GreaterThanOrEqualTo(0));
        return (textWithCursor.Remove(cursor, 1), cursor);
    }

    private sealed class TestFunctionCatalog(IReadOnlyList<FunctionMetadata> functions) : IFunctionCatalog
    {
        public IReadOnlyList<FunctionMetadata> Functions { get; } = functions;
    }
}
