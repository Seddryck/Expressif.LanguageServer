using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Syntax;

public sealed record SyntaxParseResult(SourceFileSyntax? SyntaxDocument, IReadOnlyList<SyntaxError> Errors)
{
    public RootExpressionSyntax? SyntaxTree => SyntaxDocument?.Expression;
    public bool IsValid => SyntaxDocument is not null && Errors.Count == 0;
}
