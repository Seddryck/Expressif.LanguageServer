using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Hover;

public interface IFunctionHoverService
{
    FunctionHover? GetHover(RootExpressionSyntax syntaxTree, int cursorOffset);
}
