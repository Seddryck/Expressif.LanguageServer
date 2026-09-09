using System.Globalization;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Diagnostics;

public sealed class LegacyTupleReferenceService : ILegacyTupleReferenceService
{
    public IReadOnlyList<LegacyTupleReference> GetReferences(RootExpressionSyntax syntaxTree)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        return DescendantsAndSelf(syntaxTree)
            .OfType<TupleProjectionSyntax>()
            .Where(reference => reference.Direction == TupleProjectionDirection.FromEnd &&
                reference.Text.StartsWith("$^", StringComparison.Ordinal))
            .Select(reference => new LegacyTupleReference(reference.Text,
                reference.Span.Start, reference.Span.Length,
                reference.Index < int.MaxValue
                    ? "$-" + (reference.Index + 1).ToString(CultureInfo.InvariantCulture)
                    : null))
            .ToArray();
    }

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        foreach (var descendant in DescendantsAndSelf(child))
            yield return descendant;
    }
}
