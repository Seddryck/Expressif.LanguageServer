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
        var probeText = string.Concat(text.AsSpan(0, prefixStart), ProbeName, text.AsSpan(tokenEnd));
        if (!ProbeIsFunction(probeText))
            return [];

        return functions.Functions
            .SelectMany(function => new[]
                {
                    new CompletionSuggestion(function.Name, function.Name, true, prefixStart, tokenEnd - prefixStart)
                }
                .Concat(function.Aliases.Select(alias => new CompletionSuggestion(
                    alias, alias, false, prefixStart, tokenEnd - prefixStart))))
            .Where(suggestion => suggestion.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(suggestion => suggestion.Label, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(suggestion => suggestion.IsCanonical)
            .ThenBy(suggestion => suggestion.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
}
