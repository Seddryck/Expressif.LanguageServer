using Expressif.LanguageServer.Core.Documents;
using Expressif.Semantics;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Scopes;

public sealed class FieldScopeService : IFieldScopeService
{
    public FieldScope? GetScope(DocumentSnapshot document, int cursorOffset)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (cursorOffset < 0 || cursorOffset >= document.Text.Length ||
            document.SyntaxTree is null || document.SyntaxErrors.Count != 0)
            return null;

        var nodes = DescendantsAndSelf(document.SyntaxTree).ToArray();
        var parents = GetParents(document.SyntaxTree);
        if (GetInputBindingScope(nodes, parents, cursorOffset) is { } bindingScope)
            return bindingScope;

        // Analyze this immutable snapshot; no independent cache can outlive an edit.
        var analysis = new SemanticAnalyzer().Analyze(document.SyntaxTree);
        var reference = analysis.References.FirstOrDefault(reference =>
            cursorOffset >= reference.Span.Start && cursorOffset < reference.Span.End);
        if (reference is null)
            return null;

        if (reference.Source.Kind == SemanticSourceKind.Unresolved)
            return GetInputBoundFieldScope(reference, parents);

        var source = reference.Source;
        var description = reference.Kind switch
        {
            FieldReferenceKind.ExpressionRoot => "Field of the current expression's root input.",
            FieldReferenceKind.EnclosingExpressionRoot => "Field of the enclosing expression's input.",
            _ => "Field of the input supplying this field operation."
        };
        if (source.Kind == SemanticSourceKind.Element)
            description += " The input is each element of the supplying collection.";
        var supplier = GetSupplier(source);
        if (IsExternal(source))
            description += " The value comes from external input; there is no in-document source to highlight.";
        else if (supplier is null)
            description += " No in-document source region is available.";
        else
            description += " The highlighted region supplies the input; it is not a field declaration.";
        return new(reference.Span, supplier, description);
    }

    private static FieldScope? GetInputBindingScope(
        IReadOnlyList<SyntaxNode> nodes,
        IReadOnlyDictionary<SyntaxNode, SyntaxNode> parents,
        int cursorOffset)
    {
        var declaration = nodes.OfType<BindingNameSyntax>()
            .FirstOrDefault(name => Contains(name.Span, cursorOffset));
        if (declaration is not null)
        {
            var binding = FindContainingBinding(declaration, parents);
            if (binding is null)
                return null;
            return CreateBindingScope(declaration.Span, binding, parents,
                $"Declares '@{declaration.Name}' as an input-bound reference.");
        }

        var variable = nodes.OfType<VariableSyntax>()
            .FirstOrDefault(candidate => Contains(candidate.Span, cursorOffset));
        if (variable is null)
            return null;

        var referencedBinding = nodes.OfType<InputBindingExpressionSyntax>()
            .Where(binding => Contains(binding.Body.Span, variable.Span.Start))
            .Where(binding => GetBindingNames(binding).Contains(variable.Name, StringComparer.Ordinal))
            .OrderBy(binding => binding.Span.Length)
            .FirstOrDefault();
        return referencedBinding is null
            ? null
            : CreateBindingScope(variable.Span, referencedBinding, parents,
                $"Reference to the input bound as '@{variable.Name}'.");
    }

    private static FieldScope CreateBindingScope(
        SourceSpan reference,
        InputBindingExpressionSyntax binding,
        IReadOnlyDictionary<SyntaxNode, SyntaxNode> parents,
        string description)
    {
        var supplier = GetBindingSupplier(binding, parents);
        description += supplier is null
            ? " The value comes from external input; there is no in-document source to highlight."
            : " The highlighted region supplies the bound input.";
        return new(reference, supplier, description);
    }

    private static FieldScope? GetInputBoundFieldScope(
        FieldReference reference,
        IReadOnlyDictionary<SyntaxNode, SyntaxNode> parents)
    {
        if (!IsInsideInputBinding(reference.Syntax, parents))
            return null;

        SourceSpan? supplier;
        if (reference.SelectionIndex > 0)
            supplier = new SourceSpan(reference.Syntax.Span.Start,
                reference.Span.Start - reference.Syntax.Span.Start);
        else if (!TryGetInputBoundPipelineSupplier(reference.Syntax, parents, out supplier))
            return null;

        var description = "Field of an input-bound expression.";
        description += supplier is null
            ? " The value comes from external input; there is no in-document source to highlight."
            : " The highlighted region supplies the input; it is not a field declaration.";
        return new(reference.Span, supplier, description);
    }

    private static bool TryGetInputBoundPipelineSupplier(
        RecordAccessSyntax access,
        IReadOnlyDictionary<SyntaxNode, SyntaxNode> parents,
        out SourceSpan? supplier)
    {
        if (!parents.TryGetValue(access, out var parent) || parent is not RootExpressionSyntax root)
        {
            supplier = null;
            return false;
        }

        supplier = GetPreviousStage(root, access);
        if (supplier is not null)
            return IsInsideInputBinding(root, parents);
        if (!parents.TryGetValue(root, out parent) ||
            parent is not InputBindingExpressionSyntax binding || !ReferenceEquals(binding.Body, root))
            return false;
        supplier = GetBindingSupplier(binding, parents);
        return true;
    }

    private static bool IsInsideInputBinding(
        SyntaxNode node,
        IReadOnlyDictionary<SyntaxNode, SyntaxNode> parents)
    {
        var current = node;
        while (parents.TryGetValue(current, out var parent))
        {
            if (parent is InputBindingExpressionSyntax binding && Contains(binding.Body.Span, node.Span.Start))
                return true;
            current = parent;
        }
        return false;
    }

    private static SourceSpan? GetBindingSupplier(
        InputBindingExpressionSyntax binding,
        IReadOnlyDictionary<SyntaxNode, SyntaxNode> parents)
        => parents.TryGetValue(binding, out var parent) && parent is RootExpressionSyntax root
            ? GetPreviousStage(root, binding)
            : null;

    private static SourceSpan? GetPreviousStage(RootExpressionSyntax root, ExpressionSyntax expression)
    {
        SyntaxNode? previous = root switch
        {
            ClosedExpressionSyntax closed when ReferenceEquals(closed.Value, expression) => null,
            ClosedExpressionSyntax closed => Previous(closed.Value, closed.Pipeline, expression),
            OpenExpressionSyntax open when ReferenceEquals(open.Source, expression) => null,
            OpenExpressionSyntax open => Previous(open.Source, open.Pipeline, expression),
            _ => null
        };
        return previous?.Span;
    }

    private static SyntaxNode? Previous(
        SyntaxNode? source,
        IReadOnlyList<ExpressionSyntax> pipeline,
        ExpressionSyntax expression)
    {
        var index = pipeline
            .Select((candidate, index) => (candidate, index))
            .Where(item => ReferenceEquals(item.candidate, expression))
            .Select(item => item.index)
            .DefaultIfEmpty(-1)
            .First();
        if (index < 0)
            return null;
        return index == 0 ? source : pipeline[index - 1];
    }

    private static InputBindingExpressionSyntax? FindContainingBinding(
        BindingNameSyntax declaration,
        IReadOnlyDictionary<SyntaxNode, SyntaxNode> parents)
    {
        SyntaxNode current = declaration;
        while (parents.TryGetValue(current, out var parent))
        {
            if (parent is InputBindingExpressionSyntax binding)
                return binding;
            current = parent;
        }
        return null;
    }

    private static IEnumerable<string> GetBindingNames(InputBindingExpressionSyntax binding)
        => binding.Binding switch
        {
            BindingNameSyntax name => [name.Name],
            PositionalBindingPatternSyntax positional => positional.Names.Select(name => name.Name),
            _ => []
        };

    private static Dictionary<SyntaxNode, SyntaxNode> GetParents(SyntaxNode root)
    {
        var parents = new Dictionary<SyntaxNode, SyntaxNode>(ReferenceEqualityComparer.Instance);
        AddChildren(root);
        return parents;

        void AddChildren(SyntaxNode parent)
        {
            foreach (var child in parent.Children)
            {
                parents[child] = parent;
                AddChildren(child);
            }
        }
    }

    private static bool Contains(SourceSpan span, int offset)
        => offset >= span.Start && offset < span.End;

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        foreach (var descendant in DescendantsAndSelf(child))
            yield return descendant;
    }

    private static SourceSpan? GetSupplier(SemanticSource source)
    {
        // Element sources may identify a collection through Input rather than Span.
        if (source.Kind is SemanticSourceKind.ExternalInput or SemanticSourceKind.Unresolved)
            return null;
        return source.Span ?? (source.Kind == SemanticSourceKind.Element && source.Input is { } input
            ? GetSupplier(input) : null);
    }

    private static bool IsExternal(SemanticSource source)
        => source.Kind == SemanticSourceKind.ExternalInput ||
           source.Kind == SemanticSourceKind.Element && source.Input is { } input && IsExternal(input);
}
