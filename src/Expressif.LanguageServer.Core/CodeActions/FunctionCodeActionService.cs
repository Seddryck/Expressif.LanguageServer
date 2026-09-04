using Expressif.LanguageServer.Core.Functions;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.CodeActions;

public sealed class FunctionCodeActionService(IFunctionCatalog functions) : IFunctionCodeActionService
{
    public IReadOnlyList<FunctionReplacement> GetReplacements(
        RootExpressionSyntax syntaxTree, int selectionStart, int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        if (selectionStart < 0 || selectionLength < 0 || selectionStart + selectionLength > syntaxTree.Text.Length)
            throw new ArgumentOutOfRangeException(nameof(selectionStart));

        var selectionEnd = selectionStart + selectionLength;
        return DescendantsAndSelf(syntaxTree)
            .OfType<FunctionCallSyntax>()
            .Where(call => selectionLength == 0
                ? selectionStart >= call.Span.Start && selectionStart <= call.Span.Start + call.Name.Length
                : selectionStart < call.Span.Start + call.Name.Length && selectionEnd > call.Span.Start)
            .Select(call => (Call: call, Metadata: FindFunction(call.Name)))
            .Where(match => match.Metadata is
            {
                Deprecated: true,
                SafeDirectReplacement: true,
                Replacement: not null
            })
            .Select(match => new FunctionReplacement(
                match.Call.Name,
                match.Metadata!.Replacement!,
                match.Call.Span.Start,
                match.Call.Name.Length))
            .ToArray();
    }

    private FunctionMetadata? FindFunction(string name) => functions.Functions.FirstOrDefault(function =>
        function.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
        function.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase));

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        foreach (var descendant in DescendantsAndSelf(child))
            yield return descendant;
    }
}
