using Expressif.LanguageServer.Core.CodeActions;
using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Syntax;
using Expressif.LanguageServer.Handlers;
using Moq;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Tests;

[TestFixture]
public sealed class CodeActionHandlerTests
{
    [Test]
    public async Task Handle_SafeReplacement_ReturnsQuickFixEditAsync()
    {
        var documents = new DocumentStore(new SyntaxService());
        var uri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
        documents.Open(uri.ToUri(), "legacy()", 1);
        var actions = new Mock<IFunctionCodeActionService>();
        actions.Setup(service => service.GetReplacements(
                It.IsAny<Expressif.Syntax.RootExpressionSyntax>(), 0, 6))
            .Returns([new FunctionReplacement("legacy", "modern", 0, 6)]);
        var handler = new CodeActionHandler(documents, actions.Object);

        var result = await handler.Handle(new CodeActionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                new Position(0, 0), new Position(0, 6)),
            Context = new CodeActionContext()
        }, CancellationToken.None);

        var action = result!.Single().CodeAction!;
        var edit = action.Edit!.Changes![uri].Single();
        Assert.Multiple(() =>
        {
            Assert.That(action.Title, Is.EqualTo("Replace 'legacy' with 'modern'"));
            Assert.That(action.Kind, Is.EqualTo(CodeActionKind.QuickFix));
            Assert.That(edit.NewText, Is.EqualTo("modern"));
            Assert.That(edit.Range.Start, Is.EqualTo(new Position(0, 0)));
            Assert.That(edit.Range.End, Is.EqualTo(new Position(0, 6)));
        });
    }
}
