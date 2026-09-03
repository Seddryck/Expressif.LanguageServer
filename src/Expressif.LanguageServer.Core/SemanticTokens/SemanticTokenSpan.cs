namespace Expressif.LanguageServer.Core.SemanticTokens;

public sealed record SemanticTokenSpan(int Start, int Length, SemanticTokenKind Kind);
