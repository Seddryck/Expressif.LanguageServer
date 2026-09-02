using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.SignatureHelp;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Handlers;

public sealed class SignatureHelpHandler(
    IDocumentStore documents,
    IFunctionSignatureHelpService signatures) : SignatureHelpHandlerBase
{
    public override Task<SignatureHelp?> Handle(SignatureHelpParams request, CancellationToken cancellationToken)
    {
        if (!documents.TryGet(request.TextDocument.Uri.ToUri(), out var document) || document?.SyntaxTree is null ||
            !TryGetOffset(document.Text, request.Position, out var offset))
            return Task.FromResult<SignatureHelp?>(null);

        var result = signatures.GetSignatureHelp(document.SyntaxTree, offset);
        if (result is null)
            return Task.FromResult<SignatureHelp?>(null);

        var information = new SignatureInformation
        {
            Label = result.Signature,
            Documentation = string.IsNullOrWhiteSpace(result.Description) ? null : result.Description,
            Parameters = new Container<ParameterInformation>(result.Parameters.Select(parameter =>
                new ParameterInformation
                {
                    Label = parameter.Label,
                    Documentation = string.IsNullOrWhiteSpace(parameter.Description) ? null : parameter.Description
                }))
        };

        return Task.FromResult<SignatureHelp?>(new SignatureHelp
        {
            Signatures = new Container<SignatureInformation>(information),
            ActiveSignature = 0,
            ActiveParameter = result.ActiveParameter
        });
    }

    protected override SignatureHelpRegistrationOptions CreateRegistrationOptions(
        SignatureHelpCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("expressif"),
            TriggerCharacters = new Container<string>("(", ",")
        };

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
}
