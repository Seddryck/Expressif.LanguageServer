namespace Expressif.LanguageServer.Core.Functions;

public sealed record FunctionParameterMetadata(
    string Name,
    bool Optional,
    string Description,
    bool Variadic = false,
    int MinimumCardinality = 1)
{
    public string Label => $"{Name}{(Optional ? "?" : string.Empty)}{(Variadic ? "..." : string.Empty)}";
}

public sealed record TupleBindingSignatureMetadata(
    bool SupportsTupleBinding,
    bool Variadic,
    int MinimumArguments,
    int? MaximumArguments);

public sealed record FunctionMetadata(
    string Name,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<FunctionParameterMetadata> Parameters,
    string Description,
    string Category,
    bool Deprecated = false,
    string? Replacement = null,
    string? Sunset = null,
    bool SafeDirectReplacement = false,
    Type? ImplementationType = null,
    IReadOnlyList<TupleBindingSignatureMetadata>? TupleBindingSignatures = null)
{
    public bool SupportsTupleBinding =>
        TupleBindingSignatures?.Any(signature => signature.SupportsTupleBinding) == true;
}
