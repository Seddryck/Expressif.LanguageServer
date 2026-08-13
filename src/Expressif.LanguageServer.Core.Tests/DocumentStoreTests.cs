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

    [TestCase(2)]
    [TestCase(1)]
    public void Change_NonIncreasingVersion_Throws(int version)
    {
        store.Open(Uri, ".name", 2);

        Assert.That(
            () => store.Change(Uri, ".title", version),
            Throws.InvalidOperationException.With.Message.Contains("must be greater"));

        store.TryGet(Uri, out var document);
        Assert.That(document!.Text, Is.EqualTo(".name"));
    }

    [Test]
    public void ParallelChanges_KeepHighestVersion()
    {
        store.Open(Uri, ".name", 1);

        Parallel.For(2, 101, version =>
        {
            try
            {
                store.Change(Uri, $".field{version}", version);
            }
            catch (InvalidOperationException)
            {
                // A newer concurrent version won the conditional update.
            }
        });

        store.TryGet(Uri, out var document);
        Assert.Multiple(() =>
        {
            Assert.That(document!.Version, Is.EqualTo(100));
            Assert.That(document.Text, Is.EqualTo(".field100"));
        });
    }

    [Test]
    public async Task ChangeRacingClose_DoesNotReopenDocument()
    {
        var parseStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueParse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var syntax = new Mock<ISyntaxService>();
        syntax.Setup(service => service.Parse(".name"))
            .Returns(new SyntaxParseResult(null, []));
        syntax.Setup(service => service.Parse(".title"))
            .Returns(() =>
            {
                parseStarted.SetResult();
                continueParse.Task.GetAwaiter().GetResult();
                return new SyntaxParseResult(null, []);
            });
        store = new(syntax.Object);
        store.Open(Uri, ".name", 1);

        var change = Task.Run(() => store.Change(Uri, ".title", 2));
        await parseStarted.Task;
        Assert.That(store.Close(Uri), Is.True);
        continueParse.SetResult();

        Assert.That(async () => await change, Throws.InvalidOperationException);
        Assert.That(store.TryGet(Uri, out _), Is.False);
    }
}
