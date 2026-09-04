namespace Expressif.LanguageServer.Core.Formatting;

public sealed record DocumentFormattingOptions(
    int TabSize,
    bool InsertSpaces,
    string NewLine,
    bool InsertFinalNewLine)
{
    public string Indentation => InsertSpaces ? new string(' ', Math.Max(1, TabSize)) : "\t";
}
