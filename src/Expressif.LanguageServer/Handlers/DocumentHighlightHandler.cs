using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Scopes;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Handlers;

public sealed class DocumentHighlightHandler(IDocumentStore documents, IFieldScopeService scopes)
    : DocumentHighlightHandlerBase
{
    public override Task<DocumentHighlightContainer?> Handle(
        DocumentHighlightParams request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!documents.TryGet(request.TextDocument.Uri.ToUri(), out var document) || document is null ||
            !DocumentPositions.TryGetOffset(document.Text, request.Position, out var offset) ||
            scopes.GetScope(document, offset)?.Supplier is not { } supplier)
            return Task.FromResult<DocumentHighlightContainer?>(new DocumentHighlightContainer());

        return Task.FromResult<DocumentHighlightContainer?>(new DocumentHighlightContainer(new DocumentHighlight
        {
            Kind = DocumentHighlightKind.Text,
            Range = new(DocumentPositions.GetPosition(document.Text, supplier.Start),
                DocumentPositions.GetPosition(document.Text, supplier.End))
        }));
    }

    protected override DocumentHighlightRegistrationOptions CreateRegistrationOptions(
        DocumentHighlightCapability capability, ClientCapabilities clientCapabilities) => GetRegistrationOptions();

    internal static DocumentHighlightRegistrationOptions GetRegistrationOptions() => new()
    {
        DocumentSelector = TextDocumentSelector.ForLanguage("expressif")
    };
}
