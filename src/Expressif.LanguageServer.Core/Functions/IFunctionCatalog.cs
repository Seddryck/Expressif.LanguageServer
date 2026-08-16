namespace Expressif.LanguageServer.Core.Functions;

public interface IFunctionCatalog
{
    IReadOnlyList<FunctionMetadata> Functions { get; }
}
