namespace Expressif.LanguageServer.Core.Completion;

public sealed record CompletionSuggestion(
    string Label,
    string InsertText,
    bool IsCanonical,
    int ReplacementStart,
    int ReplacementLength,
    string Description = "",
    bool Deprecated = false,
    string? Replacement = null,
    string? Sunset = null,
    IReadOnlyList<string>? SnippetParameters = null,
    CompletionSuggestionKind Kind = CompletionSuggestionKind.Function);

public enum CompletionSuggestionKind
{
    Function,
    Field
}
