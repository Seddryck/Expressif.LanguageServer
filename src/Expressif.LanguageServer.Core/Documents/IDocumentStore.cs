namespace Expressif.LanguageServer.Core.Documents;

public interface IDocumentStore
{
    DocumentSnapshot Open(Uri uri, string text, int? version);
    DocumentSnapshot Change(Uri uri, string text, int? version);
    bool Close(Uri uri);
    bool TryGet(Uri uri, out DocumentSnapshot? document);
}
