namespace Expressif.LanguageServer.Core.CodeActions;

public sealed record FunctionReplacement(
    string OldName,
    string NewName,
    int IdentifierStart,
    int IdentifierLength);
