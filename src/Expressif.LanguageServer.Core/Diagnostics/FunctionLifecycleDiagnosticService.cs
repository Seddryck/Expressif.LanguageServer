using Expressif.LanguageServer.Core.Functions;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Diagnostics;

public sealed class FunctionLifecycleDiagnosticService(IFunctionCatalog functions)
    : IFunctionLifecycleDiagnosticService
{
    public IReadOnlyList<FunctionLifecycleDiagnostic> GetDiagnostics(RootExpressionSyntax syntaxTree)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);

        return DescendantsAndSelf(syntaxTree)
            .OfType<FunctionCallSyntax>()
            .Select(call => (Call: call, Metadata: FindFunction(call.Name)))
            .Where(match => match.Metadata?.Deprecated == true)
            .Select(match => new FunctionLifecycleDiagnostic(
                match.Call.Name,
                CreateMessage(match.Call.Name, match.Metadata!),
                match.Call.Span.Start,
                match.Call.Name.Length))
            .ToArray();
    }

    private FunctionMetadata? FindFunction(string name) => functions.Functions.FirstOrDefault(function =>
        function.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
        function.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase));

    private static string CreateMessage(string usedName, FunctionMetadata function)
    {
        var message = $"Function '{usedName}' is deprecated.";
        if (!string.IsNullOrWhiteSpace(function.Replacement))
            message += $" Use '{function.Replacement}' instead.";
        if (!string.IsNullOrWhiteSpace(function.Sunset))
            message += $" It sunsets in Expressif {function.Sunset}.";
        return message;
    }

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        foreach (var descendant in DescendantsAndSelf(child))
            yield return descendant;
    }
}
