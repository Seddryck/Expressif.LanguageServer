using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Folding;
using Expressif.LanguageServer.Core.Syntax;
using Expressif.LanguageServer.Handlers;
using Moq;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Tests;

[TestFixture]
public sealed class FoldingRangeHandlerTests
{
    [Test]
    public void RegistrationOptions_TargetExpressifDocuments()
    {
        var options = FoldingRangeHandler.GetRegistrationOptions();

        Assert.That(options.DocumentSelector!.Single().Language, Is.EqualTo("expressif"));
    }

    [Test]
    public async Task Handle_OpenDocument_MapsCoreRangesToProtocolRangesAsync()
    {
        var documents = new DocumentStore(new SyntaxService());
        var uri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
        var document = documents.Open(uri.ToUri(), "@items\n| sort", 1);
        var folding = new Mock<IDocumentFoldingService>();
        folding.Setup(service => service.GetRanges(document)).Returns(
        [
            new DocumentFoldingRange(
                new FoldingPosition(0, 6),
                new FoldingPosition(1, 6)),
            new DocumentFoldingRange(
                new FoldingPosition(2, 3),
                new FoldingPosition(4, 10),
                FoldingKind.Comment)
        ]);
        var handler = new FoldingRangeHandler(documents, folding.Object);

        var result = await handler.Handle(new FoldingRangeRequestParam
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri }
        }, CancellationToken.None);

        var ranges = result!.ToArray();
        Assert.That(ranges, Has.Length.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(ranges[0].StartLine, Is.EqualTo(0));
            Assert.That(ranges[0].StartCharacter, Is.EqualTo(6));
            Assert.That(ranges[0].EndLine, Is.EqualTo(1));
            Assert.That(ranges[0].EndCharacter, Is.EqualTo(6));
            Assert.That(ranges[0].Kind, Is.EqualTo(FoldingRangeKind.Region));
            Assert.That(ranges[1].Kind, Is.EqualTo(FoldingRangeKind.Comment));
        });
    }

    [Test]
    public async Task Handle_MissingDocument_ReturnsNoRangesAsync()
    {
        var handler = new FoldingRangeHandler(
            new DocumentStore(new SyntaxService()), Mock.Of<IDocumentFoldingService>());

        var result = await handler.Handle(new FoldingRangeRequestParam
        {
            TextDocument = new TextDocumentIdentifier
            {
                Uri = DocumentUri.FromFileSystemPath("/workspace/missing.expr")
            }
        }, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }
}
