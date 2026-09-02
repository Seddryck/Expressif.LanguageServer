using Expressif.LanguageServer.Core.SemanticTokens;
using Expressif.LanguageServer.Handlers;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Tests;

[TestFixture]
public sealed class SemanticTokensHandlerTests
{
    [Test]
    public void Legend_UsesStableStandardTokenTypesWithoutModifiers()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SemanticTokensHandler.Legend.TokenTypes,
                Is.EqualTo(new[]
                {
                    SemanticTokenType.Variable,
                    SemanticTokenType.Function,
                    SemanticTokenType.Property,
                    SemanticTokenType.String,
                    SemanticTokenType.Number,
                    SemanticTokenType.Operator
                }));
            Assert.That(SemanticTokensHandler.Legend.TokenModifiers, Is.Empty);
        });
    }

    [Test]
    public void MapToSingleLineSegments_MultilineAndUnicodeText_UsesUtf16Positions()
    {
        const string text = "\ud83d\ude00 @first\r\n  \"second\r\nline\"";
        var tokens = new[]
        {
            new SemanticTokenSpan(3, 6, SemanticTokenKind.Variable),
            new SemanticTokenSpan(13, 14, SemanticTokenKind.String)
        };

        var segments = SemanticTokensHandler.MapToSingleLineSegments(text, tokens);

        Assert.That(segments, Is.EqualTo(new[]
        {
            new SemanticTokenSegment(0, 3, 6, SemanticTokenKind.Variable),
            new SemanticTokenSegment(1, 2, 7, SemanticTokenKind.String),
            new SemanticTokenSegment(2, 0, 5, SemanticTokenKind.String)
        }));
    }
}
