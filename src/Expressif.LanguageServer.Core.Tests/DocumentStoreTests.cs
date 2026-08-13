using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Syntax;
using Moq;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class DocumentStoreTests
{
    private static readonly Uri Uri = new("file:///workspace/example.expr");
    private DocumentStore store = null!;

    [SetUp]
    public void SetUp()
    {
        var syntax = new Mock<ISyntaxService>();
        syntax.Setup(service => service.Parse(It.IsAny<string>()))
            .Returns(new SyntaxParseResult(null, []));
        store = new(syntax.Object);
    }

    [Test]
    public void Open_StoresTextAndVersion()
    {
        store.Open(Uri, ".name", 1);

        Assert.That(store.TryGet(Uri, out var document), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(document!.Text, Is.EqualTo(".name"));
            Assert.That(document.Version, Is.EqualTo(1));
            Assert.That(document.SyntaxErrors, Is.Empty);
        });
    }

    [Test]
    public void Change_ReplacesStoredSnapshot()
    {
        store.Open(Uri, ".name", 1);
        store.Change(Uri, ".title", 2);

        store.TryGet(Uri, out var document);
        Assert.Multiple(() =>
        {
            Assert.That(document!.Text, Is.EqualTo(".title"));
            Assert.That(document.Version, Is.EqualTo(2));
        });
    }

    [Test]
    public void Close_RemovesDocument()
    {
        store.Open(Uri, ".name", 1);

        Assert.That(store.Close(Uri), Is.True);
        Assert.That(store.TryGet(Uri, out _), Is.False);
    }

    [Test]
    public void Change_UnknownDocument_Throws()
        => Assert.That(() => store.Change(Uri, ".name", 1), Throws.InvalidOperationException);
}
