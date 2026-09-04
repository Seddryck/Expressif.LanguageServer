namespace Expressif.LanguageServer.Core.Diagnostics;

public sealed record FunctionLifecycleDiagnostic(
    string FunctionName,
    string Message,
    int IdentifierStart,
    int IdentifierLength);
