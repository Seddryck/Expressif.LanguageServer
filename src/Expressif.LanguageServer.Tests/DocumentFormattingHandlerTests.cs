using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Formatting;
using Expressif.LanguageServer.Core.Syntax;
using Expressif.LanguageServer.Handlers;
using Moq;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Tests;

[TestFixture]
public sealed class DocumentFormattingHandlerTests
{
    [Test]
    public void RegistrationOptions_TargetExpressifDocuments()
    {
        var options = DocumentFormattingHandler.GetRegistrationOptions();

        Assert.That(options.DocumentSelector!.Single().Language, Is.EqualTo("expressif"));
    }

    [Test]
    public async Task Handle_OpenDocument_ReturnsFullDocumentEditAsync()
    {
        const string source = "record(\r\nname:=1\r\n)";
        var documents = new DocumentStore(new SyntaxService());
        var uri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
        documents.Open(uri.ToUri(), source, 1);
        DocumentFormattingOptions? receivedOptions = null;
        var formatter = new Mock<IDocumentFormatter>();
        formatter.Setup(service => service.Format(It.IsAny<DocumentSnapshot>(),
                It.IsAny<DocumentFormattingOptions>()))
            .Callback<DocumentSnapshot, DocumentFormattingOptions>((_, options) => receivedOptions = options)
            .Returns("record(\r\n  name := 1\r\n)");
        var handler = new DocumentFormattingHandler(documents, formatter.Object);

        var result = await handler.Handle(new DocumentFormattingParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Options = new FormattingOptions
            {
                TabSize = 2,
                InsertSpaces = true
            }
        }, CancellationToken.None);

        var edit = result!.Single();
        Assert.Multiple(() =>
        {
            Assert.That(edit.Range.Start, Is.EqualTo(new Position(0, 0)));
            Assert.That(edit.Range.End, Is.EqualTo(new Position(2, 1)));
            Assert.That(edit.NewText, Is.EqualTo("record(\r\n  name := 1\r\n)"));
            Assert.That(receivedOptions, Is.EqualTo(
                new DocumentFormattingOptions(2, true, "\r\n", false)));
        });
    }

    [Test]
    public async Task Handle_UnchangedDocument_ReturnsNoEditsAsync()
    {
        var documents = new DocumentStore(new SyntaxService());
        var uri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
        documents.Open(uri.ToUri(), "lower", 1);
        var formatter = new Mock<IDocumentFormatter>();
        formatter.Setup(service => service.Format(It.IsAny<DocumentSnapshot>(),
                It.IsAny<DocumentFormattingOptions>()))
            .Returns("lower");
        var handler = new DocumentFormattingHandler(documents, formatter.Object);

        var result = await handler.Handle(new DocumentFormattingParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Options = new FormattingOptions { TabSize = 4, InsertSpaces = true }
        }, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task Handle_MissingDocument_ReturnsNoEditsAsync()
    {
        var handler = new DocumentFormattingHandler(
            new DocumentStore(new SyntaxService()), Mock.Of<IDocumentFormatter>());

        var result = await handler.Handle(new DocumentFormattingParams
        {
            TextDocument = new TextDocumentIdentifier
            {
                Uri = DocumentUri.FromFileSystemPath("/workspace/missing.expr")
            },
            Options = new FormattingOptions { TabSize = 4, InsertSpaces = true }
        }, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }
}
