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
public sealed class DocumentRangeFormattingHandlerTests
{
    [Test]
    public void RegistrationOptions_TargetExpressifDocuments()
    {
        var options = DocumentRangeFormattingHandler.GetRegistrationOptions();

        Assert.That(options.DocumentSelector!.Single().Language, Is.EqualTo("expressif"));
    }

    [Test]
    public async Task Handle_OpenDocument_TranslatesRangeOptionsAndEditsAsync()
    {
        const string source = "zero\r\nadd( 1,2 )\r\nlast";
        var documents = new DocumentStore(new SyntaxService());
        var uri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
        var document = documents.Open(uri.ToUri(), source, 1);
        DocumentFormattingOptions? receivedOptions = null;
        var formatter = new Mock<IDocumentFormatter>();
        formatter.Setup(service => service.FormatRange(document, 6, 16,
                It.IsAny<DocumentFormattingOptions>()))
            .Callback<DocumentSnapshot, int, int, DocumentFormattingOptions>((_, _, _, options) =>
                receivedOptions = options)
            .Returns([new SourceTextEdit(10, 1, string.Empty), new SourceTextEdit(12, 0, " ")]);
        var handler = new DocumentRangeFormattingHandler(documents, formatter.Object);

        var result = await handler.Handle(new DocumentRangeFormattingParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                new Position(1, 0), new Position(1, 10)),
            Options = new FormattingOptions { TabSize = 2, InsertSpaces = false }
        }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(receivedOptions, Is.EqualTo(new DocumentFormattingOptions(2, false, "\r\n", false)));
            Assert.That(result.Select(edit => edit.Range).ToArray(), Is.EqualTo(new[]
            {
                new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                    new Position(1, 4), new Position(1, 5)),
                new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                    new Position(1, 6), new Position(1, 6))
            }));
            Assert.That(result.Select(edit => edit.NewText).ToArray(), Is.EqualTo(new[] { string.Empty, " " }));
        });
    }

    [Test]
    public async Task Handle_InvalidPosition_ReturnsNoEditsWithoutCallingCoreAsync()
    {
        var documents = new DocumentStore(new SyntaxService());
        var uri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
        documents.Open(uri.ToUri(), "add(1,2)", 1);
        var formatter = new Mock<IDocumentFormatter>();
        var handler = new DocumentRangeFormattingHandler(documents, formatter.Object);

        var result = await handler.Handle(new DocumentRangeFormattingParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                new Position(0, 0), new Position(2, 0)),
            Options = new FormattingOptions { TabSize = 4, InsertSpaces = true }
        }, CancellationToken.None);

        Assert.That(result, Is.Empty);
        formatter.Verify(service => service.FormatRange(
            It.IsAny<DocumentSnapshot>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<DocumentFormattingOptions>()), Times.Never);
    }

    [Test]
    public async Task Handle_ClosedDocument_ReturnsNoEditsAsync()
    {
        var formatter = new Mock<IDocumentFormatter>();
        var handler = new DocumentRangeFormattingHandler(
            new DocumentStore(new SyntaxService()), formatter.Object);

        var result = await handler.Handle(new DocumentRangeFormattingParams
        {
            TextDocument = new TextDocumentIdentifier
            {
                Uri = DocumentUri.FromFileSystemPath("/workspace/missing.expr")
            },
            Range = new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(
                new Position(0, 0), new Position(0, 1)),
            Options = new FormattingOptions { TabSize = 4, InsertSpaces = true }
        }, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }
}
