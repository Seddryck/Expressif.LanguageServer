using Expressif.LanguageServer.Core.CodeActions;
using Expressif.LanguageServer.Core.Diagnostics;
using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Syntax;
using Expressif.LanguageServer.Handlers;
using Expressif.Syntax;
using Moq;
using NUnit.Framework;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Expressif.LanguageServer.Tests;

[TestFixture]
public sealed class LegacyTupleReferenceHandlerTests
{
    private static readonly DocumentUri Uri = DocumentUri.FromFileSystemPath("/workspace/tuple.expr");
    private DocumentStore documents = null!;
    private Mock<ITextDocumentLanguageServer> textDocument = null!;
    private TextDocumentSyncHandler sync = null!;
    private CodeActionHandler actions = null!;

    [SetUp]
    public void SetUp()
    {
        documents = new(new SyntaxService());
        textDocument = new();
        var calls = new Mock<IFunctionCallDiagnosticService>();
        calls.Setup(service => service.GetDiagnostics(It.IsAny<RootExpressionSyntax>())).Returns([]);
        var lifecycle = new Mock<IFunctionLifecycleDiagnosticService>();
        lifecycle.Setup(service => service.GetDiagnostics(It.IsAny<RootExpressionSyntax>())).Returns([]);
        var functions = new Mock<IFunctionCodeActionService>();
        functions.Setup(service => service.GetReplacements(It.IsAny<RootExpressionSyntax>(), It.IsAny<int>(), It.IsAny<int>())).Returns([]);
        var references = new LegacyTupleReferenceService();
        sync = new(documents, calls.Object, lifecycle.Object,
            Mock.Of<ILanguageServerFacade>(server => server.TextDocument == textDocument.Object), references);
        actions = new(documents, functions.Object, references);
    }

    [TestCase("$^0", 0, 0, "$-1")]
    [TestCase("select(\"\u00e9\U0001f600\", $^12)", 0, 14, "$-13")]
    [TestCase("/* \u00e9\U0001f600 */\r\n  $^1", 1, 2, "$-2")]
    public async Task Migration_PublishesHintAndAppliesPreciseQuickFixAsync(string source, int line, int column, string expected)
    {
        await OpenAsync(source);
        var diagnostic = Publications().Single().Diagnostics.Single();
        var oldText = source.Contains("$^12", StringComparison.Ordinal) ? "$^12" : source.Contains("$^0", StringComparison.Ordinal) ? "$^0" : "$^1";
        var range = new Range(line, column, line, column + oldText.Length);
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Range, Is.EqualTo(range));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Hint));
            Assert.That(diagnostic.Tags, Does.Contain(DiagnosticTag.Deprecated));
            Assert.That(diagnostic.Message, Is.EqualTo($"Tuple reference '{oldText}' is deprecated. Use '{expected}' instead."));
        });
        var result = await RequestAsync(range);
        var action = result!.Single().CodeAction!;
        var edit = action.Edit!.Changes![Uri].Single();
        Assert.Multiple(() =>
        {
            Assert.That(action.Title, Is.EqualTo($"Replace '{oldText}' with '{expected}'"));
            Assert.That(action.Kind, Is.EqualTo(CodeActionKind.QuickFix));
            Assert.That(action.IsPreferred, Is.True);
            Assert.That(action.Diagnostics!.Single().Range, Is.EqualTo(range));
            Assert.That(edit.Range, Is.EqualTo(range));
            Assert.That(edit.NewText, Is.EqualTo(expected));
        });
        var start = source.IndexOf(oldText, StringComparison.Ordinal);
        var edited = source.Remove(start, oldText.Length).Insert(start, edit.NewText);
        await sync.Handle(new DidChangeTextDocumentParams
        {
            TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = Uri, Version = 2 },
            ContentChanges = new Container<TextDocumentContentChangeEvent>(new TextDocumentContentChangeEvent { Text = edited })
        }, CancellationToken.None);
        Assert.That(Publications().Last().Diagnostics, Is.Empty);
        Assert.That(Publications().Last().Version, Is.EqualTo(2));
        Assert.That(await RequestAsync(new Range(line, column, line, column)), Is.Empty);
        Assert.That(documents.TryGet(Uri.ToUri(), out var document), Is.True);
        Assert.That(document!.Text, Is.EqualTo(source.Replace(oldText, expected, StringComparison.Ordinal)));
    }

    [TestCase("$-1")]
    [TestCase("$0")]
    [TestCase("^$1")]
    [TestCase("\"$^0\" /* $^1 */")]
    [TestCase("$-0")]
    [TestCase("$^")]
    public async Task NonLegacyOrMalformedSyntax_HasNoMigrationAsync(string source)
    {
        await OpenAsync(source);
        Assert.That(Publications().Single().Diagnostics.Where(diagnostic => diagnostic.Tags?.Contains(DiagnosticTag.Deprecated) == true), Is.Empty);
        Assert.That(await RequestAsync(new Range(0, 0, 0, source.Length)), Is.Empty);
        if (source is "$-0" or "$^")
            Assert.That(Publications().Single().Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.True);
    }

    [Test]
    public async Task MaximumIndex_HasNoUnsafeQuickFixAsync()
    {
        await OpenAsync("$^2147483647");
        Assert.That(Publications().Single().Diagnostics.Single().Tags, Does.Contain(DiagnosticTag.Deprecated));
        Assert.That(await RequestAsync(new Range(0, 0, 0, 0)), Is.Empty);
    }

    [Test]
    public async Task Close_ClearsDeprecationAndRemovesActionsAsync()
    {
        await OpenAsync("$^0");
        await sync.Handle(new DidCloseTextDocumentParams { TextDocument = new TextDocumentIdentifier { Uri = Uri } }, CancellationToken.None);
        Assert.That(Publications().Last().Diagnostics, Is.Empty);
        Assert.That(await RequestAsync(new Range(0, 0, 0, 0)), Is.Empty);
    }

    [Test]
    public async Task Selection_OnlyOffersIntersectingReferenceAndRespectsRequestedKindAsync()
    {
        await OpenAsync("select($^0, $^1)");
        var result = await RequestAsync(new Range(0, 8, 0, 8));
        Assert.That(result!.Single().CodeAction!.Title, Is.EqualTo("Replace '$^0' with '$-1'"));
        Assert.That(await RequestAsync(new Range(0, 0, 0, 6)), Is.Empty);
        Assert.That(await RequestAsync(new Range(0, 8, 0, 8), CodeActionKind.Refactor), Is.Empty);
        Assert.That((await RequestAsync(new Range(0, 8, 0, 8), CodeActionKind.QuickFix))!.Count(), Is.EqualTo(1));
        Assert.That(await RequestAsync(new Range(0, 10, 0, 7)), Is.Empty);
    }

    private Task OpenAsync(string source) => sync.Handle(new DidOpenTextDocumentParams
    {
        TextDocument = new TextDocumentItem { Uri = Uri, LanguageId = "expressif", Version = 1, Text = source }
    }, CancellationToken.None);

    private Task<CommandOrCodeActionContainer?> RequestAsync(Range range, CodeActionKind? kind = null) => actions.Handle(new CodeActionParams
    {
        TextDocument = new TextDocumentIdentifier { Uri = Uri }, Range = range,
        Context = new CodeActionContext { Only = kind is null ? null : new Container<CodeActionKind>(kind.Value) }
    }, CancellationToken.None);

    private IEnumerable<PublishDiagnosticsParams> Publications() => textDocument.Invocations
        .SelectMany(invocation => invocation.Arguments).OfType<PublishDiagnosticsParams>();
}
