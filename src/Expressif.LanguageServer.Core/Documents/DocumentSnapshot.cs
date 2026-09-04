using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Documents;

public sealed record DocumentSnapshot(Uri Uri, string Text, int? Version,
    SourceFileSyntax? SyntaxDocument, IReadOnlyList<SyntaxError> SyntaxErrors)
{
    public RootExpressionSyntax? SyntaxTree => SyntaxDocument?.Expression;
}
