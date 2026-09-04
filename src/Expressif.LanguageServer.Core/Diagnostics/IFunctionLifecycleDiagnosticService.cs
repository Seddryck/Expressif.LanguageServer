using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Diagnostics;

public interface IFunctionLifecycleDiagnosticService
{
    IReadOnlyList<FunctionLifecycleDiagnostic> GetDiagnostics(RootExpressionSyntax syntaxTree);
}
