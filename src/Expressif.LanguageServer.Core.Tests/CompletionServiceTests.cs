using Expressif.LanguageServer.Core.Completion;
using Expressif.LanguageServer.Core.Functions;
using NUnit.Framework;

namespace Expressif.LanguageServer.Core.Tests;

[TestFixture]
public sealed class CompletionServiceTests
{
    private static readonly IFunctionCatalog Catalog = new TestFunctionCatalog(
    [
        new("lower", ["text-to-lower"], [], "Lowercase text.", "Text"),
        new("title-case", ["text-to-title-case"], [], "Title-case text.", "Text"),
        new("upper", ["text-to-upper"], [], "Uppercase text.", "Text")
    ]);

    private readonly CompletionService service = new(Catalog);

    [TestCase("@foo | text-to-", "text-to-", 3)]
    [TestCase("text-to-", "text-to-", 3)]
    public void GetCompletions_FunctionPrefix_ReturnsMatchingNames(
        string text, string prefix, int expectedCount)
    {
        var result = service.GetCompletions(text, text.Length);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(expectedCount));
            Assert.That(result.All(item => item.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(result.All(item => item.ReplacementStart == text.Length - prefix.Length), Is.True);
            Assert.That(result.All(item => item.ReplacementLength == prefix.Length), Is.True);
        });
    }

    [Test]
    public void GetCompletions_EmptyPipelinePosition_ReturnsAvailableFunctions()
    {
        const string text = "@foo | ";

        var result = service.GetCompletions(text, text.Length);

        Assert.That(result.Select(item => item.Label), Does.Contain("upper"));
        Assert.That(result.Select(item => item.Label), Does.Contain("text-to-upper"));
    }

    [TestCase("@foo |")]
    [TestCase("@foo |>")]
    public void GetCompletions_AdjacentToPipelineOperator_InsertsLeadingSpace(string text)
    {
        var result = service.GetCompletions(text, text.Length);

        var suggestion = result.Single(item => item.Label == "upper");
        Assert.Multiple(() =>
        {
            Assert.That(suggestion.InsertText, Is.EqualTo(" upper"));
            Assert.That(suggestion.ReplacementStart, Is.EqualTo(text.Length));
            Assert.That(suggestion.ReplacementLength, Is.Zero);
        });
    }

    [TestCase("@foo | ")]
    [TestCase("@foo |> ")]
    public void GetCompletions_SeparatedFromPipelineOperator_DoesNotInsertLeadingSpace(string text)
    {
        var result = service.GetCompletions(text, text.Length);

        var suggestion = result.Single(item => item.Label == "upper");
        Assert.That(suggestion.InsertText, Is.EqualTo("upper"));
    }

    [Test]
    public void GetCompletions_InsideLiteral_ReturnsNoFunctions()
    {
        const string text = "@foo | suffix(\"text-to-\")";
        var cursor = text.IndexOf("text-to-", StringComparison.Ordinal) + "text-to-".Length;

        var result = service.GetCompletions(text, cursor);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetCompletions_CursorInsideFunctionName_ReplacesWholeToken()
    {
        const string text = "@foo | text-to-uppr";
        var cursor = text.IndexOf("uppr", StringComparison.Ordinal) + 2;

        var result = service.GetCompletions(text, cursor);

        var suggestion = result.Single(item => item.Label == "text-to-upper");
        var edited = string.Concat(
            text.AsSpan(0, suggestion.ReplacementStart),
            suggestion.InsertText,
            text.AsSpan(suggestion.ReplacementStart + suggestion.ReplacementLength));
        Assert.That(edited, Is.EqualTo("@foo | text-to-upper"));
    }

    [Test]
    public void GetCompletions_ResultsAreDeterministicAndPreferCanonicalNames()
    {
        var result = service.GetCompletions(string.Empty, 0);

        Assert.That(result.Select(item => item.Label), Is.EqualTo(new[]
        {
            "lower", "title-case", "upper", "text-to-lower", "text-to-title-case", "text-to-upper"
        }));
    }

    private sealed class TestFunctionCatalog(IReadOnlyList<FunctionMetadata> functions) : IFunctionCatalog
    {
        public IReadOnlyList<FunctionMetadata> Functions { get; } = functions;
    }
}
