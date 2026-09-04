using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Hover;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Handlers;

public sealed class HoverHandler(IDocumentStore documents, IFunctionHoverService hovers) : HoverHandlerBase
{
    public override Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        if (!documents.TryGet(request.TextDocument.Uri.ToUri(), out var document) || document?.SyntaxTree is null ||
            !TryGetOffset(document.Text, request.Position, out var offset))
            return Task.FromResult<Hover?>(null);

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
                GetPosition(document.Text, hover.IdentifierStart),
                GetPosition(document.Text, hover.IdentifierStart + hover.IdentifierLength))
        });
    }

    protected override HoverRegistrationOptions CreateRegistrationOptions(
        HoverCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("expressif")
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
