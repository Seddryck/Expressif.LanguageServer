using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Syntax;

internal sealed record CallableSyntaxReference(string Name, SourceSpan NameSpan)
{
    public static IEnumerable<CallableSyntaxReference> DescendantsOf(SyntaxNode node)
    {
        foreach (var descendant in DescendantsAndSelf(node))
        {
            if (descendant is FunctionCallSyntax call)
                yield return new(call.Name, new SourceSpan(call.Span.Start, call.Name.Length));
            else if (descendant is TupleBindingShorthandSyntax shorthand)
                yield return new(shorthand.Name, shorthand.NameSpan);
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
