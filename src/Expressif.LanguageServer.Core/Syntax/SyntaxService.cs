using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Syntax;

public sealed class SyntaxService : ISyntaxService
{
    public SyntaxParseResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            return new(ExpressifSyntax.ParseDocument(text), []);
        }
        catch (ExpressifSyntaxException exception)
        {
            if (text.Contains("|#>", StringComparison.Ordinal))
            {
                var normalized = text.Replace("|#>", "|  ", StringComparison.Ordinal);
                try
                {
                    return new(ExpressifSyntax.ParseDocument(normalized), []);
                }
                catch (ExpressifSyntaxException)
                {
                    // Preserve diagnostics for the source as authored.
                }
            }

            return new(null, exception.Errors);
        }
    }
}
