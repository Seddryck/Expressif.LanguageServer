using Expressif.LanguageServer.Core.Functions;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Hover;

public sealed class FunctionHoverService(IFunctionCatalog functions) : IFunctionHoverService
{
    public FunctionHover? GetHover(RootExpressionSyntax syntaxTree, int cursorOffset)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        if (cursorOffset < 0 || cursorOffset > syntaxTree.Text.Length)
            throw new ArgumentOutOfRangeException(nameof(cursorOffset));

        var call = DescendantsAndSelf(syntaxTree)
            .OfType<FunctionCallSyntax>()
            .Where(function => cursorOffset >= function.Span.Start &&
                               cursorOffset < function.Span.Start + function.Name.Length)
            .OrderBy(function => function.Span.Length)
            .FirstOrDefault();
        if (call is null)
            return null;

        var metadata = functions.Functions.FirstOrDefault(function =>
            function.Name.Equals(call.Name, StringComparison.OrdinalIgnoreCase) ||
            function.Aliases.Contains(call.Name, StringComparer.OrdinalIgnoreCase));
        if (metadata is null)
            return null;

        var parameters = string.Join(", ", metadata.Parameters.Select(parameter => parameter.Label));
        return new FunctionHover(
            $"{metadata.Name}({parameters})",
            metadata.Description,
            call.Span.Start,
            call.Name.Length);
    }

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        foreach (var descendant in DescendantsAndSelf(child))
            yield return descendant;
    }
}
