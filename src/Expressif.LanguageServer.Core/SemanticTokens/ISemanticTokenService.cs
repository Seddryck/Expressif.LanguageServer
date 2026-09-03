using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.SemanticTokens;

public interface ISemanticTokenService
{
    IReadOnlyList<SemanticTokenSpan> GetTokens(RootExpressionSyntax syntaxTree, string text);
}
