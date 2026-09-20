using Expressif.LanguageServer.Core.Functions;
using Expressif.LanguageServer.Core.Hover;
using Expressif.LanguageServer.Core.Syntax;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class FunctionHoverServiceTests
{
    private static readonly IFunctionCatalog Catalog = new TestFunctionCatalog(
    [
        new("lower", ["text-to-lower"], [], "Lowercase text.", "Text"),
        new("token", [],
        [
            new("index", false, "Token index."),
            new("separator", true, "Token separator.")
        ], "Returns a token.", "Text"),
        new("upper", ["text-to-upper"], [], "Uppercase text.", "Text"),
        new("concat", [],
        [
            new("value", false, "First value."),
            new("values", true, "Additional values.", true, 0)
        ], "Concatenates values.", "Text")
    ]);

    private readonly FunctionHoverService service = new(Catalog);
    private readonly SyntaxService syntax = new();

    [TestCase(0)]
    [TestCase(2)]
    [TestCase(4)]
    public void GetHover_CanonicalFunctionAtIdentifierBoundaries_ReturnsDocumentation(int nameOffset)
    {
        const string text = "upper(.name)";

        var result = service.GetHover(Parse(text), nameOffset);

        Assert.Multiple(() =>
        {
            Assert.That(result?.Signature, Is.EqualTo("upper()"));
            Assert.That(result?.Description, Is.EqualTo("Uppercase text."));
            Assert.That(result?.IdentifierStart, Is.Zero);
            Assert.That(result?.IdentifierLength, Is.EqualTo(5));
        });
    }

    [Test]
    public void GetHover_Alias_ReturnsCanonicalDocumentation()
    {
        const string text = ".name | text-to-upper";
        var cursor = text.IndexOf("to", StringComparison.Ordinal);

        var result = service.GetHover(Parse(text), cursor);

        Assert.Multiple(() =>
        {
            Assert.That(result?.Signature, Is.EqualTo("upper()"));
            Assert.That(result?.Description, Is.EqualTo("Uppercase text."));
            Assert.That(result?.IdentifierStart, Is.EqualTo(8));
            Assert.That(result?.IdentifierLength, Is.EqualTo("text-to-upper".Length));
        });
    }

    [Test]
    public void GetHover_OptionalParameter_UsesConsistentQuestionMarkNotation()
    {
        const string text = "token(0)";

        var result = service.GetHover(Parse(text), 1);

        Assert.That(result?.Signature, Is.EqualTo("token(index, separator?)"));
    }

    [Test]
    public void GetHover_OptionalVariadicParameter_RendersBothModifiers()
    {
        var result = service.GetHover(Parse("concat(1)"), 1);

        Assert.That(result?.Signature, Is.EqualTo("concat(value, values?...)"));
    }

    [Test]
    public void GetHover_NestedFunction_UsesInnermostIdentifierRange()
    {
        const string text = "upper(lower(.name))";
        var cursor = text.IndexOf("lower", StringComparison.Ordinal) + 2;

        var result = service.GetHover(Parse(text), cursor);

        Assert.Multiple(() =>
        {
            Assert.That(result?.Signature, Is.EqualTo("lower()"));
            Assert.That(result?.IdentifierStart, Is.EqualTo(6));
            Assert.That(result?.IdentifierLength, Is.EqualTo(5));
        });
    }

    [Test]
    public void GetHover_FunctionInsideInputBoundBody_ReturnsDocumentation()
    {
        const string text = "10 | input :> @input | upper";
        var cursor = text.IndexOf("upper", StringComparison.Ordinal);

        var result = service.GetHover(Parse(text), cursor);

        Assert.That(result?.Signature, Is.EqualTo("upper()"));
        Assert.That(result?.IdentifierStart, Is.EqualTo(cursor));
    }

    [TestCase("upper() // trailing comment")]
    [TestCase("upper() /* trailing comment */")]
    [TestCase("upper() // middle comment\n| lower()")]
    [TestCase("upper() /* middle comment */ | lower()")]
    public void GetHover_Comment_ReturnsNoHover(string text)
    {
        var cursor = text.IndexOf("comment", StringComparison.Ordinal);

        var result = service.GetHover(Parse(text), cursor);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GetHover_FunctionAfterLeadingComment_ReturnsDocumentation()
    {
        const string text = "// leading comment\nupper()";
        var cursor = text.IndexOf("upper", StringComparison.Ordinal);

        var result = service.GetHover(Parse(text), cursor);

        Assert.That(result?.Signature, Is.EqualTo("upper()"));
    }

    [TestCase("~lower", 2, 1)]
    [TestCase("lower~", 2, 0)]
    [TestCase("~text-to-lower", 5, 1)]
    public void GetHover_TupleBindingShorthand_ResolvesUnderlyingCallable(
        string text, int cursor, int expectedStart)
    {
        var result = service.GetHover(Parse(text), cursor);

        Assert.Multiple(() =>
        {
            Assert.That(result?.Signature, Is.EqualTo("lower()"));
            Assert.That(result?.IdentifierStart, Is.EqualTo(expectedStart));
            Assert.That(result?.IdentifierLength, Is.EqualTo(text.Trim('~').Length));
        });
    }

    [Test]
    public void GetHover_DeprecatedFunction_IncludesReplacementAndSunset()
    {
        var service = new FunctionHoverService(new TestFunctionCatalog(
        [
            new("append", [], [], "Appends text.", "Text", true, "suffix", "3.0")
        ]));

        var result = service.GetHover(Parse("append(\"-x\")"), 2);

        Assert.That(result?.LifecycleNotice,
            Is.EqualTo("Deprecated. Use suffix instead.\nSunset: Expressif 3.0."));
    }

    [Test]
    public void GetHover_DeprecatedFunctionWithoutOptionalMetadata_FallsBackCleanly()
    {
        var service = new FunctionHoverService(new TestFunctionCatalog(
        [
            new("legacy", [], [], "Legacy.", "Text", true)
        ]));

        Assert.That(service.GetHover(Parse("legacy()"), 2)?.LifecycleNotice, Is.EqualTo("Deprecated."));
    }

    [Test]
    public void GetHover_ImplicitBinding_ExplainsUsageSpecificDeprecation()
    {
        const string text = "{1, 2, 5} | adjacent(subtract)";
        var service = new FunctionHoverService(new TestFunctionCatalog(
        [
            new("adjacent", [], [], "Adjacent pairs.", "Array"),
            new("subtract", [], [], "Subtracts a value.", "Numeric")
        ]));

        var result = service.GetHover(Parse(text), text.IndexOf("subtract", StringComparison.Ordinal));

        Assert.That(result?.LifecycleNotice, Is.EqualTo(
            "Deprecated usage. Implicit argument injection into 'subtract' is deprecated; " +
            "use an explicit binding expression."));
    }

    [TestCase("\"upper\"", 2)]
    [TestCase(".name | upper", 6)]
    [TestCase("this-function-does-not-exist(1)", 4)]
    public void GetHover_UnsupportedContext_ReturnsNoHover(string text, int cursor)
    {
        var result = service.GetHover(Parse(text), cursor);

        Assert.That(result, Is.Null);
    }

    private Expressif.Syntax.RootExpressionSyntax Parse(string text)
    {
        var result = syntax.Parse(text);
        Assert.That(result.SyntaxTree, Is.Not.Null, string.Join(Environment.NewLine, result.Errors));
        return result.SyntaxTree!;
    }

    private sealed class TestFunctionCatalog(IReadOnlyList<FunctionMetadata> functions) : IFunctionCatalog
    {
        public IReadOnlyList<FunctionMetadata> Functions { get; } = functions;
    }
}
