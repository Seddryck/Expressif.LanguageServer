using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.SignatureHelp;

public interface IFunctionSignatureHelpService
{
    FunctionSignatureHelp? GetSignatureHelp(RootExpressionSyntax syntaxTree, int cursorOffset);
}
