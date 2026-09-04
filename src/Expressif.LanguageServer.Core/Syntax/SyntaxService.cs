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
            return new(null, exception.Errors);
        }
    }
}
