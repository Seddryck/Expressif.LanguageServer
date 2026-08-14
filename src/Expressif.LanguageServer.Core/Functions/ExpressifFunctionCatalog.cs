using Expressif.Functions.Introspection;

namespace Expressif.LanguageServer.Core.Functions;

public sealed class ExpressifFunctionCatalog : IFunctionCatalog
{
    public IReadOnlyList<FunctionMetadata> Functions { get; } = new FunctionIntrospector()
        .Describe()
        .Where(function => function.IsPublic)
        .Select(function => new FunctionMetadata(
            function.Name,
            function.Aliases.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            function.Parameters.Select(parameter => new FunctionParameterMetadata(
                parameter.Name, parameter.Optional, parameter.Summary)).ToArray(),
            function.Summary,
            function.Scope))
        .OrderBy(function => function.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
