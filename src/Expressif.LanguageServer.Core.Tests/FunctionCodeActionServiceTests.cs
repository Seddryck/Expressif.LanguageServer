using Expressif.LanguageServer.Core.CodeActions;
using Expressif.LanguageServer.Core.Functions;
using Expressif.LanguageServer.Core.Syntax;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class FunctionCodeActionServiceTests
{
    [Test]
    public void GetReplacements_SafeDirectReplacement_ReturnsRename()
    {
        var service = CreateService(safe: true);

        var replacement = service.GetReplacements(Parse("legacy()"), 2, 0).Single();

        Assert.Multiple(() =>
        {
            Assert.That(replacement.OldName, Is.EqualTo("legacy"));
            Assert.That(replacement.NewName, Is.EqualTo("modern"));
            Assert.That(replacement.IdentifierStart, Is.Zero);
            Assert.That(replacement.IdentifierLength, Is.EqualTo(6));
        });
    }

    [Test]
    public void GetReplacements_UnsafeReplacement_DoesNotReturnBlindRename()
    {
        var service = CreateService(safe: false);

        Assert.That(service.GetReplacements(Parse("legacy()"), 2, 0), Is.Empty);
    }

    [Test]
    public void GetReplacements_SelectionExtendsPastSyntaxText_ReturnsRename()
    {
        var service = CreateService(safe: true);

        var replacement = service.GetReplacements(Parse("legacy()"), 0, 10).Single();

        Assert.That(replacement.NewName, Is.EqualTo("modern"));
    }

    [Test]
    public void GetReplacements_SelectionAfterSyntaxText_ReturnsNoRename()
    {
        var service = CreateService(safe: true);

        Assert.That(service.GetReplacements(Parse("legacy()"), 9, 1), Is.Empty);
    }

    private static FunctionCodeActionService CreateService(bool safe) => new(new TestFunctionCatalog(
    [
        new("legacy", [], [], "Legacy.", "Text", true, "modern", "3.0", safe),
        new("modern", [], [], "Modern.", "Text")
    ]));

    private static Expressif.Syntax.RootExpressionSyntax Parse(string text)
    {
        var result = new SyntaxService().Parse(text);
        Assert.That(result.SyntaxTree, Is.Not.Null, string.Join(Environment.NewLine, result.Errors));
        return result.SyntaxTree!;
    }

    private sealed class TestFunctionCatalog(IReadOnlyList<FunctionMetadata> functions) : IFunctionCatalog
    {
        public IReadOnlyList<FunctionMetadata> Functions { get; } = functions;
    }
}
