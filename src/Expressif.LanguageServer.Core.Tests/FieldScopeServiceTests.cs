using Expressif.LanguageServer.Core.Documents;
using Expressif.LanguageServer.Core.Scopes;
using Expressif.LanguageServer.Core.Syntax;
using Expressif.Semantics;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class FieldScopeServiceTests
{
    private readonly FieldScopeService service = new();
    private static readonly Uri Uri = new("file:///scope.expr");

    [TestCase("{name := 1} | .name", ".name", "{name := 1}")]
    [TestCase("{name := 1} | ^.name", "^.name", "{name := 1}")]
    [TestCase("{items := {1}} | .items | map(.name | suffix(^^.items))", "^^.items", "{items := {1}}")]
    [TestCase(".items | map(.name)", ".name", ".items")]
    [TestCase(".items | filter(.enabled)", ".enabled", ".items")]
    [TestCase(".items |> (.name)", ".name", ".items")]
    [TestCase(".a.b.c", ".c", ".a.b")]
    [TestCase(".groups | map(.items | map(.value | suffix(^^.name)))", "^^.name", ".groups")]
    [TestCase(".items | map(.value | upper | ^.field)", "^.field", ".items")]
    [TestCase("suffix(({name := 1} | ^.name))", "^.name", "{name := 1}")]
    public void Scope_SelectsSupplyingRegion(string text, string selection, string supplier)
    {
        var document = Open(text);
        var scope = service.GetScope(document, text.LastIndexOf(selection, StringComparison.Ordinal));
        Assert.That(scope, Is.Not.Null);
        Assert.That(scope!.Supplier, Is.Not.Null);
        Assert.That(text.Substring(scope.Supplier!.Value.Start, scope.Supplier.Value.Length), Is.EqualTo(supplier));
        Assert.That(scope.Description, Does.Contain("not a field declaration"));
    }

    [TestCase(".name")]
    [TestCase("^.name")]
    [TestCase("@missing | .name")]
    [TestCase("map(.name)")]
    public void ExternalSource_HasHoverWithoutHighlight(string text)
    {
        var scope = service.GetScope(Open(text), text.IndexOf(".name", StringComparison.Ordinal));
        Assert.That(scope, Is.Not.Null);
        Assert.That(scope!.Supplier, Is.Null);
        Assert.That(scope.Description, Does.Contain("external input"));
    }

    [TestCase("^^.name", 0)]
    [TestCase("map(^^.name)", 4)]
    [TestCase("unknown | .name", 10)]
    [TestCase("unknown(.name)", 9)]
    [TestCase("map(.name |", 5)]
    [TestCase(".name", -1)]
    [TestCase(".name", 5)]
    [TestCase(".name ", 5)]
    public void UnsafeOrOutsideReference_ReturnsNothing(string text, int offset)
        => Assert.That(service.GetScope(Open(text), offset), Is.Null);

    [TestCase("map-over")]
    [TestCase("map-with")]
    public void DirectionalMaps_AgreeWithReleasedSemantics(string function)
    {
        var text = function + "(.name | suffix(^.suffix), {{name := \"item\", suffix := \"!\"}})";
        var document = Open(text);
        var references = new SemanticAnalyzer().Analyze(document.SyntaxTree!).References;
        Assert.That(references, Has.Count.EqualTo(2));
        foreach (var reference in references)
        {
            var actual = service.GetScope(document, reference.Span.Start);
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual!.Reference, Is.EqualTo(reference.Span));
            Assert.That(actual.Supplier, Is.EqualTo(reference.Source.Span));
        }
    }

    [Test]
    public void ChangesAndReopen_UseCurrentSnapshot()
    {
        var store = new DocumentStore(new SyntaxService());
        var first = store.Open(Uri, "{a := 1} | .a", 1);
        Assert.That(service.GetScope(first, 12)?.Supplier, Is.Not.Null);
        var second = store.Change(Uri, ".a", 2);
        Assert.That(service.GetScope(second, 0)?.Supplier, Is.Null);
        Assert.That(service.GetScope(second, 0)?.Description, Does.Contain("external input"));
        var invalid = store.Change(Uri, "map(.a |", 3);
        Assert.That(service.GetScope(invalid, 4), Is.Null);
        store.Close(Uri);
        Assert.That(service.GetScope(store.Open(Uri, "^^.a", 1), 0), Is.Null);
    }

    [Test]
    public void RootsAndElements_HaveDistinctExplanations()
    {
        Assert.That(service.GetScope(Open("^.a"), 0)?.Description, Does.Contain("current expression's root"));
        const string text = ".items | map(.name | suffix(^^.a))";
        Assert.That(service.GetScope(Open(text), text.IndexOf('^'))?.Description, Does.Contain("enclosing expression's input"));
        Assert.That(service.GetScope(Open("map(.a)"), 4)?.Description, Does.Contain("each element"));
    }

    private static DocumentSnapshot Open(string text) => new DocumentStore(new SyntaxService()).Open(Uri, text, 1);
}
