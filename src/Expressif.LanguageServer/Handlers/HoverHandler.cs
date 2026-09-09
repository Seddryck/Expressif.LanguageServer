using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Hover;
using Expressif.LanguageServer.Core.Scopes;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Handlers;

public sealed class HoverHandler(IDocumentStore documents, IFunctionHoverService hovers, IFieldScopeService scopes) : HoverHandlerBase
{
    public override Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!documents.TryGet(request.TextDocument.Uri.ToUri(), out var document) || document?.SyntaxTree is null ||
            !DocumentPositions.TryGetOffset(document.Text, request.Position, out var offset))
            return Task.FromResult<Hover?>(null);

        if (scopes.GetScope(document, offset) is { } scope)
            return Task.FromResult<Hover?>(new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.PlainText,
                    Value = scope.Description
                }),
                Range = new(DocumentPositions.GetPosition(document.Text, scope.Reference.Start),
                    DocumentPositions.GetPosition(document.Text, scope.Reference.End))
            });

        var hover = hovers.GetHover(document.SyntaxTree, offset);
        if (hover is null)
            return Task.FromResult<Hover?>(null);

        var contents = $"```expressif\n{hover.Signature}\n```";
        if (!string.IsNullOrWhiteSpace(hover.Description))
            contents += $"\n\n{hover.Description}";
        if (!string.IsNullOrWhiteSpace(hover.LifecycleNotice))
            contents += $"\n\n{hover.LifecycleNotice}";

        return Task.FromResult<Hover?>(new Hover
        {
            Contents = new MarkedStringsOrMarkupContent(new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = contents
            }),
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                DocumentPositions.GetPosition(document.Text, hover.IdentifierStart),
                DocumentPositions.GetPosition(document.Text, hover.IdentifierStart + hover.IdentifierLength))
        });
    }

    protected override HoverRegistrationOptions CreateRegistrationOptions(
        HoverCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("expressif")
        };

}
