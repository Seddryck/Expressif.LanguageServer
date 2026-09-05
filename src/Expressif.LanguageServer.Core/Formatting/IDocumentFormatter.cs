using Expressif.LanguageServer.Core.Documents;

namespace Expressif.LanguageServer.Core.Formatting;

public interface IDocumentFormatter
{
    string Format(DocumentSnapshot document, DocumentFormattingOptions options);
}
