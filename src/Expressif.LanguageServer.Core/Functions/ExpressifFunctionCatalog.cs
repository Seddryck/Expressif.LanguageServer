using Expressif.Accumulators.Introspection;
using Expressif.Functions.Introspection;
using Expressif.Predicates.Introspection;

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

        var functions = descriptions
            .Select(function => new FunctionMetadata(
                function.Name,
                function.Aliases.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                function.Parameters.Select(parameter => CreateParameterMetadata(
                    function.ImplementationType,
                    parameter)).ToArray(),
                function.Summary,
                function.Scope,
                function.Deprecated,
                function.Replacement,
                function.Sunset,
                HasSafeDirectReplacement(function)))
            .ToArray();

        var predicates = new PredicateIntrospector()
            .Describe()
            .Where(predicate => predicate.IsPublic)
            .Select(predicate => new FunctionMetadata(
                predicate.Name,
                predicate.Aliases.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                predicate.Parameters.Select(parameter => CreateParameterMetadata(
                    predicate.ImplementationType,
                    parameter)).ToArray(),
                predicate.Summary,
                predicate.Scope))
            .ToArray();

        var aggregations = new AccumulatorIntrospector()
            .Describe()
            .Where(aggregation => aggregation.IsPublic)
            .SelectMany(CreateAggregationMetadata)
            .ToArray();

        return functions
            .Concat(predicates)
            .Concat(aggregations)
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

        static IEnumerable<FunctionMetadata> CreateAggregationMetadata(AccumulatorInfo aggregation)
        {
            var parameters = aggregation.Parameters.Select(parameter => CreateParameterMetadata(
                aggregation.ImplementationType,
                parameter)).ToArray();
            var deprecatedAliases = aggregation.DeprecatedAliases
                .Select(alias => alias.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            yield return new FunctionMetadata(
                aggregation.Name,
                aggregation.Aliases
                    .Where(alias => !deprecatedAliases.Contains(alias))
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                parameters,
                aggregation.Summary,
                aggregation.Scope);

            foreach (var alias in aggregation.DeprecatedAliases)
                yield return new FunctionMetadata(
                    alias.Name,
                    [],
                    parameters,
                    aggregation.Summary,
                    aggregation.Scope,
                    Deprecated: true,
                    Replacement: alias.Replacement,
                    SafeDirectReplacement: true);
        }
    }

    private static FunctionParameterMetadata CreateParameterMetadata(
        Type implementationType,
        ParameterInfo parameter)
    {
        var optional = parameter.Optional || IsOmittedFromConstructorOverload(implementationType, parameter.Name);
        return new FunctionParameterMetadata(
            parameter.Name,
            optional,
            parameter.Summary,
            parameter.Variadic,
            optional ? 0 : parameter.MinimumCardinality);
    }

    private static bool IsOmittedFromConstructorOverload(Type implementationType, string parameterName)
    {
        var constructorParameterNames = implementationType
            .GetConstructors()
            .Select(constructor => constructor.GetParameters()
                .Select(parameter => parameter.Name)
                .ToHashSet(StringComparer.Ordinal))
            .ToArray();

        return constructorParameterNames.Any(names => names.Contains(parameterName)) &&
               constructorParameterNames.Any(names => !names.Contains(parameterName));
    }
}
