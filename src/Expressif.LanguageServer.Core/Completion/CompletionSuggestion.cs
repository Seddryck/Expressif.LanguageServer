namespace Expressif.LanguageServer.Core.Completion;

public sealed record CompletionSuggestion(
    string Label,
    string InsertText,
    bool IsCanonical,
    int ReplacementStart,
    int ReplacementLength);
