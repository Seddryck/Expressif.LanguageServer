using Expressif.LanguageServer.Core.Documents;

namespace Expressif.LanguageServer.Core.Scopes;

public interface IFieldScopeService
{
    FieldScope? GetScope(DocumentSnapshot document, int cursorOffset);
}
