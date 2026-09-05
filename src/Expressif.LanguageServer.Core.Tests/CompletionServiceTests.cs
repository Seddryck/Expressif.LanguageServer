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

    [Test]
    public void GetCompletions_DeprecatedFunction_IsMarkedAndRankedAfterActiveFunctions()
    {
        var service = new CompletionService(new TestFunctionCatalog(
        [
            new("append", [], [], "Appends text.", "Text", true, "suffix", "3.0"),
            new("suffix", [], [], "Suffixes text.", "Text")
        ]));

        var result = service.GetCompletions(string.Empty, 0);

        Assert.Multiple(() =>
        {
            Assert.That(result.Select(item => item.Label), Is.EqualTo(new[] { "suffix", "append" }));
            Assert.That(result[0].Deprecated, Is.False);
            Assert.That(result[1].Deprecated, Is.True);
            Assert.That(result[1].Replacement, Is.EqualTo("suffix"));
            Assert.That(result[1].Sunset, Is.EqualTo("3.0"));
        });
    }

    [Test]
    public void GetCompletions_RequiredParameters_AreIncludedInSnippetOrder()
    {
        var service = CreateService(
            new FunctionMetadata("pad-right", [],
            [
                new("length", false, "Length."),
                new("character", false, "Character.")
            ], "Pads text.", "Text"));

        var suggestion = service.GetCompletions("pad", 3).Single();

        Assert.That(suggestion.SnippetParameters, Is.EqualTo(new[] { "length", "character" }));
    }

    [Test]
    public void GetCompletions_OptionalParameters_AreOmittedFromPrimarySnippet()
    {
        var service = CreateService(
            new FunctionMetadata("token", [],
            [
                new("index", false, "Index."),
                new("separator", true, "Separator.")
            ], "Gets a token.", "Text"));

        var suggestion = service.GetCompletions("tok", 3).Single();

        Assert.That(suggestion.SnippetParameters, Is.EqualTo(new[] { "index" }));
    }

    [Test]
    public void GetCompletions_VariadicParameter_ReceivesOneSnippetPlaceholder()
    {
        var service = CreateService(
            new FunctionMetadata("concat", [],
            [
                new("value", false, "Value."),
                new("values", true, "Additional values.", true, 0)
            ], "Concatenates values.", "Text"));

        var suggestion = service.GetCompletions("con", 3).Single();

        Assert.That(suggestion.SnippetParameters, Is.EqualTo(new[] { "value", "values" }));
    }

    [Test]
    public void GetCompletions_ZeroArgumentFunction_HasNoSnippetParameters()
    {
        var suggestion = service.GetCompletions("upp", 3).Single(item => item.Label == "upper");

        Assert.That(suggestion.SnippetParameters, Is.Empty);
    }

    [TestCase("@foo | pad(")]
    [TestCase("@foo | pad  (")]
    public void GetCompletions_OpeningParenthesisAlreadyPresent_UsesPlainName(string text)
    {
        var service = CreateService(
            new FunctionMetadata("pad-right", [], [new("length", false, "Length.")], "Pads text.", "Text"));
        var cursor = text.IndexOf("pad", StringComparison.Ordinal) + 3;

        var suggestion = service.GetCompletions(text, cursor).Single();

        Assert.That(suggestion.SnippetParameters, Is.Null);
    }

    [Test]
    public void GetCompletions_AfterPipelineRecordAccess_ReturnsDefinedRecordFields()
    {
        const string text = "record(name := \"Alice\", age := 30) |> upper |> .";

        var result = service.GetCompletions(text, text.Length);

        Assert.Multiple(() =>
        {
            Assert.That(result.Select(item => item.Label), Is.EqualTo(new[] { "age", "name" }));
            Assert.That(result.All(item => item.Kind == CompletionSuggestionKind.Field), Is.True);
            Assert.That(result.All(item => item.ReplacementStart == text.Length), Is.True);
            Assert.That(result.All(item => item.ReplacementLength == 0), Is.True);
        });
    }

    [Test]
    public void GetCompletions_RecordFieldPrefix_FiltersAndReplacesExistingFieldName()
    {
        const string text = "record(name := \"Alice\", nickname := \"Al\", age := 30) |> .nme";
        var cursor = text.IndexOf("nme", StringComparison.Ordinal) + 1;

        var result = service.GetCompletions(text, cursor);

        var suggestion = result.Single(item => item.Label == "name");
        Assert.Multiple(() =>
        {
            Assert.That(suggestion.InsertText, Is.EqualTo("name"));
            Assert.That(suggestion.ReplacementStart, Is.EqualTo(text.IndexOf("nme", StringComparison.Ordinal)));
            Assert.That(suggestion.ReplacementLength, Is.EqualTo(3));
        });
    }

    [Test]
    public void GetCompletions_RecordAccessWithoutUpstreamRecord_ReturnsNoFields()
    {
        const string text = "@customer |> upper |> .";

        var result = service.GetCompletions(text, text.Length);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GetCompletions_QuotedRecordField_ReplacesDotWithLongFormAccess()
    {
        const string text = "record(\"display name\" := \"Alice\") |> .";

        var suggestion = service.GetCompletions(text, text.Length).Single();

        Assert.Multiple(() =>
        {
            Assert.That(suggestion.Label, Is.EqualTo("display name"));
            Assert.That(suggestion.InsertText, Is.EqualTo("field(\"display name\")"));
            Assert.That(suggestion.ReplacementStart, Is.EqualTo(text.Length - 1));
            Assert.That(suggestion.ReplacementLength, Is.EqualTo(1));
        });
    }

    private static CompletionService CreateService(params FunctionMetadata[] functions)
        => new(new TestFunctionCatalog(functions));

    private sealed class TestFunctionCatalog(IReadOnlyList<FunctionMetadata> functions) : IFunctionCatalog
    {
        public IReadOnlyList<FunctionMetadata> Functions { get; } = functions;
    }
}
