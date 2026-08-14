namespace Expressif.LanguageServer.Core.Functions;

public sealed record FunctionParameterMetadata(string Name, bool Optional, string Description);

public sealed record FunctionMetadata(
    string Name,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<FunctionParameterMetadata> Parameters,
    string Description,
    string Category);
