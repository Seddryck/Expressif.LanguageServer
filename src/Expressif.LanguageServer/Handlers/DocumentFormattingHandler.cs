using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Formatting;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Handlers;

public sealed class DocumentFormattingHandler(
    IDocumentStore documents,
    IDocumentFormatter formatter) : DocumentFormattingHandlerBase
{
    public override Task<TextEditContainer?> Handle(
        DocumentFormattingParams request, CancellationToken cancellationToken)
    {
        if (!documents.TryGet(request.TextDocument.Uri.ToUri(), out var document) || document is null)
            return Task.FromResult<TextEditContainer?>(new TextEditContainer());

        cancellationToken.ThrowIfCancellationRequested();
        var newLine = DetectNewLine(document.Text);
        var preserveFinalNewLine = !request.Options.TrimFinalNewlines && EndsWithNewLine(document.Text);
        var options = new DocumentFormattingOptions(
            request.Options.TabSize,
            request.Options.InsertSpaces,
            newLine,
            request.Options.InsertFinalNewline || preserveFinalNewLine);
        var formatted = formatter.Format(document, options);
        if (formatted == document.Text)
            return Task.FromResult<TextEditContainer?>(new TextEditContainer());

        var edit = new TextEdit
        {
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                new Position(0, 0), GetEndPosition(document.Text)),
            NewText = formatted
        };
        return Task.FromResult<TextEditContainer?>(new TextEditContainer(edit));
    }

    protected override DocumentFormattingRegistrationOptions CreateRegistrationOptions(
        DocumentFormattingCapability capability, ClientCapabilities clientCapabilities)
        => GetRegistrationOptions();

    internal static DocumentFormattingRegistrationOptions GetRegistrationOptions() => new()
    {
        DocumentSelector = TextDocumentSelector.ForLanguage("expressif")
    };

    private static string DetectNewLine(string text)
    {
        var newline = text.IndexOf('\n');
        return newline > 0 && text[newline - 1] == '\r' ? "\r\n" : "\n";
    }

    private static bool EndsWithNewLine(string text)
        => text.EndsWith('\n') || text.EndsWith('\r');

    private static Position GetEndPosition(string text)
    {
        var line = 0;
        var lineStart = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
                continue;
            line++;
            lineStart = index + 1;
        }

        return new Position(line, text.Length - lineStart);
    }
}
