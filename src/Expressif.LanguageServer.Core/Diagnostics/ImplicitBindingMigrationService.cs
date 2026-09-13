using Expressif.Semantics;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Diagnostics;

public sealed class ImplicitBindingMigrationService : IImplicitBindingMigrationService
{
    private readonly LegacyTupleBindingAnalyzer analyzer = new();

    public IReadOnlyList<ImplicitBindingMigration> GetMigrations(RootExpressionSyntax syntaxTree)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);

        return analyzer.Analyze(syntaxTree)
            .Where(use => use.Span is not null)
            .Select(use => new ImplicitBindingMigration(
                use.Operator,
                use.Callable,
                use.Code,
                use.Message,
                use.Span!.Value.Start,
                use.Span.Value.Length,
                CreateReplacements(use)))
            .ToArray();
    }

    private static IReadOnlyList<ImplicitBindingReplacement> CreateReplacements(LegacyTupleBindingUse use)
    {
        if (!use.CanRewrite)
            return [];

        var replacements = new List<ImplicitBindingReplacement>
        {
            new($"Use explicit tuple binding '{use.Replacement}'", use.Replacement, true)
        };
        if (use.Operator is "adjacent" or "chunk-while")
        {
            replacements.Add(new(
                $"Rewrite '{use.Callable}' with explicit tuple arguments",
                $"$1 | {use.Callable}($0)",
                false));
        }

        return replacements;
    }
}
