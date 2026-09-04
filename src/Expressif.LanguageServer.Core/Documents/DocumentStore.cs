using System.Collections.Concurrent;
using Expressif.LanguageServer.Core.Syntax;

namespace Expressif.LanguageServer.Core.Documents;

public sealed class DocumentStore(ISyntaxService syntaxService) : IDocumentStore
{
    private readonly ConcurrentDictionary<Uri, DocumentSnapshot> documents = new();

    public DocumentSnapshot Open(Uri uri, string text, int? version)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var document = CreateSnapshot(uri, text, version);
        documents[uri] = document;
        return document;
    }

    public DocumentSnapshot Change(Uri uri, string text, int? version)
    {
        ArgumentNullException.ThrowIfNull(uri);
        var document = CreateSnapshot(uri, text, version);

        while (documents.TryGetValue(uri, out var current))
        {
            if (current.Version is int currentVersion &&
                version is int incomingVersion &&
                incomingVersion <= currentVersion)
            {
                throw new InvalidOperationException(
                    $"Document '{uri}' version {incomingVersion} must be greater than version {currentVersion}.");
            }

            if (documents.TryUpdate(uri, document, current))
                return document;
        }

        throw new InvalidOperationException($"Document '{uri}' is not open.");
    }

    public bool Close(Uri uri) => documents.TryRemove(uri, out _);

    public bool TryGet(Uri uri, out DocumentSnapshot? document)
        => documents.TryGetValue(uri, out document);

    private DocumentSnapshot CreateSnapshot(Uri uri, string text, int? version)
    {
        var result = syntaxService.Parse(text);
        return new(uri, text, version, result.SyntaxDocument, result.Errors);
    }
}
