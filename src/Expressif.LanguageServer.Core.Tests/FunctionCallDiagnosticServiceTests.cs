using Expressif.LanguageServer.Core.Diagnostics;
using Expressif.LanguageServer.Core.Functions;
using Expressif.LanguageServer.Core.Syntax;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class FunctionCallDiagnosticServiceTests
{
    [Test]
    public void GetDiagnostics_UnknownFunction_ReportsIdentifier()
    {
        var diagnostic = CreateService().GetDiagnostics(Parse("mispelled()" )).Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Message, Is.EqualTo("Unknown function 'mispelled'."));
            Assert.That(diagnostic.Start, Is.EqualTo(0));
            Assert.That(diagnostic.Length, Is.EqualTo(9));
        });
    }

    [Test]
    public void GetDiagnostics_KnownAlias_ReturnsNoDiagnostic()
    {
        var service = CreateService(new FunctionMetadata(
            "upper", ["text-to-upper"], [], "Uppercase.", "Text"));

        Assert.That(service.GetDiagnostics(Parse("text-to-upper()")), Is.Empty);
    }

    [Test]
    public void GetDiagnostics_MissingRequiredArgument_ReportsFunctionCall()
    {
        var service = CreateService(new FunctionMetadata("add", [],
            [new("value", false, "Value.")], "Adds.", "Numeric"));

        var diagnostic = service.GetDiagnostics(Parse("add()" )).Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Message,
                Is.EqualTo("Function 'add' requires at least 1 argument, but 0 arguments were provided."));
            Assert.That(diagnostic.Start, Is.EqualTo(0));
            Assert.That(diagnostic.Length, Is.EqualTo(5));
        });
    }

    [Test]
    public void GetDiagnostics_VariadicMinimumCardinality_RequiresEveryMandatoryArgument()
    {
        var service = CreateService(new FunctionMetadata("coalesce", [],
            [new("expressions", false, "Candidates.", true, 2)], "Coalesces.", "Flow"));

        var diagnostic = service.GetDiagnostics(Parse("coalesce(@value)" )).Single();

        Assert.That(diagnostic.Message,
            Is.EqualTo("Function 'coalesce' requires at least 2 arguments, but 1 argument was provided."));
    }

    [Test]
    public void GetDiagnostics_AllRequiredArgumentsProvided_ReturnsNoDiagnostic()
    {
        var service = CreateService(new FunctionMetadata("add", [],
            [new("value", false, "Value."), new("fallback", true, "Fallback.", false, 0)],
            "Adds.", "Numeric"));

        Assert.That(service.GetDiagnostics(Parse("add(1)")), Is.Empty);
    }

    [Test]
    public void GetDiagnostics_RealCatalogMissingAddArgument_ReportsDiagnostic()
    {
        var service = new FunctionCallDiagnosticService(new ExpressifFunctionCatalog());

        Assert.That(service.GetDiagnostics(Parse("add()")), Has.Count.EqualTo(1));
    }

    [TestCase("upper(1)", "upper", 0, 1)]
    [TestCase("add(1, 2)", "add", 1, 2)]
    [TestCase("ADD(1, 2)", "ADD", 1, 2)]
    [TestCase("plus(1, 2)", "plus", 1, 2)]
    [TestCase("add(1, 2, 3)", "add", 2, 3)]
    public void GetDiagnostics_TooManyArguments_ReportsFunctionCall(
        string source, string name, int maximum, int supplied)
    {
        FunctionParameterMetadata[] parameters = maximum switch
        {
            0 => [],
            1 => [new("value", false, "Value.")],
            _ => [new("value", false, "Value."), new("fallback", true, "Fallback.", false, 0)]
        };
        var service = CreateService(new FunctionMetadata(
            maximum == 0 ? "upper" : "add", ["plus"], parameters, "Description.", "Test"));

        var diagnostic = service.GetDiagnostics(Parse(source)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Message, Is.EqualTo(
                $"Function '{name}' accepts at most {maximum} argument{(maximum == 1 ? "" : "s")}, " +
                $"but {supplied} argument{(supplied == 1 ? " was" : "s were")} provided."));
            Assert.That(diagnostic.Start, Is.Zero);
            Assert.That(diagnostic.Length, Is.EqualTo(source.Length));
        });
    }

    [TestCase("add(1)")]
    [TestCase("add(1, 2)")]
    public void GetDiagnostics_WithinOptionalParameterLimit_ReturnsNoDiagnostic(string source)
    {
        var service = CreateService(new FunctionMetadata("add", [],
            [new("value", false, "Value."), new("fallback", true, "Fallback.", false, 0)],
            "Adds.", "Numeric"));

        Assert.That(service.GetDiagnostics(Parse(source)), Is.Empty);
    }

    [TestCase(0)]
    [TestCase(2)]
    public void GetDiagnostics_VariadicArgumentsAboveParameterCount_ReturnsNoDiagnostic(int minimum)
    {
        var service = CreateService(new FunctionMetadata("coalesce", [],
            [new("expressions", minimum == 0, "Candidates.", true, minimum)], "Coalesces.", "Flow"));

        Assert.That(service.GetDiagnostics(Parse("coalesce(1, 2, 3, 4)")), Is.Empty);
    }

    [Test]
    public void GetDiagnostics_NestedMultilineCall_ReportsOnlyExcessiveCall()
    {
        var service = CreateService(
            new FunctionMetadata("coalesce", [], [new("expressions", false, "Candidates.", true)], "", ""),
            new FunctionMetadata("upper", [], [], "", ""));
        const string source = "coalesce(\n  upper(1),\n  2)";

        var diagnostic = service.GetDiagnostics(Parse(source)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Start, Is.EqualTo(source.IndexOf("upper", StringComparison.Ordinal)));
            Assert.That(diagnostic.Length, Is.EqualTo("upper(1)".Length));
        });
    }

    [TestCase("add(1, 2, 3)", 1)]
    [TestCase("add(1, 2)", 0)]
    [TestCase("add(1)", 0)]
    public void GetDiagnostics_RealCatalog_RespectsMaximumArgumentCount(string source, int count)
    {
        var service = new FunctionCallDiagnosticService(new ExpressifFunctionCatalog());

        Assert.That(service.GetDiagnostics(Parse(source)), Has.Count.EqualTo(count));
    }

    private static FunctionCallDiagnosticService CreateService(params FunctionMetadata[] functions)
        => new(new TestFunctionCatalog(functions));

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
