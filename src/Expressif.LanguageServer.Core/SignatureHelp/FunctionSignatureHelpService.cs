using Expressif.LanguageServer.Core.Functions;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.SignatureHelp;

public sealed class FunctionSignatureHelpService(IFunctionCatalog functions) : IFunctionSignatureHelpService
{
    public FunctionSignatureHelp? GetSignatureHelp(RootExpressionSyntax syntaxTree, int cursorOffset)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        if (cursorOffset < 0 || cursorOffset > syntaxTree.Text.Length)
            throw new ArgumentOutOfRangeException(nameof(cursorOffset));

        var call = DescendantsAndSelf(syntaxTree)
            .OfType<FunctionCallSyntax>()
            .Where(function => IsInsideArgumentList(function, cursorOffset))
            .OrderBy(function => function.Span.Length)
            .FirstOrDefault();
        if (call is null)
            return null;

        var metadata = functions.Functions.FirstOrDefault(function =>
            function.Name.Equals(call.Name, StringComparison.OrdinalIgnoreCase) ||
            function.Aliases.Contains(call.Name, StringComparer.OrdinalIgnoreCase));
        if (metadata is null)
            return null;

        var parameters = metadata.Parameters
            .Select(parameter => new SignatureParameter(
                parameter.Label,
                parameter.Description))
            .ToArray();
        var activeArgument = GetActiveArgument(call, cursorOffset);
        int? activeParameter = parameters.Length == 0
            ? null
            : GetActiveParameter(metadata.Parameters, activeArgument, call.Arguments.Count);

        return new FunctionSignatureHelp(
            $"{metadata.Name}({string.Join(", ", parameters.Select(parameter => parameter.Label))})",
            metadata.Description,
            parameters,
            activeParameter);
    }

    private static int GetActiveParameter(
        IReadOnlyList<FunctionParameterMetadata> parameters,
        int activeArgument,
        int argumentCount)
    {
        var variadicIndex = parameters
            .Select((parameter, index) => (parameter, index))
            .Where(item => item.parameter.Variadic)
            .Select(item => item.index)
            .DefaultIfEmpty(-1)
            .Single();
        if (variadicIndex < 0 || activeArgument < variadicIndex)
            return Math.Min(activeArgument, parameters.Count - 1);

        var trailingParameterCount = parameters.Count - variadicIndex - 1;
        var firstTrailingArgument = Math.Max(
            variadicIndex + parameters[variadicIndex].MinimumCardinality,
            argumentCount - trailingParameterCount);
        return activeArgument < firstTrailingArgument
            ? variadicIndex
            : Math.Min(variadicIndex + 1 + activeArgument - firstTrailingArgument, parameters.Count - 1);
    }

    private static bool IsInsideArgumentList(FunctionCallSyntax call, int cursorOffset)
    {
        if (!call.HasParentheses)
            return false;

        var openingParenthesis = call.Span.Start + call.Text.IndexOf('(');
        return cursorOffset > openingParenthesis && cursorOffset < call.Span.End;
    }

    private static int GetActiveArgument(FunctionCallSyntax call, int cursorOffset)
    {
        for (var index = 0; index < call.Arguments.Count; index++)
        {
            if (cursorOffset <= call.Arguments[index].Span.End)
                return index;
        }

        return call.Arguments.Count;
    }

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in DescendantsAndSelf(child))
                yield return descendant;
    }
}
