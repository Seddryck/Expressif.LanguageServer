using Expressif.LanguageServer.Core.CodeActions;
using Expressif.LanguageServer.Core.Documents;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Handlers;

public sealed class CodeActionHandler(IDocumentStore documents, IFunctionCodeActionService codeActions)
    : CodeActionHandlerBase
{
    public override Task<CodeAction> Handle(CodeAction request, CancellationToken cancellationToken)
        => Task.FromResult(request);

    public override Task<CommandOrCodeActionContainer?> Handle(
        CodeActionParams request, CancellationToken cancellationToken)
    {
        if (!documents.TryGet(request.TextDocument.Uri.ToUri(), out var document) ||
            document?.SyntaxTree is null ||
            !TryGetOffset(document.Text, request.Range.Start, out var start) ||
            !TryGetOffset(document.Text, request.Range.End, out var end))
            return Task.FromResult<CommandOrCodeActionContainer?>(new CommandOrCodeActionContainer());

        var actions = codeActions.GetReplacements(document.SyntaxTree, start, end - start)
            .Select(replacement => new CommandOrCodeAction(new CodeAction
            {
                Title = $"Replace '{replacement.OldName}' with '{replacement.NewName}'",
                Kind = CodeActionKind.QuickFix,
                IsPreferred = true,
                Edit = new WorkspaceEdit
                {
                    Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>>
                    {
                        [request.TextDocument.Uri] =
                        [
                            new TextEdit
                            {
                                NewText = replacement.NewName,
                                Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                                    GetPosition(document.Text, replacement.IdentifierStart),
                                    GetPosition(document.Text,
                                        replacement.IdentifierStart + replacement.IdentifierLength))
                            }
                        ]
                    }
                }
            }))
            .ToArray();
        return Task.FromResult<CommandOrCodeActionContainer?>(new CommandOrCodeActionContainer(actions));
    }

    protected override CodeActionRegistrationOptions CreateRegistrationOptions(
        CodeActionCapability capability, ClientCapabilities clientCapabilities) => new()
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("expressif"),
            CodeActionKinds = new Container<CodeActionKind>(CodeActionKind.QuickFix),
            ResolveProvider = false
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
