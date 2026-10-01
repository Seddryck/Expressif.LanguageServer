using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Folding;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Handlers;

public sealed class FoldingRangeHandler(
    IDocumentStore documents,
    IDocumentFoldingService folding) : FoldingRangeHandlerBase
{
    public override Task<Container<FoldingRange>?> Handle(
        FoldingRangeRequestParam request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!documents.TryGet(request.TextDocument.Uri.ToUri(), out var document) || document is null)
            return Task.FromResult<Container<FoldingRange>?>(new Container<FoldingRange>());

        var ranges = folding.GetRanges(document).Select(Map).ToArray();
        return Task.FromResult<Container<FoldingRange>?>(new Container<FoldingRange>(ranges));
    }

    protected override FoldingRangeRegistrationOptions CreateRegistrationOptions(
        FoldingRangeCapability capability,
        ClientCapabilities clientCapabilities) => GetRegistrationOptions();

    internal static FoldingRangeRegistrationOptions GetRegistrationOptions() => new()
    {
        DocumentSelector = TextDocumentSelector.ForLanguage("expressif")
    };

    internal static FoldingRange Map(DocumentFoldingRange range) => new()
    {
        StartLine = range.Start.Line,
        StartCharacter = range.Start.Character,
        EndLine = range.End.Line,
        EndCharacter = range.End.Character,
        Kind = range.Kind == FoldingKind.Comment ? FoldingRangeKind.Comment : FoldingRangeKind.Region
    };
}
