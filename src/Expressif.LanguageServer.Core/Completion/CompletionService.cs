using Expressif.LanguageServer.Core.Functions;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Completion;

public sealed class CompletionService(IFunctionCatalog functions) : ICompletionService
{
    private const string ProbeName = "expressif-completion-probe";
    private const string FieldProbeName = "expressif_field_completion_probe";

    public IReadOnlyList<CompletionSuggestion> GetCompletions(string text, int cursorOffset)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (cursorOffset < 0 || cursorOffset > text.Length)
            throw new ArgumentOutOfRangeException(nameof(cursorOffset));

        var fieldCompletions = GetRecordFieldCompletions(text, cursorOffset);
        if (fieldCompletions is not null)
            return fieldCompletions;

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

    private static IReadOnlyList<CompletionSuggestion>? GetRecordFieldCompletions(string text, int cursorOffset)
    {
        var prefixStart = cursorOffset;
        while (prefixStart > 0 && IsFieldNameCharacter(text[prefixStart - 1]))
            prefixStart--;

        if (prefixStart == 0 || text[prefixStart - 1] != '.')
            return null;

        var tokenEnd = cursorOffset;
        while (tokenEnd < text.Length && IsFieldNameCharacter(text[tokenEnd]))
            tokenEnd++;

        var prefix = text[prefixStart..cursorOffset];
        var probeText = CloseOpenParentheses(string.Concat(
            text.AsSpan(0, prefixStart),
            FieldProbeName,
            text.AsSpan(tokenEnd)));

        RootExpressionSyntax syntax;
        try
        {
            syntax = ExpressifSyntax.Parse(probeText);
        }
        catch (ExpressifSyntaxException)
        {
            return [];
        }

        var access = DescendantsAndSelf(syntax)
            .OfType<RecordAccessSyntax>()
            .FirstOrDefault(candidate => candidate.Fields.Any(field =>
                field.IsNamed && string.Equals(field.Name, FieldProbeName, StringComparison.Ordinal)));
        if (access is null)
            return [];

        var record = DescendantsAndSelf(syntax)
            .OfType<FunctionCallSyntax>()
            .Where(call => call.Span.Start < access.Span.Start
                && call.Name.Equals("record", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(call => call.Span.Start)
            .FirstOrDefault();
        var fieldNames = record is not null
            ? record.Arguments.OfType<NamedArgumentSyntax>().Select(argument => argument.Name.Value)
            : GetLiteralRecordFieldNames(syntax, access);
        if (fieldNames is null)
            return [];

        return fieldNames
            .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => CreateFieldSuggestion(name, prefixStart, tokenEnd))
            .ToArray();
    }

    private static IEnumerable<string>? GetLiteralRecordFieldNames(SyntaxNode syntax, RecordAccessSyntax access)
    {
        var candidates = DescendantsAndSelf(syntax)
            .OfType<RecordLiteralSyntax>()
            .Where(record => record.Span.Start < access.Span.Start)
            .ToArray();
        var records = candidates
            .Where(record => !candidates.Any(other => !ReferenceEquals(other, record)
                && other.Span.Start <= record.Span.Start
                && other.Span.End >= record.Span.End))
            .ToArray();
        if (records.Length == 0)
            return null;

        var commonNames = records[0].Fields
            .Select(field => field.Name.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records.Skip(1))
            commonNames.IntersectWith(record.Fields.Select(field => field.Name.Value));

        return commonNames;
    }

    private static CompletionSuggestion CreateFieldSuggestion(string name, int prefixStart, int tokenEnd)
    {
        var supportsShorthand = IsShorthandFieldName(name);
        return new(
            name,
            supportsShorthand ? name : $"field(\"{EscapeQuotedText(name)}\")",
            true,
            supportsShorthand ? prefixStart : prefixStart - 1,
            tokenEnd - prefixStart + (supportsShorthand ? 0 : 1),
            "Record field",
            Kind: CompletionSuggestionKind.Field);
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

    private static bool IsFieldNameCharacter(char character)
        => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '+';

    private static bool IsShorthandFieldName(string name)
        => name.Length > 0
            && (char.IsAsciiLetter(name[0]) || name[0] == '_')
            && name.Skip(1).All(IsFieldNameCharacter);

    private static string EscapeQuotedText(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);

    private static string CloseOpenParentheses(string text)
    {
        var depth = 0;
        var quote = '\0';
        var escaped = false;
        foreach (var character in text)
        {
            if (quote != '\0')
            {
                if (escaped)
                    escaped = false;
                else if (character == '\\')
                    escaped = true;
                else if (character == quote)
                    quote = '\0';
                continue;
            }

            if (character is '\'' or '"')
                quote = character;
            else if (character == '(')
                depth++;
            else if (character == ')' && depth > 0)
                depth--;
        }

        return depth == 0 ? text : text + new string(')', depth);
    }

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
