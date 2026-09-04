using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.SemanticTokens;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Handlers;

/// <summary>
/// Provides the stable Expressif legend: variable, function, property, string, number, operator, comment.
/// </summary>
public sealed class SemanticTokensHandler(
    IDocumentStore documents,
    ISemanticTokenService semanticTokens) : SemanticTokensHandlerBase
{
    internal static readonly SemanticTokensLegend Legend = new()
    {
        TokenTypes = new Container<SemanticTokenType>(
            SemanticTokenType.Variable,
            SemanticTokenType.Function,
            SemanticTokenType.Property,
            SemanticTokenType.String,
            SemanticTokenType.Number,
            SemanticTokenType.Operator,
            SemanticTokenType.Comment),
        TokenModifiers = new Container<SemanticTokenModifier>()
    };

    protected override Task Tokenize(SemanticTokensBuilder builder,
        ITextDocumentIdentifierParams identifier, CancellationToken cancellationToken)
    {
        if (!documents.TryGet(identifier.TextDocument.Uri.ToUri(), out var document) ||
            document?.SyntaxDocument is null)
            return Task.CompletedTask;

        var tokens = semanticTokens.GetTokens(document.SyntaxDocument, document.Text);
        foreach (var segment in MapToSingleLineSegments(document.Text, tokens))
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.Push(segment.Line, segment.Character, segment.Length, MapKind(segment.Kind),
                Array.Empty<SemanticTokenModifier>());
        }

        return Task.CompletedTask;
    }

    protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(
        ITextDocumentIdentifierParams @params, CancellationToken cancellationToken)
        => Task.FromResult(new SemanticTokensDocument(Legend));

    protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(
        SemanticTokensCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("expressif"),
            Legend = Legend,
            Full = new SemanticTokensCapabilityRequestFull { Delta = false },
            Range = false
        };

    internal static IReadOnlyList<SemanticTokenSegment> MapToSingleLineSegments(
        string text, IReadOnlyList<SemanticTokenSpan> tokens)
    {
        var result = new List<SemanticTokenSegment>();
        var line = 0;
        var lineStart = 0;

        foreach (var token in tokens)
        {
            while (lineStart < token.Start)
            {
                var newline = text.IndexOf('\n', lineStart);
                if (newline < 0 || newline >= token.Start)
                    break;
                line++;
                lineStart = newline + 1;
            }

            var position = token.Start;
            var end = token.Start + token.Length;
            var tokenLine = line;
            var tokenLineStart = lineStart;
            while (position < end)
            {
                var newline = text.IndexOf('\n', position, end - position);
                var segmentEnd = newline < 0 ? end : newline;
                if (segmentEnd > position && text[segmentEnd - 1] == '\r')
                    segmentEnd--;
                if (segmentEnd > position)
                    result.Add(new(tokenLine, position - tokenLineStart, segmentEnd - position, token.Kind));

                if (newline < 0)
                    break;
                position = newline + 1;
                tokenLine++;
                tokenLineStart = position;
            }

            line = tokenLine;
            lineStart = tokenLineStart;
        }

        return result;
    }

    private static SemanticTokenType MapKind(SemanticTokenKind kind) => kind switch
    {
        SemanticTokenKind.Variable => SemanticTokenType.Variable,
        SemanticTokenKind.Function => SemanticTokenType.Function,
        SemanticTokenKind.Property => SemanticTokenType.Property,
        SemanticTokenKind.String => SemanticTokenType.String,
        SemanticTokenKind.Number => SemanticTokenType.Number,
        SemanticTokenKind.Operator => SemanticTokenType.Operator,
        SemanticTokenKind.Comment => SemanticTokenType.Comment,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}

internal sealed record SemanticTokenSegment(
    int Line, int Character, int Length, SemanticTokenKind Kind);
