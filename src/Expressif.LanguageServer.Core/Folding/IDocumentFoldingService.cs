using Expressif.LanguageServer.Core.Documents;

namespace Expressif.LanguageServer.Core.Folding;

public interface IDocumentFoldingService
{
    IReadOnlyList<DocumentFoldingRange> GetRanges(DocumentSnapshot document);
}
