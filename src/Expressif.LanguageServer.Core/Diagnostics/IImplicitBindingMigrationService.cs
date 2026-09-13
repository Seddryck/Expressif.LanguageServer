using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Diagnostics;

public interface IImplicitBindingMigrationService
{
    IReadOnlyList<ImplicitBindingMigration> GetMigrations(RootExpressionSyntax syntaxTree);
}
