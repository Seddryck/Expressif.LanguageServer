using Expressif.LanguageServer.Core.Functions;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Completion;

public sealed class CompletionService(IFunctionCatalog functions) : ICompletionService
{
    private const string ProbeName = "expressif-completion-probe";

    public IReadOnlyList<CompletionSuggestion> GetCompletions(string text, int cursorOffset)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (cursorOffset < 0 || cursorOffset > text.Length)
            throw new ArgumentOutOfRangeException(nameof(cursorOffset));

        var prefixStart = cursorOffset;
        while (prefixStart > 0 && IsFunctionNameCharacter(text[prefixStart - 1]))
            prefixStart--;

        var tokenEnd = cursorOffset;
        while (tokenEnd < text.Length && IsFunctionNameCharacter(text[tokenEnd]))
            tokenEnd++;

        var prefix = text[prefixStart..cursorOffset];
        var pipelineOperator = FindPrecedingPipelineOperator(text, prefixStart);
        var hasOpeningParenthesis = HasOpeningParenthesis(text, tokenEnd);
        var probeText = pipelineOperator?.Length == 2
            ? string.Concat(
                text.AsSpan(0, pipelineOperator.Value.Start + 1),
                text.AsSpan(pipelineOperator.Value.Start + 2, prefixStart - pipelineOperator.Value.Start - 2),
                ProbeName,
                text.AsSpan(tokenEnd))
            : string.Concat(text.AsSpan(0, prefixStart), ProbeName, text.AsSpan(tokenEnd));
        if (!ProbeIsFunction(probeText) &&
            (!hasOpeningParenthesis || !ProbeIsFunction($"{probeText})")))
            return [];

        var needsLeadingSpace = pipelineOperator is { } precedingOperator
            && precedingOperator.Start + precedingOperator.Length == prefixStart;
        var replacementStart = needsLeadingSpace ? cursorOffset : prefixStart;
        var replacementLength = needsLeadingSpace ? tokenEnd - cursorOffset : tokenEnd - prefixStart;
        return functions.Functions
            .SelectMany(function => new[]
                {
                    CreateSuggestion(function, function.Name, true)
                }
                .Concat(function.Aliases.Select(alias => CreateSuggestion(function, alias, false))))
            .Where(suggestion => suggestion.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(suggestion => suggestion.Deprecated)
            .ThenByDescending(suggestion => suggestion.IsCanonical)
            .ThenBy(suggestion => suggestion.Label, StringComparer.OrdinalIgnoreCase)
            .DistinctBy(suggestion => suggestion.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        CompletionSuggestion CreateSuggestion(FunctionMetadata function, string functionName, bool isCanonical)
            => new(
                functionName,
                needsLeadingSpace ? $" {functionName}" : functionName,
                isCanonical,
                replacementStart,
                replacementLength,
                function.Description,
                function.Deprecated,
                function.Replacement,
                function.Sunset,
                hasOpeningParenthesis
                    ? null
                    : function.Parameters
                        .Where(parameter => !parameter.Optional || parameter.Variadic)
                        .Select(parameter => parameter.Name)
                        .ToArray());
    }

    private static bool ProbeIsFunction(string probeText)
    {
        try
        {
            var syntax = ExpressifSyntax.Parse(probeText);
            return DescendantsAndSelf(syntax)
                .OfType<FunctionCallSyntax>()
                .Any(function => function.Name.Equals(ProbeName, StringComparison.Ordinal));
        }
        catch (ExpressifSyntaxException)
        {
            return false;
        }
    }

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        foreach (var descendant in DescendantsAndSelf(child))
            yield return descendant;
    }

    private static bool IsFunctionNameCharacter(char character)
        => char.IsAsciiLetterOrDigit(character) || character is '-' or '_';

    private static bool HasOpeningParenthesis(string text, int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
            position++;

        return position < text.Length && text[position] == '(';
    }

    private static (int Start, int Length)? FindPrecedingPipelineOperator(string text, int position)
    {
        var index = position - 1;
        while (index >= 0 && char.IsWhiteSpace(text[index]))
            index--;

        if (index > 0 && text[index - 1] == '|' && text[index] == '>')
            return (index - 1, 2);

        return index >= 0 && text[index] == '|' ? (index, 1) : null;
    }
}
