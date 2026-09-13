namespace Expressif.LanguageServer.Core.Diagnostics;

public sealed record ImplicitBindingMigration(
    string Operator,
    string Callable,
    string Code,
    string Message,
    int Start,
    int Length,
    IReadOnlyList<ImplicitBindingReplacement> Replacements)
{
    public bool Overlaps(int selectionStart, int selectionLength)
    {
        var selectionEnd = (long)selectionStart + selectionLength;
        return selectionLength == 0
            ? selectionStart >= Start && selectionStart <= Start + Length
            : selectionStart < Start + Length && selectionEnd > Start;
    }
}

public sealed record ImplicitBindingReplacement(string Title, string NewText, bool Preferred);
