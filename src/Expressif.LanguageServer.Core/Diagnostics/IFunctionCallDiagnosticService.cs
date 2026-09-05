using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Diagnostics;

public interface IFunctionCallDiagnosticService
{
    IReadOnlyList<FunctionCallDiagnostic> GetDiagnostics(RootExpressionSyntax syntaxTree);
}
