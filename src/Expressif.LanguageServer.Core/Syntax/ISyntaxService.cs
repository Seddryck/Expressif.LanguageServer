namespace Expressif.LanguageServer.Core.Syntax;

public interface ISyntaxService
{
    SyntaxParseResult Parse(string text);
}
