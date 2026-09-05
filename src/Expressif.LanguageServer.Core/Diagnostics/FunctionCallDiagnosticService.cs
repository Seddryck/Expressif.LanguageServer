using Expressif.LanguageServer.Core.Functions;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Diagnostics;

public sealed class FunctionCallDiagnosticService(IFunctionCatalog functions)
    : IFunctionCallDiagnosticService
{
    public IReadOnlyList<FunctionCallDiagnostic> GetDiagnostics(RootExpressionSyntax syntaxTree)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);

        return DescendantsAndSelf(syntaxTree)
            .OfType<FunctionCallSyntax>()
            .SelectMany(GetDiagnostics)
            .ToArray();
    }

    private IEnumerable<FunctionCallDiagnostic> GetDiagnostics(FunctionCallSyntax call)
    {
        var function = FindFunction(call.Name);
        if (function is null)
        {
            yield return new(
                $"Unknown function '{call.Name}'.",
                call.Span.Start,
                call.Name.Length);
            yield break;
        }

        var minimumArgumentCount = function.Parameters.Sum(parameter => parameter.MinimumCardinality);
        if (call.Arguments.Count >= minimumArgumentCount)
            yield break;

        yield return new(
            $"Function '{call.Name}' requires at least {FormatArguments(minimumArgumentCount)}, " +
            $"but {FormatProvidedArguments(call.Arguments.Count)} provided.",
            call.Span.Start,
            call.Span.Length);
    }

    private FunctionMetadata? FindFunction(string name) => functions.Functions.FirstOrDefault(function =>
        function.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
        function.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase));

    private static string FormatArguments(int count) => $"{count} argument{(count == 1 ? string.Empty : "s")}";

    private static string FormatProvidedArguments(int count)
        => $"{FormatArguments(count)} {(count == 1 ? "was" : "were")}";

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        foreach (var descendant in DescendantsAndSelf(child))
            yield return descendant;
    }
}
