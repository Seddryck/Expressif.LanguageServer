using Expressif.LanguageServer.Core.CodeActions;
using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Diagnostics;
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
        var handler = new CodeActionHandler(documents, actions.Object, new LegacyTupleReferenceService());

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

    [Test]
    public async Task Handle_SelectionThroughEndOfLine_ReturnsQuickFixAsync()
    {
        var documents = new DocumentStore(new SyntaxService());
        var uri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
        documents.Open(uri.ToUri(), "legacy()\r\n", 1);
        var actions = new Mock<IFunctionCodeActionService>();
        actions.Setup(service => service.GetReplacements(
                It.IsAny<Expressif.Syntax.RootExpressionSyntax>(), 0, 10))
            .Returns([new FunctionReplacement("legacy", "modern", 0, 6)]);
        var handler = new CodeActionHandler(documents, actions.Object, new LegacyTupleReferenceService());

        var result = await handler.Handle(new CodeActionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                new Position(0, 0), new Position(1, 0)),
            Context = new CodeActionContext()
        }, CancellationToken.None);

        Assert.That(result!.Single().CodeAction, Is.Not.Null);
        actions.VerifyAll();
    }

    [Test]
    public async Task Handle_AdjacentImplicitBinding_ReturnsShorthandAndExplicitExpressionFixesAsync()
    {
        const string source = "{1, 2, 5} | adjacent(subtract)";
        var documents = new DocumentStore(new SyntaxService());
        var uri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
        documents.Open(uri.ToUri(), source, 1);
        var functionActions = new Mock<IFunctionCodeActionService>();
        functionActions.Setup(service => service.GetReplacements(
                It.IsAny<Expressif.Syntax.RootExpressionSyntax>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns([]);
        var handler = new CodeActionHandler(documents, functionActions.Object,
            new LegacyTupleReferenceService(), new ImplicitBindingMigrationService());

        var result = await handler.Handle(new CodeActionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                new Position(0, 21), new Position(0, 29)),
            Context = new CodeActionContext()
        }, CancellationToken.None);

        var actions = result!.Select(item => item.CodeAction!).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(actions.Select(action => action.Title), Is.EqualTo(new[]
            {
                "Use explicit tuple binding '~subtract'",
                "Rewrite 'subtract' with explicit tuple arguments"
            }));
            Assert.That(actions.Select(action => action.Edit!.Changes![uri].Single().NewText),
                Is.EqualTo(new[] { "~subtract", "$1 | subtract($0)" }));
            Assert.That(actions.Select(action => action.Edit!.Changes![uri].Single().Range),
                Is.All.EqualTo(new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(0, 21, 0, 29)));
            Assert.That(actions[0].IsPreferred, Is.True);
            Assert.That(actions[1].IsPreferred, Is.False);
            Assert.That(actions.All(action => action.Diagnostics!.Single().Code?.String ==
                                              "implicit-tuple-binding"), Is.True);
        });
    }

    [Test]
    public async Task Handle_UnprovenImplicitBinding_ReturnsNoUnsafeFixAsync()
    {
        const string source = "adjacent(subtract)";
        var documents = new DocumentStore(new SyntaxService());
        var uri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
        documents.Open(uri.ToUri(), source, 1);
        var functionActions = new Mock<IFunctionCodeActionService>();
        functionActions.Setup(service => service.GetReplacements(
                It.IsAny<Expressif.Syntax.RootExpressionSyntax>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns([]);
        var handler = new CodeActionHandler(documents, functionActions.Object,
            new LegacyTupleReferenceService(), new ImplicitBindingMigrationService());

        var result = await handler.Handle(new CodeActionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                new Position(0, 9), new Position(0, 17)),
            Context = new CodeActionContext()
        }, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }
}
