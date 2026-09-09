using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Scopes;

public sealed record FieldScope(SourceSpan Reference, SourceSpan? Supplier, string Description);
