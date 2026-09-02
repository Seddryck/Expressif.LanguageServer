namespace Expressif.LanguageServer.Core.SignatureHelp;

public sealed record SignatureParameter(string Label, string Description);

public sealed record FunctionSignatureHelp(
    string Signature,
    string Description,
    IReadOnlyList<SignatureParameter> Parameters,
    int? ActiveParameter);
