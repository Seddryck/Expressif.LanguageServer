using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Formatting;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Handlers;

public sealed class DocumentRangeFormattingHandler(
    IDocumentStore documents,
    IDocumentFormatter formatter) : DocumentRangeFormattingHandlerBase
{
    public override Task<TextEditContainer> Handle(
        DocumentRangeFormattingParams request, CancellationToken cancellationToken)
    {
        if (!documents.TryGet(request.TextDocument.Uri.ToUri(), out var document) || document is null)
            return Task.FromResult(new TextEditContainer());

        cancellationToken.ThrowIfCancellationRequested();
        if (!DocumentPositions.TryGetOffset(document.Text, request.Range.Start, out var start) ||
            !DocumentPositions.TryGetOffset(document.Text, request.Range.End, out var end) ||
            start >= end)
            return Task.FromResult(new TextEditContainer());

        var options = new DocumentFormattingOptions(
            request.Options.TabSize,
            request.Options.InsertSpaces,
            DocumentFormattingHandler.DetectNewLine(document.Text),
            request.Options.InsertFinalNewline ||
            !request.Options.TrimFinalNewlines && DocumentFormattingHandler.EndsWithNewLine(document.Text));
        var edits = formatter.FormatRange(document, start, end, options)
            .Select(edit => new TextEdit
            {
                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                    DocumentPositions.GetPosition(document.Text, edit.Start),
                    DocumentPositions.GetPosition(document.Text, edit.Start + edit.Length)),
                NewText = edit.NewText
            })
            .ToArray();
        return Task.FromResult(new TextEditContainer(edits));
    }

    protected override DocumentRangeFormattingRegistrationOptions CreateRegistrationOptions(
        DocumentRangeFormattingCapability capability, ClientCapabilities clientCapabilities)
        => GetRegistrationOptions();

    internal static DocumentRangeFormattingRegistrationOptions GetRegistrationOptions() => new()
    {
        DocumentSelector = TextDocumentSelector.ForLanguage("expressif")
    };
}
