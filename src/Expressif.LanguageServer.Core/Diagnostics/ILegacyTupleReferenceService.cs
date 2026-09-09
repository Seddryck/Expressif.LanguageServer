using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Diagnostics;

public interface ILegacyTupleReferenceService
{
    IReadOnlyList<LegacyTupleReference> GetReferences(RootExpressionSyntax syntaxTree);
}
