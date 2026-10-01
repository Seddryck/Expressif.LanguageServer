namespace Expressif.LanguageServer.Core.Formatting;

public sealed record SourceTextEdit(int Start, int Length, string NewText);
