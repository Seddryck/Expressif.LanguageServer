using Expressif.LanguageServer.Core.Diagnostics;
using Expressif.LanguageServer.Core.Functions;
using Expressif.LanguageServer.Core.Syntax;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Hover;

public sealed class FunctionHoverService(
    IFunctionCatalog functions,
    IImplicitBindingMigrationService? implicitBindings = null) : IFunctionHoverService
{
    private readonly IImplicitBindingMigrationService implicitBindings =
        implicitBindings ?? new ImplicitBindingMigrationService();

    public FunctionHover? GetHover(RootExpressionSyntax syntaxTree, int cursorOffset)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        if (cursorOffset < 0 || cursorOffset > syntaxTree.Text.Length)
            throw new ArgumentOutOfRangeException(nameof(cursorOffset));

        var call = CallableSyntaxReference.DescendantsOf(syntaxTree)
            .Where(function => cursorOffset >= function.NameSpan.Start &&
                               cursorOffset < function.NameSpan.End)
            .OrderBy(function => function.NameSpan.Length)
            .FirstOrDefault();
        if (call is null)
            return null;

        var metadata = functions.Functions.FirstOrDefault(function =>
            function.Name.Equals(call.Name, StringComparison.OrdinalIgnoreCase) ||
            function.Aliases.Contains(call.Name, StringComparer.OrdinalIgnoreCase));
        if (metadata is null)
            return null;

        var parameters = string.Join(", ", metadata.Parameters.Select(parameter => parameter.Label));
        var implicitBinding = implicitBindings.GetMigrations(syntaxTree)
            .FirstOrDefault(migration => cursorOffset >= migration.Start &&
                                         cursorOffset < migration.Start + migration.Length);
        return new FunctionHover(
            $"{metadata.Name}({parameters})",
            metadata.Description,
            call.NameSpan.Start,
            call.NameSpan.Length,
            implicitBinding is null
                ? CreateLifecycleNotice(metadata)
                : $"Deprecated usage. {implicitBinding.Message}");
    }

    private static string? CreateLifecycleNotice(FunctionMetadata function)
    {
        if (!function.Deprecated)
            return null;

        var notice = "Deprecated.";
        if (!string.IsNullOrWhiteSpace(function.Replacement))
            notice += $" Use {function.Replacement} instead.";
        if (!string.IsNullOrWhiteSpace(function.Sunset))
            notice += $"\nSunset: Expressif {function.Sunset}.";
        return notice;
    }
}
