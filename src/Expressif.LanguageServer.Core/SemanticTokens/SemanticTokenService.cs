using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.SemanticTokens;

public sealed class SemanticTokenService : ISemanticTokenService
{
    public IReadOnlyList<SemanticTokenSpan> GetTokens(RootExpressionSyntax syntaxTree, string text)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        ArgumentNullException.ThrowIfNull(text);

        var tokens = new List<SemanticTokenSpan>();
        foreach (var node in DescendantsAndSelf(syntaxTree))
        {
            switch (node)
            {
                case VariableSyntax:
                case ConstantReferenceSyntax:
                case IncomingValueSyntax:
                case TupleProjectionSyntax:
                    Add(tokens, node.Span.Start, node.Span.Length, SemanticTokenKind.Variable, text.Length);
                    break;
                case FunctionCallSyntax function:
                    AddNamedToken(tokens, function.Span, function.Text, function.Name,
                        SemanticTokenKind.Function, text.Length);
                    break;
                case RecordAccessSyntax access:
                    AddRecordFields(tokens, access, text.Length);
                    break;
                case QuotedLiteralSyntax:
                    Add(tokens, node.Span.Start, node.Span.Length, SemanticTokenKind.String, text.Length);
                    break;
                case NumericLiteralSyntax:
                    Add(tokens, node.Span.Start, node.Span.Length, SemanticTokenKind.Number, text.Length);
                    break;
                case UnaryOperatorSyntax:
                case BinaryOperatorSyntax:
                    Add(tokens, node.Span.Start, node.Span.Length, SemanticTokenKind.Operator, text.Length);
                    break;
                case OpenExpressionSyntax open:
                    if (open.Source is not null)
                        AddPipelineOperators(tokens, text, open.Source, open.Pipeline);
                    break;
                case ClosedExpressionSyntax closed:
                    if (closed.Value is not null)
                        AddPipelineOperators(tokens, text, closed.Value, closed.Pipeline);
                    break;
                case ParameterizedExpressionSyntax parameterized:
                    var relativeOperator = parameterized.Text.IndexOf("|>", StringComparison.Ordinal);
                    if (relativeOperator >= 0)
                        Add(tokens, parameterized.Span.Start + relativeOperator, 2,
                            SemanticTokenKind.Operator, text.Length);
                    break;
                case MapShorthandSyntax shorthand when shorthand.Text.StartsWith("|>", StringComparison.Ordinal):
                    Add(tokens, shorthand.Span.Start, 2, SemanticTokenKind.Operator, text.Length);
                    break;
            }
        }

        return tokens
            .Where(token => token.Length > 0)
            .OrderBy(token => token.Start)
            .ThenBy(token => token.Length)
            .Aggregate(new List<SemanticTokenSpan>(), (result, token) =>
            {
                if (result.Count == 0 || token.Start >= result[^1].Start + result[^1].Length)
                    result.Add(token);
                return result;
            });
    }

    private static void AddRecordFields(ICollection<SemanticTokenSpan> tokens, RecordAccessSyntax access,
        int textLength)
    {
        var searchStart = 0;
        foreach (var field in access.Fields.Where(field => field.IsNamed))
        {
            if (field.Name is null)
                continue;
            var index = access.Text.IndexOf(field.Name, searchStart, StringComparison.Ordinal);
            if (index < 0)
                continue;

            Add(tokens, access.Span.Start + index, field.Name.Length, SemanticTokenKind.Property, textLength);
            searchStart = index + field.Name.Length;
        }
    }

    private static void AddNamedToken(ICollection<SemanticTokenSpan> tokens, SourceSpan span, string nodeText,
        string name, SemanticTokenKind kind, int textLength)
    {
        var relativeStart = nodeText.IndexOf(name, StringComparison.Ordinal);
        if (relativeStart >= 0)
            Add(tokens, span.Start + relativeStart, name.Length, kind, textLength);
    }

    private static void AddPipelineOperators(ICollection<SemanticTokenSpan> tokens, string text,
        SyntaxNode source, IReadOnlyList<ExpressionSyntax> pipeline)
    {
        foreach (var expression in pipeline)
        {
            var index = Math.Min(expression.Span.Start, text.Length) - 1;
            while (index >= source.Span.Start && char.IsWhiteSpace(text[index]))
                index--;

            if (index > source.Span.Start && text[index] == '>' && text[index - 1] == '|')
                Add(tokens, index - 1, 2, SemanticTokenKind.Operator, text.Length);
            else if (index >= source.Span.Start && text[index] == '|')
                Add(tokens, index, 1, SemanticTokenKind.Operator, text.Length);
        }
    }

    private static void Add(ICollection<SemanticTokenSpan> tokens, int start, int length,
        SemanticTokenKind kind, int textLength)
    {
        if (start >= 0 && length > 0 && start <= textLength - length)
            tokens.Add(new(start, length, kind));
    }

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        foreach (var descendant in DescendantsAndSelf(child))
            yield return descendant;
    }
}
