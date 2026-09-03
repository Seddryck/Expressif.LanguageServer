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

    private IReadOnlyList<SemanticTokenSpan> GetTokens(string text)
    {
        var parsed = syntax.Parse(text);
        Assert.That(parsed.SyntaxTree, Is.Not.Null, string.Join(Environment.NewLine, parsed.Errors));
        return service.GetTokens(parsed.SyntaxTree!, text);
    }

}
