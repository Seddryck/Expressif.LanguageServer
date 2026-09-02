using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.SignatureHelp;
using Expressif.LanguageServer.Core.Syntax;
using Expressif.LanguageServer.Handlers;
using Moq;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Tests;

[TestFixture]
public sealed class SignatureHelpHandlerTests
{
    [Test]
    public async Task Handle_OpenDocument_MapsSignatureAndParameterDocumentationAsync()
    {
        const string text = "foo(1, 2)";
        var syntax = new SyntaxService();
        var documents = new DocumentStore(syntax);
        var uri = DocumentUri.FromFileSystemPath("/workspace/example.expr");
        documents.Open(uri.ToUri(), text, 1);

        var signatures = new Mock<IFunctionSignatureHelpService>();
        signatures.Setup(service => service.GetSignatureHelp(It.IsAny<Expressif.Syntax.RootExpressionSyntax>(), 7))
            .Returns(new FunctionSignatureHelp(
                "foo(first, second?)",
                "Combines values.",
                [new("first", "First value."), new("second?", "Second value.")],
                1));
        var handler = new SignatureHelpHandler(documents, signatures.Object);

        var result = await handler.Handle(new SignatureHelpParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri },
            Position = new Position(0, 7)
        }, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.ActiveSignature, Is.Zero);
            Assert.That(result.ActiveParameter, Is.EqualTo(1));
            Assert.That(result.Signatures.Single().Label, Is.EqualTo("foo(first, second?)"));
            Assert.That(result.Signatures.Single().Parameters!.Count(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Handle_ClosedDocument_ReturnsNoSignatureHelpAsync()
    {
        var handler = new SignatureHelpHandler(
            new DocumentStore(Mock.Of<ISyntaxService>()),
            Mock.Of<IFunctionSignatureHelpService>());

        var result = await handler.Handle(new SignatureHelpParams
        {
            TextDocument = new TextDocumentIdentifier
            {
                Uri = DocumentUri.FromFileSystemPath("/workspace/missing.expr")
            },
            Position = new Position(0, 0)
        }, CancellationToken.None);

        Assert.That(result, Is.Null);
    }
}
