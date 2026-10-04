using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Syntax;

internal sealed record CallableSyntaxReference(
    string Name,
    SourceSpan NameSpan,
    SourceSpan Span,
    TupleBindingDirection? TupleBindingDirection = null)
{
    public static IEnumerable<CallableSyntaxReference> DescendantsOf(SyntaxNode node)
    {
        foreach (var descendant in DescendantsAndSelf(node))
        {
            if (descendant is FunctionCallSyntax call)
                yield return new(call.Name, new SourceSpan(call.Span.Start, call.Name.Length), call.Span);
            else if (descendant is TupleBindingShorthandSyntax shorthand)
                yield return new(shorthand.Name, shorthand.NameSpan, shorthand.Span, shorthand.Direction);
        }
    }

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        foreach (var descendant in DescendantsAndSelf(child))
            yield return descendant;
    }
}
