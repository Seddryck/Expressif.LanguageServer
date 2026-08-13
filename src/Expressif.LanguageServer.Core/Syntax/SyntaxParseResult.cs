using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Syntax;

public sealed record SyntaxParseResult(RootExpressionSyntax? SyntaxTree, IReadOnlyList<SyntaxError> Errors)
{
    public bool IsValid => SyntaxTree is not null && Errors.Count == 0;
}
