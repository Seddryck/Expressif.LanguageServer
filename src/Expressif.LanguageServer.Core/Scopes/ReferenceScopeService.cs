using Expressif.Functions;
using Expressif.Functions.Array;
using Expressif.LanguageServer.Core.Documents;
using Expressif.Semantics;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Scopes;

public sealed class ReferenceScopeService : IReferenceScopeService
{
    public ReferenceScope? GetScope(DocumentSnapshot document, int cursorOffset)
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
        var field = analysis.References.FirstOrDefault(reference => Contains(reference.Span, cursorOffset));
        if (field is not null)
            return GetFieldScope(field, parents);

        var tuple = nodes.OfType<TupleProjectionSyntax>()
            .FirstOrDefault(reference => Contains(reference.Span, cursorOffset));
        return tuple is null || analysis.Diagnostics.Count != 0
            ? null
            : GetTupleScope(tuple, parents);
    }

    private static ReferenceScope? GetFieldScope(
        FieldReference reference,
        IReadOnlyDictionary<SyntaxNode, SyntaxNode> parents)
    {
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

    private static ReferenceScope GetTupleScope(
        TupleProjectionSyntax reference,
        IReadOnlyDictionary<SyntaxNode, SyntaxNode> parents)
    {
        var supplier = reference.Direction == TupleProjectionDirection.FromStart && reference.RootDepth == 0
            ? GetTupleElementSupplier(reference, parents)
            : null;
        var description = $"Tuple element at zero-based position {reference.Index}.";
        description += supplier is null
            ? " No in-document tuple element is available to highlight."
            : " The highlighted region is the referenced tuple element.";
        return new(reference.Span, supplier, description);
    }

    private static SourceSpan? GetTupleElementSupplier(
        TupleProjectionSyntax reference,
        IReadOnlyDictionary<SyntaxNode, SyntaxNode> parents)
    {
        if (!TryGetContainingStage(reference, parents, out var root, out var stage) ||
            IsInElementScope(reference, stage, parents))
            return null;

        var source = GetPreviousStageNode(root, stage);
        if (source is not TupleLiteralSyntax tuple || reference.Index < 0 || reference.Index >= tuple.Elements.Count)
            return null;

        // A spread before the selected position makes the runtime position impossible to map statically.
        if (tuple.Elements.Take(reference.Index + 1).Any(element => element.IsSpread || element.IsImplicitSpread))
            return null;
        return tuple.Elements[reference.Index].Expression?.Span;
    }

    private static bool TryGetContainingStage(
        SyntaxNode reference,
        IReadOnlyDictionary<SyntaxNode, SyntaxNode> parents,
        out RootExpressionSyntax root,
        out ExpressionSyntax stage)
    {
        var current = reference;
        while (parents.TryGetValue(current, out var parent))
        {
            if (parent is RootExpressionSyntax expressionRoot && current is ExpressionSyntax expression)
            {
                root = expressionRoot;
                stage = expression;
                return true;
            }
            current = parent;
        }
        root = null!;
        stage = null!;
        return false;
    }

    private static bool IsInElementScope(
        SyntaxNode reference,
        ExpressionSyntax stage,
        IReadOnlyDictionary<SyntaxNode, SyntaxNode> parents)
    {
        var current = reference;
        while (!ReferenceEquals(current, stage) && parents.TryGetValue(current, out var parent))
        {
            if (parent is FunctionCallSyntax call && IntroducesElementScope(call.Name))
                return true;
            current = parent;
        }
        return false;
    }

    private static bool IntroducesElementScope(string functionName)
    {
        var functions = new FunctionTypeMapper();
        return functions.TryExecute(functionName, out var type) &&
               (type == typeof(Map) || type == typeof(Filter) || type == typeof(MapOver) || type == typeof(MapWith));
    }

    private static ReferenceScope? GetInputBindingScope(
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

    private static ReferenceScope CreateBindingScope(
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

    private static ReferenceScope? GetInputBoundFieldScope(
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
        => GetPreviousStageNode(root, expression)?.Span;

    private static SyntaxNode? GetPreviousStageNode(RootExpressionSyntax root, ExpressionSyntax expression)
        => root switch
        {
            ClosedExpressionSyntax closed when ReferenceEquals(closed.Value, expression) => null,
            ClosedExpressionSyntax closed => Previous(closed.Value, closed.Pipeline, expression),
            OpenExpressionSyntax open when ReferenceEquals(open.Source, expression) => null,
            OpenExpressionSyntax open => Previous(open.Source, open.Pipeline, expression),
            _ => null
        };

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
