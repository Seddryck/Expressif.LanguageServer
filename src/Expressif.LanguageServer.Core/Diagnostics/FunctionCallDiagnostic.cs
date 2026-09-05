namespace Expressif.LanguageServer.Core.Diagnostics;

public sealed record FunctionCallDiagnostic(
    string Message,
    int Start,
    int Length);
