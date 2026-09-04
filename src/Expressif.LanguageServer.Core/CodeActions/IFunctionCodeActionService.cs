using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.CodeActions;

public interface IFunctionCodeActionService
{
    IReadOnlyList<FunctionReplacement> GetReplacements(
        RootExpressionSyntax syntaxTree, int selectionStart, int selectionLength);
}
