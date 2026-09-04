using Expressif.Functions.Introspection;

namespace Expressif.LanguageServer.Core.Functions;

public sealed class ExpressifFunctionCatalog : IFunctionCatalog
{
    public IReadOnlyList<FunctionMetadata> Functions { get; } = CreateFunctions();

    private static IReadOnlyList<FunctionMetadata> CreateFunctions()
    {
        var descriptions = new FunctionIntrospector()
            .Describe()
            .Where(function => function.IsPublic)
            .ToArray();

        return descriptions
            .Select(function => new FunctionMetadata(
                function.Name,
                function.Aliases.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                function.Parameters.Select(parameter => new FunctionParameterMetadata(
                    parameter.Name,
                    parameter.Optional,
                    parameter.Summary,
                    parameter.Variadic,
                    parameter.MinimumCardinality)).ToArray(),
                function.Summary,
                function.Scope,
                function.Deprecated,
                function.Replacement,
                function.Sunset,
                HasSafeDirectReplacement(function)))
            .OrderBy(function => function.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        bool HasSafeDirectReplacement(FunctionInfo function)
        {
            if (!function.Deprecated || string.IsNullOrWhiteSpace(function.Replacement))
                return false;

            var replacement = descriptions.FirstOrDefault(candidate =>
                candidate.Name.Equals(function.Replacement, StringComparison.OrdinalIgnoreCase) ||
                candidate.Aliases.Contains(function.Replacement, StringComparer.OrdinalIgnoreCase));
            return replacement?.ImplementationType == function.ImplementationType;
        }
    }
}
