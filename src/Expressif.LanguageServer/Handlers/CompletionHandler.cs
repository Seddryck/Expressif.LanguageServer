using Expressif.LanguageServer.Core.Completion;
using Expressif.LanguageServer.Core.Documents;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Handlers;

public sealed class CompletionHandler(IDocumentStore documents, ICompletionService completions) : CompletionHandlerBase
{
    public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        => Task.FromResult(request);

    public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
    {
        if (!documents.TryGet(request.TextDocument.Uri.ToUri(), out var document) || document is null ||
            !TryGetOffset(document.Text, request.Position, out var offset))
            return Task.FromResult(new CompletionList());

        var items = completions.GetCompletions(document.Text, offset)
            .Select((suggestion, index) => new CompletionItem
            {
                Label = suggestion.Label,
                Detail = CreateDetail(suggestion),
                Documentation = new StringOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = CreateDocumentation(suggestion)
                }),
                Deprecated = suggestion.Deprecated,
                Tags = suggestion.Deprecated
                    ? new Container<CompletionItemTag>(CompletionItemTag.Deprecated)
                    : null,
                TextEdit = new TextEdit
                {
                    NewText = suggestion.InsertText,
                    Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                        GetPosition(document.Text, suggestion.ReplacementStart),
                        GetPosition(document.Text, suggestion.ReplacementStart + suggestion.ReplacementLength))
                },
                Kind = CompletionItemKind.Function,
                SortText = $"{(suggestion.Deprecated ? 1 : 0)}-{(suggestion.IsCanonical ? 0 : 1)}-{index:D5}"
            });
        return Task.FromResult(new CompletionList(items));
    }

    protected override CompletionRegistrationOptions CreateRegistrationOptions(
        CompletionCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("expressif"),
            ResolveProvider = false,
            TriggerCharacters = new Container<string>("|", "-")
        };

    private static string CreateDetail(CompletionSuggestion suggestion)
    {
        if (!suggestion.Deprecated)
            return "Expressif function";

        var details = "Deprecated";
        if (!string.IsNullOrWhiteSpace(suggestion.Replacement))
            details += $" · use {suggestion.Replacement}";
        if (!string.IsNullOrWhiteSpace(suggestion.Sunset))
            details += $" · sunsets in {suggestion.Sunset}";
        return details;
    }

    private static string CreateDocumentation(CompletionSuggestion suggestion)
    {
        var documentation = suggestion.Description;
        if (!suggestion.Deprecated)
            return documentation;

        var lifecycle = "**Deprecated.**";
        if (!string.IsNullOrWhiteSpace(suggestion.Replacement))
            lifecycle += $" Use `{suggestion.Replacement}` instead.";
        if (!string.IsNullOrWhiteSpace(suggestion.Sunset))
            lifecycle += $" Sunset: Expressif {suggestion.Sunset}.";
        return string.IsNullOrWhiteSpace(documentation)
            ? lifecycle
            : $"{documentation}\n\n{lifecycle}";
    }

    private static bool TryGetOffset(string text, Position position, out int offset)
    {
        offset = 0;
        for (var line = 0; line < position.Line; line++)
        {
            var newline = text.IndexOf('\n', offset);
            if (newline < 0)
                return false;
            offset = newline + 1;
        }

        var lineEnd = text.IndexOf('\n', offset);
        if (lineEnd < 0)
            lineEnd = text.Length;
        var lineLength = lineEnd - offset;
        if (lineLength > 0 && text[offset + lineLength - 1] == '\r')
            lineLength--;
        if (position.Character > lineLength)
            return false;

        offset += position.Character;
        return true;
    }

    private static Position GetPosition(string text, int offset)
    {
        var line = 0;
        var lineStart = 0;
        for (var index = 0; index < offset; index++)
        {
            if (text[index] != '\n')
                continue;

            line++;
            lineStart = index + 1;
        }

        return new Position(line, offset - lineStart);
    }
}
