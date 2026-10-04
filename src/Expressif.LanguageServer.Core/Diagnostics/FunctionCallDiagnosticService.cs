using Expressif.LanguageServer.Core.Functions;
using Expressif.LanguageServer.Core.Syntax;
using Expressif.Functions;
using Expressif.Semantics;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Diagnostics;

public sealed class FunctionCallDiagnosticService(IFunctionCatalog functions)
    : IFunctionCallDiagnosticService
{
    private readonly TupleBindingAnalyzer tupleBindings = new(new CatalogTypeMapper(functions));

    public IReadOnlyList<FunctionCallDiagnostic> GetDiagnostics(RootExpressionSyntax syntaxTree)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);

        return DescendantsAndSelf(syntaxTree)
            .OfType<FunctionCallSyntax>()
            .SelectMany(GetDiagnostics)
            .Concat(GetTupleBindingDiagnostics(syntaxTree))
            .ToArray();
    }

    private IEnumerable<FunctionCallDiagnostic> GetTupleBindingDiagnostics(RootExpressionSyntax syntaxTree)
    {
        foreach (var use in tupleBindings.Analyze(syntaxTree).Where(use => use.Failure is not null))
        {
            var range = GetTupleBindingTargetRange(syntaxTree, use);
            yield return new(
                use.Message ?? $"Invalid tuple binding target '{use.Name}'.",
                range.Start,
                range.Length);
        }
    }

    private static SourceSpan GetTupleBindingTargetRange(RootExpressionSyntax syntaxTree, TupleBindingUse use)
    {
        if (use.Span is not { } operationSpan)
            return new SourceSpan(0, 0);

        var shorthand = CallableSyntaxReference.DescendantsOf(syntaxTree)
            .FirstOrDefault(reference => reference.TupleBindingDirection is not null &&
                                         reference.Span == operationSpan);
        if (shorthand is not null)
            return shorthand.NameSpan;

        var explicitBind = DescendantsAndSelf(syntaxTree)
            .OfType<FunctionCallSyntax>()
            .FirstOrDefault(call => call.Span == operationSpan &&
                                    call.Name.Equals("bind", StringComparison.OrdinalIgnoreCase));
        var argument = explicitBind?.Arguments.FirstOrDefault();
        if (argument is not null && use.Name is not null)
        {
            var relativeStart = argument.Text.IndexOf(use.Name, StringComparison.OrdinalIgnoreCase);
            if (relativeStart >= 0)
                return new SourceSpan(argument.Span.Start + relativeStart, use.Name.Length);
        }

        return operationSpan;
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

        var minimumArgumentCount = function.Parameters
            .Where(parameter => !parameter.Optional)
            .Sum(parameter => parameter.MinimumCardinality);
        if (call.Arguments.Count < minimumArgumentCount)
        {
            yield return new(
                $"Function '{call.Name}' requires at least {FormatArguments(minimumArgumentCount)}, " +
                $"but {FormatProvidedArguments(call.Arguments.Count)} provided.",
                call.Span.Start,
                call.Span.Length);
            yield break;
        }

        if (function.Parameters.Any(parameter => parameter.Variadic) ||
            call.Arguments.Count <= function.Parameters.Count)
            yield break;

        yield return new(
            $"Function '{call.Name}' accepts at most {FormatArguments(function.Parameters.Count)}, " +
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

    private sealed class CatalogTypeMapper(IFunctionCatalog catalog) : BaseTypeMapper
    {
        protected override IDictionary<string, Type> Initialize()
        {
            var mapping = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var function in catalog.Functions.Where(function => function.ImplementationType is not null))
            {
                foreach (var name in function.Aliases.Prepend(function.Name))
                    mapping.TryAdd(name, function.ImplementationType!);
            }

            return mapping;
        }
    }
}
