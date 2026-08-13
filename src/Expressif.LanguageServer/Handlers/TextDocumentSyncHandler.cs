using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Diagnostics;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace Expressif.LanguageServer.Handlers;

public sealed class TextDocumentSyncHandler(IDocumentStore documents, ILanguageServerFacade server) : TextDocumentSyncHandlerBase
{
    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new(uri, "expressif");

    public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
    {
        var document = documents.Open(
            request.TextDocument.Uri.ToUri(), request.TextDocument.Text, request.TextDocument.Version);
        PublishDiagnostics(request.TextDocument.Uri, document);
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
    {
        var text = request.ContentChanges.LastOrDefault()?.Text;
        if (text is not null)
        {
            var document = documents.Change(
                request.TextDocument.Uri.ToUri(), text, request.TextDocument.Version);
            PublishDiagnostics(request.TextDocument.Uri, document);
        }
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
    {
        documents.Close(request.TextDocument.Uri.ToUri());
        server.TextDocument.PublishDiagnostics(new()
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = []
        });
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken) => Unit.Task;

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(
        TextSynchronizationCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("expressif"),
            Change = TextDocumentSyncKind.Full,
            Save = false
        };

    private void PublishDiagnostics(DocumentUri uri, DocumentSnapshot document)
    {
        server.TextDocument.PublishDiagnostics(new()
        {
            Uri = uri,
            Version = document.Version,
            Diagnostics = document.SyntaxErrors
                .Select(error => SyntaxDiagnosticMapper.Map(document.Text, error))
                .ToArray()
        });
    }
}
