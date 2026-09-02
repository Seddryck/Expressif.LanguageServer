namespace Expressif.LanguageServer.Core.Hover;

public sealed record FunctionHover(
    string Signature,
    string Description,
    int IdentifierStart,
    int IdentifierLength);
