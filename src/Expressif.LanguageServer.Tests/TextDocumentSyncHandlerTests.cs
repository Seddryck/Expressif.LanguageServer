using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Diagnostics;
using Expressif.LanguageServer.Core.Syntax;
using Expressif.LanguageServer.Handlers;
using Expressif.Syntax;
using Moq;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Expressif.LanguageServer.Tests;

[TestFixture]
public sealed class TextDocumentSyncHandlerTests
{
    private static readonly DocumentUri DocumentUri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
    private Mock<ITextDocumentLanguageServer> textDocument = null!;
    private Mock<IFunctionLifecycleDiagnosticService> lifecycleDiagnostics = null!;
    private TextDocumentSyncHandler handler = null!;

    [SetUp]
    public void SetUp()
    {
        var syntax = new Mock<ISyntaxService>();
        syntax.Setup(service => service.Parse(It.IsAny<string>()))
            .Returns((string text) => text.EndsWith('(')
                ? new SyntaxParseResult(null,
                    [new SyntaxError(")", new SourceSpan(System.Text.Encoding.UTF8.GetByteCount(text), 0), "", true)])
                : new SyntaxParseResult(null, []));

        textDocument = new();
        var server = new Mock<ILanguageServerFacade>();
        server.SetupGet(facade => facade.TextDocument).Returns(textDocument.Object);
        lifecycleDiagnostics = new();
        handler = new(new DocumentStore(syntax.Object), lifecycleDiagnostics.Object, server.Object);
    }

    [Test]
    public async Task Open_InvalidDocument_PublishesParserDiagnosticAsync()
    {
        await handler.Handle(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = DocumentUri,
                LanguageId = "expressif",
                Version = 1,
                Text = "@foo | add("
            }
        }, CancellationToken.None);

        var publication = PublishedDiagnostics().Single();
        Assert.Multiple(() =>
        {
            Assert.That(publication.Uri, Is.EqualTo(DocumentUri));
            Assert.That(publication.Version, Is.EqualTo(1));
            Assert.That(publication.Diagnostics.ToArray(), Has.Length.EqualTo(1));
            Assert.That(publication.Diagnostics.Single().Message, Is.EqualTo("Missing )."));
        });
    }

    [Test]
    public async Task Change_ToValidLatestText_ClearsPreviousDiagnosticsAsync()
    {
        await OpenInvalidDocumentAsync();

        await handler.Handle(new DidChangeTextDocumentParams
        {
            TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = DocumentUri, Version = 2 },
            ContentChanges = new Container<TextDocumentContentChangeEvent>(
                new TextDocumentContentChangeEvent { Text = "@foo | add()" })
        }, CancellationToken.None);

        var publications = PublishedDiagnostics().ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(publications, Has.Length.EqualTo(2));
            Assert.That(publications[1].Version, Is.EqualTo(2));
            Assert.That(publications[1].Diagnostics, Is.Empty);
        });
    }

    [Test]
    public async Task Close_ClearsPublishedDiagnosticsAsync()
    {
        await OpenInvalidDocumentAsync();

        await handler.Handle(new DidCloseTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = DocumentUri }
        }, CancellationToken.None);

        var publication = PublishedDiagnostics().Last();
        Assert.Multiple(() =>
        {
            Assert.That(publication.Uri, Is.EqualTo(DocumentUri));
            Assert.That(publication.Diagnostics, Is.Empty);
        });
    }

    [Test]
    public async Task Open_DeprecatedFunction_PublishesHintWithDeprecatedTagAsync()
    {
        var syntaxTree = Expressif.Syntax.ExpressifSyntax.Parse("append()");
        var syntax = new Mock<ISyntaxService>();
        syntax.Setup(service => service.Parse("append()"))
            .Returns(new SyntaxParseResult(syntaxTree, []));
        var documents = new DocumentStore(syntax.Object);
        lifecycleDiagnostics.Setup(service => service.GetDiagnostics(syntaxTree))
            .Returns([new FunctionLifecycleDiagnostic(
                "append", "Function 'append' is deprecated.", 0, 6)]);
        handler = new(documents, lifecycleDiagnostics.Object,
            Mock.Of<ILanguageServerFacade>(facade => facade.TextDocument == textDocument.Object));

        await handler.Handle(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = DocumentUri,
                LanguageId = "expressif",
                Version = 1,
                Text = "append()"
            }
        }, CancellationToken.None);

        var diagnostic = PublishedDiagnostics().Single().Diagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Hint));
            Assert.That(diagnostic.Tags, Does.Contain(DiagnosticTag.Deprecated));
            Assert.That(diagnostic.Range.Start, Is.EqualTo(new Position(0, 0)));
            Assert.That(diagnostic.Range.End, Is.EqualTo(new Position(0, 6)));
        });
    }

    private Task OpenInvalidDocumentAsync() => handler.Handle(new DidOpenTextDocumentParams
    {
        TextDocument = new TextDocumentItem
        {
            Uri = DocumentUri,
            LanguageId = "expressif",
            Version = 1,
            Text = "@foo | add("
        }
    }, CancellationToken.None);

    private IEnumerable<PublishDiagnosticsParams> PublishedDiagnostics()
        => textDocument.Invocations
            .SelectMany(invocation => invocation.Arguments)
            .OfType<PublishDiagnosticsParams>();
}
