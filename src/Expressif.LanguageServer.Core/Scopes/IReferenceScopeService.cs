using Expressif.LanguageServer.Core.Documents;

namespace Expressif.LanguageServer.Core.Scopes;

public interface IReferenceScopeService
{
    ReferenceScope? GetScope(DocumentSnapshot document, int cursorOffset);
}
