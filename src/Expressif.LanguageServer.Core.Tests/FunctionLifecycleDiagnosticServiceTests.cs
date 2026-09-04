using Expressif.LanguageServer.Core.Diagnostics;
using Expressif.LanguageServer.Core.Functions;
using Expressif.LanguageServer.Core.Syntax;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class FunctionLifecycleDiagnosticServiceTests
{
    [Test]
    public void GetDiagnostics_DeprecatedFunction_ReportsReplacementAndSunset()
    {
        var service = new FunctionLifecycleDiagnosticService(new TestFunctionCatalog(
        [
            new("append", [], [], "Append text.", "Text", true, "suffix", "3.0")
        ]));
        var syntax = Parse(".name | append(\"-x\")");

        var diagnostic = service.GetDiagnostics(syntax).Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.FunctionName, Is.EqualTo("append"));
            Assert.That(diagnostic.Message, Is.EqualTo(
                "Function 'append' is deprecated. Use 'suffix' instead. It sunsets in Expressif 3.0."));
            Assert.That(diagnostic.IdentifierStart, Is.EqualTo(8));
            Assert.That(diagnostic.IdentifierLength, Is.EqualTo(6));
        });
    }

    [Test]
    public void GetDiagnostics_ActiveFunction_ReturnsNoDiagnostic()
    {
        var service = new FunctionLifecycleDiagnosticService(new TestFunctionCatalog(
        [
            new("suffix", [], [], "Suffix text.", "Text")
        ]));

        Assert.That(service.GetDiagnostics(Parse(".name | suffix(\"-x\")")), Is.Empty);
    }

    [Test]
    public void GetDiagnostics_MissingReplacementAndSunset_FallsBackCleanly()
    {
        var service = new FunctionLifecycleDiagnosticService(new TestFunctionCatalog(
        [
            new("legacy", [], [], "Legacy.", "Text", true)
        ]));

        Assert.That(service.GetDiagnostics(Parse("legacy()" )).Single().Message,
            Is.EqualTo("Function 'legacy' is deprecated."));
    }

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
