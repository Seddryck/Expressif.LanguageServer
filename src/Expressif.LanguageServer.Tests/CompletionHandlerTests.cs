using Expressif.LanguageServer.Core.Completion;
using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Syntax;
using Expressif.LanguageServer.Handlers;
using Moq;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Tests;

[TestFixture]
public sealed class CompletionHandlerTests
{
    [Test]
    public async Task Handle_OpenDocument_ReturnsFunctionCompletionItemsAsync()
    {
        var syntax = new Mock<ISyntaxService>();
        syntax.Setup(service => service.Parse(It.IsAny<string>()))
            .Returns(new SyntaxParseResult(null, []));
        var documents = new DocumentStore(syntax.Object);
        var uri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
        documents.Open(uri.ToUri(), "@foo | text-to-", 1);

        var completions = new Mock<ICompletionService>();
        completions.Setup(service => service.GetCompletions("@foo | text-to-", 15))
            .Returns([new CompletionSuggestion("text-to-upper", "text-to-upper", false, 7, 8)]);
        var handler = new CompletionHandler(documents, completions.Object);

        var result = await handler.Handle(new CompletionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position(0, 15)
        }, CancellationToken.None);

        var item = result.Single();
        var edit = item.TextEdit?.TextEdit;
        Assert.Multiple(() =>
        {
            Assert.That(item.Label, Is.EqualTo("text-to-upper"));
            Assert.That(item.Kind, Is.EqualTo(CompletionItemKind.Function));
            Assert.That(edit, Is.Not.Null);
            Assert.That(edit!.NewText, Is.EqualTo("text-to-upper"));
            Assert.That(edit.Range.Start, Is.EqualTo(new Position(0, 7)));
            Assert.That(edit.Range.End, Is.EqualTo(new Position(0, 15)));
        });
    }

    [Test]
    public async Task Handle_DeprecatedSuggestion_UsesStandardDeprecationTagAndDocumentationAsync()
    {
        var syntax = new Mock<ISyntaxService>();
        syntax.Setup(service => service.Parse(It.IsAny<string>()))
            .Returns(new SyntaxParseResult(null, []));
        var documents = new DocumentStore(syntax.Object);
        var uri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
        documents.Open(uri.ToUri(), "app", 1);
        var completions = new Mock<ICompletionService>();
        completions.Setup(service => service.GetCompletions("app", 3))
            .Returns([new CompletionSuggestion(
                "append", "append", true, 0, 3, "Appends text.", true, "suffix", "3.0")]);
        var handler = new CompletionHandler(documents, completions.Object);

        var item = (await handler.Handle(new CompletionParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position(0, 3)
        }, CancellationToken.None)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(item.Deprecated, Is.True);
            Assert.That(item.Tags, Does.Contain(CompletionItemTag.Deprecated));
            Assert.That(item.Detail, Is.EqualTo("Deprecated · use suffix · sunsets in 3.0"));
            Assert.That(item.Documentation?.MarkupContent?.Value,
                Is.EqualTo("Appends text.\n\n**Deprecated.** Use `suffix` instead. Sunset: Expressif 3.0."));
        });
    }
}
