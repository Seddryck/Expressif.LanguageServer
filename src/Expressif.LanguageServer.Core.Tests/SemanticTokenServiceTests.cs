using Expressif.LanguageServer.Core.SemanticTokens;
using Expressif.LanguageServer.Core.Syntax;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class SemanticTokenServiceTests
{
    private readonly SyntaxService syntax = new();
    private readonly SemanticTokenService service = new();

    [Test]
    public void GetTokens_MixedExpression_ClassifiesSyntaxConstructsAndExactRanges()
    {
        const string text = "@customer | .name | text-to-upper";

        var tokens = GetTokens(text);

        Assert.That(tokens.Select(token => (text.Substring(token.Start, token.Length), token.Kind)),
            Is.EqualTo(new[]
            {
                ("@customer", SemanticTokenKind.Variable),
                ("|", SemanticTokenKind.Operator),
                ("name", SemanticTokenKind.Property),
                ("|", SemanticTokenKind.Operator),
                ("text-to-upper", SemanticTokenKind.Function)
            }));
    }

    [Test]
    public void GetTokens_NestedCallsReferencesAndLiterals_AreDeterministicAndNonOverlapping()
    {
        const string text = "add($1, multiply(@factor, 2), \"text\")";

        var tokens = GetTokens(text);

        Assert.Multiple(() =>
        {
            Assert.That(tokens.Select(token => (text.Substring(token.Start, token.Length), token.Kind)),
                Is.EqualTo(new[]
                {
                    ("add", SemanticTokenKind.Function),
                    ("$1", SemanticTokenKind.Variable),
                    ("multiply", SemanticTokenKind.Function),
                    ("@factor", SemanticTokenKind.Variable),
                    ("2", SemanticTokenKind.Number),
                    ("\"text\"", SemanticTokenKind.String)
                }));
            Assert.That(tokens.Zip(tokens.Skip(1), (left, right) => left.Start + left.Length <= right.Start),
                Is.All.True);
        });
    }

    [Test]
    public void GetTokens_StringContainingLanguageText_DoesNotClassifyItsContents()
    {
        const string text = "\"@foo | add\"";

        var token = GetTokens(text).Single();

        Assert.That((text.Substring(token.Start, token.Length), token.Kind),
            Is.EqualTo((text, SemanticTokenKind.String)));
    }

    [Test]
    public void GetTokens_MultilinePipeline_UsesLatestSourceOffsets()
    {
        const string text = "@customer\n  |> .address.city\n  | add(12, $1)";

        var tokens = GetTokens(text);

        Assert.That(tokens.Select(token => (text.Substring(token.Start, token.Length), token.Kind)),
            Is.EqualTo(new[]
            {
                ("@customer", SemanticTokenKind.Variable),
                ("|>", SemanticTokenKind.Operator),
                ("address", SemanticTokenKind.Property),
                ("city", SemanticTokenKind.Property),
                ("|", SemanticTokenKind.Operator),
                ("add", SemanticTokenKind.Function),
                ("12", SemanticTokenKind.Number),
                ("$1", SemanticTokenKind.Variable)
            }));
    }

    [Test]
    public void GetTokens_LineAndBlockComments_ClassifiesExactRanges()
    {
        const string text = "// explain the source\n@customer /* before projection */ | .name";

        var tokens = GetTokens(text);

        Assert.That(tokens.Select(token => (text.Substring(token.Start, token.Length), token.Kind)),
            Is.EqualTo(new[]
            {
                ("// explain the source", SemanticTokenKind.Comment),
                ("@customer", SemanticTokenKind.Variable),
                ("/* before projection */", SemanticTokenKind.Comment),
                ("|", SemanticTokenKind.Operator),
                ("name", SemanticTokenKind.Property)
            }));
    }

    [Test]
    public void GetTokens_MultilineBlockComment_ReturnsSingleSourceSpan()
    {
        const string text = "@customer /* first line\r\nsecond line */ | .name";

        var comment = GetTokens(text).Single(token => token.Kind == SemanticTokenKind.Comment);

        Assert.That(text.Substring(comment.Start, comment.Length),
            Is.EqualTo("/* first line\r\nsecond line */"));
    }

    [Test]
    public void GetTokens_NamedInputBinding_ClassifiesDeclarationOperatorAndReference()
    {
        const string text = "10 | input :> @input | upper";

        var tokens = GetTokens(text);

        Assert.That(tokens.Select(token => (text.Substring(token.Start, token.Length), token.Kind)),
            Is.EqualTo(new[]
            {
                ("10", SemanticTokenKind.Number),
                ("|", SemanticTokenKind.Operator),
                ("input", SemanticTokenKind.Variable),
                (":>", SemanticTokenKind.Operator),
                ("@input", SemanticTokenKind.Variable),
                ("|", SemanticTokenKind.Operator),
                ("upper", SemanticTokenKind.Function)
            }));
    }

    [Test]
    public void GetTokens_PositionalInputBinding_ClassifiesEveryDeclaration()
    {
        const string text = "T(10, 20) | (left, right) :> @left | add(@right)";

        var tokens = GetTokens(text);

        Assert.Multiple(() =>
        {
            Assert.That(tokens.Where(token => token.Kind == SemanticTokenKind.Variable)
                .Select(token => text.Substring(token.Start, token.Length)),
                Is.EqualTo(new[] { "left", "right", "@left", "@right" }));
            Assert.That(tokens.Where(token => token.Kind == SemanticTokenKind.Operator)
                .Select(token => text.Substring(token.Start, token.Length)),
                Does.Contain(":>"));
        });
    }

    [Test]
    public void GetTokens_MultilineAnonymousInputBinding_ClassifiesExactOperatorRange()
    {
        const string text = "10\n| :>\n.first";

        var bindingOperator = GetTokens(text)
            .Single(token => text.Substring(token.Start, token.Length) == ":>");

        Assert.That(bindingOperator,
            Is.EqualTo(new SemanticTokenSpan(text.IndexOf(":>", StringComparison.Ordinal), 2,
                SemanticTokenKind.Operator)));
    }

    [TestCase("~subtract", "~", "subtract")]
    [TestCase("subtract~", "~", "subtract")]
    [TestCase("~greater-than", "~", "greater-than")]
    public void GetTokens_TupleBindingShorthand_ClassifiesTildeAndCallable(
        string text, string expectedOperator, string expectedFunction)
    {
        var tokens = GetTokens(text);

        Assert.That(tokens.Select(token => (text.Substring(token.Start, token.Length), token.Kind)),
            Is.EqualTo(new[]
            {
                (text.StartsWith('~') ? expectedOperator : expectedFunction,
                    text.StartsWith('~') ? SemanticTokenKind.Operator : SemanticTokenKind.Function),
                (text.StartsWith('~') ? expectedFunction : expectedOperator,
                    text.StartsWith('~') ? SemanticTokenKind.Function : SemanticTokenKind.Operator)
            }));
    }

    [Test]
    public void GetTokens_QuotedTilde_RemainsAString()
    {
        const string text = "\"~subtract and greater-than~\"";

        Assert.That(GetTokens(text), Is.EqualTo(new[]
        {
            new SemanticTokenSpan(0, text.Length, SemanticTokenKind.String)
        }));
    }

    private IReadOnlyList<SemanticTokenSpan> GetTokens(string text)
    {
        var parsed = syntax.Parse(text);
        Assert.That(parsed.SyntaxDocument, Is.Not.Null, string.Join(Environment.NewLine, parsed.Errors));
        return service.GetTokens(parsed.SyntaxDocument!, text);
    }

}
