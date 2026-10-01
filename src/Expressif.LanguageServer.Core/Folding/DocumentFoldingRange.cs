namespace Expressif.LanguageServer.Core.Folding;

public sealed record DocumentFoldingRange(
    FoldingPosition Start,
    FoldingPosition End,
    FoldingKind Kind = FoldingKind.Region);
