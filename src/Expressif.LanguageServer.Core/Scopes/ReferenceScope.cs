using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Scopes;

public sealed record ReferenceScope(SourceSpan Reference, SourceSpan? Supplier, string Description);
