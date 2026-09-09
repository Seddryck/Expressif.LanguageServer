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

        // Analyze this immutable snapshot; no independent cache can outlive an edit.
        var analysis = new SemanticAnalyzer().Analyze(document.SyntaxTree);
        var reference = analysis.References.FirstOrDefault(reference =>
            cursorOffset >= reference.Span.Start && cursorOffset < reference.Span.End);
        if (reference is null || reference.Source.Kind == SemanticSourceKind.Unresolved)
            return null;

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
