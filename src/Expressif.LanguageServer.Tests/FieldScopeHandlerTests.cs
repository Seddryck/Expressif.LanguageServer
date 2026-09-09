using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Hover;
using Expressif.LanguageServer.Core.Scopes;
using Expressif.LanguageServer.Core.Syntax;
using Expressif.LanguageServer.Handlers;
using Moq;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Expressif.LanguageServer.Tests;

[TestFixture]
public sealed class FieldScopeHandlerTests
{
    private static readonly DocumentUri Uri = DocumentUri.FromFileSystemPath("/workspace/scope.expr");
    private DocumentStore documents = null!;
    private DocumentHighlightHandler highlights = null!;
    private HoverHandler hovers = null!;

    [SetUp]
    public void SetUp()
    {
        documents = new(new SyntaxService());
        var scopes = new FieldScopeService();
        highlights = new(documents, scopes);
        hovers = new(documents, Mock.Of<IFunctionHoverService>(), scopes);
    }

    [Test]
    public void Registration_TargetsExpressif()
    {
        var options = DocumentHighlightHandler.GetRegistrationOptions();
        Assert.That(options.DocumentSelector, Is.Not.Null);
        Assert.That(options.DocumentSelector!.Single().Language, Is.EqualTo("expressif"));
    }

    [TestCase("{name := \"é😀\"} | .name", 0, 18, 0, 0, 0, 15)]
    [TestCase("{\r\n name := \"é😀\"\r\n} | .name", 2, 4, 0, 0, 2, 1)]
    [TestCase(".a.b", 0, 2, 0, 0, 0, 2)]
    public async Task Highlights_UseTextKindAndUtf16SupplierRangeAsync(
        string text, int line, int column, int startLine, int startColumn, int endLine, int endColumn)
    {
        documents.Open(Uri.ToUri(), text, 1);
        var result = await HighlightAsync(line, column);
        Assert.That(result!.Count(), Is.EqualTo(1));
        Assert.That(result!.Single().Kind, Is.EqualTo(DocumentHighlightKind.Text));
        Assert.That(result!.Single().Range, Is.EqualTo(new Range(startLine, startColumn, endLine, endColumn)));
        var hover = await HoverAsync(line, column);
        Assert.That(hover?.Contents.MarkupContent?.Value, Does.Contain("not a field declaration"));
    }

    [TestCase(".a", 0, 0)]
    [TestCase(".a", 0, 1)]
    public async Task ExternalAtBeginning_HasHoverWithoutHighlightAsync(string text, int line, int column)
    {
        documents.Open(Uri.ToUri(), text, 1);
        Assert.That(await HighlightAsync(line, column), Is.Empty);
        var hover = await HoverAsync(line, column);
        Assert.That(hover?.Range, Is.EqualTo(new Range(0, 0, 0, 2)));
        Assert.That(hover?.Contents.MarkupContent?.Value, Does.Contain("external input"));
    }

    [TestCase(0, -1)]
    [TestCase(-1, 0)]
    [TestCase(1, 0)]
    [TestCase(0, 4)]
    [TestCase(0, 5)]
    public async Task InvalidAndEndPositions_ReturnNothingAsync(int line, int column)
    {
        documents.Open(Uri.ToUri(), ".a.b", 1);
        Assert.That(await HighlightAsync(line, column), Is.Empty);
        Assert.That(await HoverAsync(line, column), Is.Null);
    }

    [Test]
    public async Task ChangeAndClose_DoNotReturnStaleHighlightsAsync()
    {
        documents.Open(Uri.ToUri(), ".a.b", 1);
        Assert.That((await HighlightAsync(0, 2))!.Count(), Is.EqualTo(1));
        documents.Change(Uri.ToUri(), "  .b", 2);
        Assert.That(await HighlightAsync(0, 2), Is.Empty);
        Assert.That((await HoverAsync(0, 2))?.Contents.MarkupContent?.Value, Does.Contain("external input"));
        documents.Change(Uri.ToUri(), "map(.b |", 3);
        Assert.That(await HighlightAsync(0, 4), Is.Empty);
        Assert.That(await HoverAsync(0, 4), Is.Null);
        documents.Close(Uri.ToUri());
        Assert.That(await HighlightAsync(0, 0), Is.Empty);
        Assert.That(await HoverAsync(0, 0), Is.Null);
    }

    private Task<DocumentHighlightContainer?> HighlightAsync(int line, int column) => highlights.Handle(new()
    {
        TextDocument = new TextDocumentIdentifier { Uri = Uri },
        Position = new Position(line, column)
    }, CancellationToken.None);

    private Task<Hover?> HoverAsync(int line, int column) => hovers.Handle(new HoverParams
    {
        TextDocument = new TextDocumentIdentifier { Uri = Uri },
        Position = new Position(line, column)
    }, CancellationToken.None);
}
