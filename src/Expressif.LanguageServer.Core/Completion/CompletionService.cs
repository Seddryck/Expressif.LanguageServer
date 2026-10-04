using Expressif.LanguageServer.Core.Functions;
using Expressif.Syntax;
using Expressif.Types;

namespace Expressif.LanguageServer.Core.Completion;

public sealed class CompletionService(IFunctionCatalog functions) : ICompletionService
{
    private const string ProbeName = "expressif-completion-probe";

    public IReadOnlyList<CompletionSuggestion> GetCompletions(string text, int cursorOffset)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (cursorOffset < 0 || cursorOffset > text.Length)
            throw new ArgumentOutOfRangeException(nameof(cursorOffset));

        if (TryGetTypeLiteralCompletion(text, cursorOffset, out var typeCompletions))
            return typeCompletions;

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
        var context = GetFunctionContext(probeText);
        if (context == FunctionCompletionContext.None && hasOpeningParenthesis)
            context = GetFunctionContext($"{probeText})");
        if (context == FunctionCompletionContext.None)
            return [];

        var isTupleBinding = context is FunctionCompletionContext.TupleBindingPrefix or
            FunctionCompletionContext.TupleBindingPostfix;
        var needsLeadingSpace = pipelineOperator is { } precedingOperator
            && precedingOperator.Start + precedingOperator.Length == prefixStart;
        var replacementStart = needsLeadingSpace ? cursorOffset : prefixStart;
        var replacementLength = needsLeadingSpace ? tokenEnd - cursorOffset : tokenEnd - prefixStart;
        return functions.Functions
            .Where(function => !isTupleBinding || function.SupportsTupleBinding)
            .SelectMany(CreateSuggestions)
            .Where(suggestion => suggestion.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(suggestion => suggestion.Deprecated)
            .ThenByDescending(suggestion => suggestion.IsCanonical)
            .ThenBy(suggestion => suggestion.Label, StringComparer.OrdinalIgnoreCase)
            .DistinctBy(suggestion => suggestion.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        IEnumerable<CompletionSuggestion> CreateSuggestions(FunctionMetadata function)
        {
            foreach (var (name, canonical) in new[] { (function.Name, true) }
                         .Concat(function.Aliases.Select(alias => (alias, false))))
            {
                yield return CreateSuggestion(function, name, canonical, isTupleBinding);
                if (!isTupleBinding && !hasOpeningParenthesis && function.SupportsTupleBinding)
                    yield return CreateSuggestion(function, name, canonical, true, producePostfix: true);
            }
        }

        CompletionSuggestion CreateSuggestion(
            FunctionMetadata function,
            string functionName,
            bool isCanonical,
            bool tupleBinding,
            bool producePostfix = false)
            => new(
                producePostfix ? $"{functionName}~" : functionName,
                $"{(needsLeadingSpace ? " " : string.Empty)}{functionName}{(producePostfix ? "~" : string.Empty)}",
                isCanonical,
                replacementStart,
                replacementLength,
                tupleBinding
                    ? $"{function.Description}\n\n{CreateTupleBindingDescription(producePostfix || context == FunctionCompletionContext.TupleBindingPostfix)}"
                    : function.Description,
                function.Deprecated,
                function.Replacement,
                function.Sunset,
                hasOpeningParenthesis || tupleBinding
                    ? null
                    : function.Parameters
                        .Where(parameter => !parameter.Optional || parameter.Variadic)
                        .Select(parameter => parameter.Name)
                        .ToArray());

        static string CreateTupleBindingDescription(bool postfix) => postfix
            ? "Postfix tuple binding uses the first tuple item as pipeline input and the remaining items as explicit arguments in order."
            : "Prefix tuple binding uses the last tuple item as pipeline input and the preceding items as explicit arguments in their original order.";
    }

    private static bool TryGetTypeLiteralCompletion(
        string text, int cursorOffset, out IReadOnlyList<CompletionSuggestion> completions)
    {
        var prefixStart = cursorOffset;
        while (prefixStart > 0 && IsTypeNameCharacter(text[prefixStart - 1]))
            prefixStart--;

        if (prefixStart == 0 || text[prefixStart - 1] != ':')
        {
            completions = [];
            return false;
        }

        var tokenEnd = cursorOffset;
        while (tokenEnd < text.Length && IsTypeNameCharacter(text[tokenEnd]))
            tokenEnd++;

        const string probeType = "numeric";
        var probeText = string.Concat(
            text.AsSpan(0, prefixStart),
            probeType,
            text.AsSpan(tokenEnd));
        if (!ProbeIsTypeLiteral(probeText, prefixStart - 1, probeType.Length + 1))
        {
            completions = [];
            return false;
        }

        var prefix = text[prefixStart..cursorOffset];
        completions = TypeRegistry.All
            .Where(type => type.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .Select(type => new CompletionSuggestion(
                type.Name,
                type.Name,
                true,
                prefixStart,
                tokenEnd - prefixStart,
                type.Summary,
                Kind: CompletionSuggestionKind.Type))
            .ToArray();
        return true;
    }

    private static FunctionCompletionContext GetFunctionContext(string probeText)
    {
        try
        {
            var syntax = ExpressifSyntax.Parse(probeText);
            var node = DescendantsAndSelf(syntax).FirstOrDefault(node => node switch
            {
                FunctionCallSyntax function => function.Name.Equals(ProbeName, StringComparison.Ordinal),
                TupleBindingShorthandSyntax shorthand => shorthand.Name.Equals(ProbeName, StringComparison.Ordinal),
                _ => false
            });
            return node switch
            {
                FunctionCallSyntax => FunctionCompletionContext.Function,
                TupleBindingShorthandSyntax { Direction: TupleBindingDirection.Prefix } =>
                    FunctionCompletionContext.TupleBindingPrefix,
                TupleBindingShorthandSyntax => FunctionCompletionContext.TupleBindingPostfix,
                _ => FunctionCompletionContext.None
            };
        }
        catch (ExpressifSyntaxException)
        {
            return FunctionCompletionContext.None;
        }
    }

    private static bool ProbeIsTypeLiteral(string probeText, int expectedStart, int expectedLength)
    {
        try
        {
            var syntax = ExpressifSyntax.Parse(probeText);
            return DescendantsAndSelf(syntax).Any(node => node is TypeLiteralSyntax type
                && type.Span.Start == expectedStart
                && type.Span.Length == expectedLength);
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

    private static bool IsTypeNameCharacter(char character)
        => char.IsAsciiLetterOrDigit(character) || character == '-';

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

    private enum FunctionCompletionContext
    {
        None,
        Function,
        TupleBindingPrefix,
        TupleBindingPostfix
    }
}
