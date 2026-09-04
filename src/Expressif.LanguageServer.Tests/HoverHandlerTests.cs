using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Hover;
using Expressif.LanguageServer.Core.Syntax;
using Expressif.LanguageServer.Handlers;
using Moq;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Tests;

[TestFixture]
public sealed class HoverHandlerTests
{
    [Test]
    public async Task Handle_OpenDocument_ReturnsMarkdownAndIdentifierRangeAsync()
    {
        var documents = new DocumentStore(new SyntaxService());
        var uri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
        const string text = ".name | upper";
        documents.Open(uri.ToUri(), text, 1);

        var hovers = new Mock<IFunctionHoverService>();
        hovers.Setup(service => service.GetHover(It.IsAny<Expressif.Syntax.RootExpressionSyntax>(), 10))
            .Returns(new FunctionHover("upper()", "Uppercase text.", 8, 5));
        var handler = new HoverHandler(documents, hovers.Object);

        var result = await handler.Handle(new HoverParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position(0, 10)
        }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Contents.MarkupContent?.Kind, Is.EqualTo(MarkupKind.Markdown));
            Assert.That(result.Contents.MarkupContent?.Value, Is.EqualTo(
                "```expressif\nupper()\n```\n\nUppercase text."));
            Assert.That(result.Range?.Start, Is.EqualTo(new Position(0, 8)));
            Assert.That(result.Range?.End, Is.EqualTo(new Position(0, 13)));
        });
    }

    [Test]
    public async Task Handle_ClosedDocument_ReturnsNoHoverAsync()
    {
        var documents = new DocumentStore(new SyntaxService());
        var handler = new HoverHandler(documents, Mock.Of<IFunctionHoverService>());

        var result = await handler.Handle(new HoverParams
        {
            TextDocument = new TextDocumentIdentifier
            {
                Uri = DocumentUri.FromFileSystemPath("/workspace/closed.expr")
            },
            Position = new Position(0, 0)
        }, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Handle_DeprecatedFunction_AppendsLifecycleNoticeAsync()
    {
        var documents = new DocumentStore(new SyntaxService());
        var uri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
        documents.Open(uri.ToUri(), "append()", 1);
        var hovers = new Mock<IFunctionHoverService>();
        hovers.Setup(service => service.GetHover(It.IsAny<Expressif.Syntax.RootExpressionSyntax>(), 2))
            .Returns(new FunctionHover(
                "append(text)", "Appends text.", 0, 6,
                "Deprecated. Use suffix instead.\nSunset: Expressif 3.0."));
        var handler = new HoverHandler(documents, hovers.Object);

        var result = await handler.Handle(new HoverParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position(0, 2)
        }, CancellationToken.None);

        Assert.That(result?.Contents.MarkupContent?.Value, Is.EqualTo(
            "```expressif\nappend(text)\n```\n\nAppends text.\n\n" +
            "Deprecated. Use suffix instead.\nSunset: Expressif 3.0."));
    }
}
