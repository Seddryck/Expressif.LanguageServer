namespace Expressif.LanguageServer.Core.Completion;

public interface ICompletionService
{
    IReadOnlyList<CompletionSuggestion> GetCompletions(string text, int cursorOffset);
}
