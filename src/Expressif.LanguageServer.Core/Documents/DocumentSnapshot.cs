using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Documents;

public sealed record DocumentSnapshot(Uri Uri, string Text, int? Version,
    RootExpressionSyntax? SyntaxTree, IReadOnlyList<SyntaxError> SyntaxErrors);
