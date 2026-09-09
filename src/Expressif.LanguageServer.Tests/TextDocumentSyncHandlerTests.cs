using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Diagnostics;
using Expressif.LanguageServer.Core.Functions;
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
    private Mock<IFunctionCallDiagnosticService> functionCallDiagnostics = null!;
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
        functionCallDiagnostics = new();
        functionCallDiagnostics.Setup(service => service.GetDiagnostics(It.IsAny<RootExpressionSyntax>()))
            .Returns([]);
        lifecycleDiagnostics = new();
        lifecycleDiagnostics.Setup(service => service.GetDiagnostics(It.IsAny<RootExpressionSyntax>()))
            .Returns([]);
        handler = new(new DocumentStore(syntax.Object), functionCallDiagnostics.Object,
            lifecycleDiagnostics.Object, server.Object, new LegacyTupleReferenceService());
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
        var syntaxDocument = Expressif.Syntax.ExpressifSyntax.ParseDocument("append()");
        var syntaxTree = syntaxDocument.Expression;
        var syntax = new Mock<ISyntaxService>();
        syntax.Setup(service => service.Parse("append()"))
            .Returns(new SyntaxParseResult(syntaxDocument, []));
        var documents = new DocumentStore(syntax.Object);
        lifecycleDiagnostics.Setup(service => service.GetDiagnostics(syntaxTree))
            .Returns([new FunctionLifecycleDiagnostic(
                "append", "Function 'append' is deprecated.", 0, 6)]);
        handler = new(documents, functionCallDiagnostics.Object, lifecycleDiagnostics.Object,
            Mock.Of<ILanguageServerFacade>(facade => facade.TextDocument == textDocument.Object), new LegacyTupleReferenceService());

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

    [Test]
    public async Task Open_InvalidFunctionCall_PublishesErrorDiagnosticAsync()
    {
        var syntaxDocument = Expressif.Syntax.ExpressifSyntax.ParseDocument("unknown()");
        var syntax = new Mock<ISyntaxService>();
        syntax.Setup(service => service.Parse("unknown()"))
            .Returns(new SyntaxParseResult(syntaxDocument, []));
        var documents = new DocumentStore(syntax.Object);
        functionCallDiagnostics.Setup(service => service.GetDiagnostics(syntaxDocument.Expression))
            .Returns([new FunctionCallDiagnostic("Unknown function 'unknown'.", 0, 7)]);
        handler = new(documents, functionCallDiagnostics.Object, lifecycleDiagnostics.Object,
            Mock.Of<ILanguageServerFacade>(facade => facade.TextDocument == textDocument.Object), new LegacyTupleReferenceService());

        await handler.Handle(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = DocumentUri,
                LanguageId = "expressif",
                Version = 1,
                Text = "unknown()"
            }
        }, CancellationToken.None);

        var diagnostic = PublishedDiagnostics().Single().Diagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Message, Is.EqualTo("Unknown function 'unknown'."));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(diagnostic.Range, Is.EqualTo(
                new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(0, 0, 0, 7)));
        });
    }

    [Test]
    public async Task Open_TooManyArguments_PublishesErrorAndChangeClearsItAsync()
    {
        handler = new(new DocumentStore(new SyntaxService()),
            new FunctionCallDiagnosticService(new ExpressifFunctionCatalog()), lifecycleDiagnostics.Object,
            Mock.Of<ILanguageServerFacade>(facade => facade.TextDocument == textDocument.Object));

        await handler.Handle(new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = DocumentUri,
                LanguageId = "expressif",
                Version = 1,
                Text = "@value |\nadd(1,\n  2, 3)"
            }
        }, CancellationToken.None);

        var diagnostic = PublishedDiagnostics().Single().Diagnostics.Single();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Message, Is.EqualTo(
                "Function 'add' accepts at most 2 arguments, but 3 arguments were provided."));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(diagnostic.Source, Is.EqualTo("expressif"));
            Assert.That(diagnostic.Range, Is.EqualTo(
                new OmniSharp.Extensions.LanguageServer.Protocol.Models.Range(1, 0, 2, 7)));
        });

        await handler.Handle(new DidChangeTextDocumentParams
        {
            TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = DocumentUri, Version = 2 },
            ContentChanges = new Container<TextDocumentContentChangeEvent>(
                new TextDocumentContentChangeEvent { Text = "@value | add(1)" })
        }, CancellationToken.None);

        Assert.That(PublishedDiagnostics().Last().Diagnostics, Is.Empty);
        Assert.That(PublishedDiagnostics().Last().Version, Is.EqualTo(2));
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
