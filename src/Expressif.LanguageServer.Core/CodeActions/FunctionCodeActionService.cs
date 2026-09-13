using Expressif.LanguageServer.Core.Functions;
using Expressif.LanguageServer.Core.Syntax;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.CodeActions;

public sealed class FunctionCodeActionService(IFunctionCatalog functions) : IFunctionCodeActionService
{
    public IReadOnlyList<FunctionReplacement> GetReplacements(
        RootExpressionSyntax syntaxTree, int selectionStart, int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        if (selectionStart < 0 || selectionLength < 0)
            throw new ArgumentOutOfRangeException(nameof(selectionStart));

        var selectionEnd = (long)selectionStart + selectionLength;
        return CallableSyntaxReference.DescendantsOf(syntaxTree)
            .Where(call => selectionLength == 0
                ? selectionStart >= call.NameSpan.Start && selectionStart <= call.NameSpan.End
                : selectionStart < call.NameSpan.End && selectionEnd > call.NameSpan.Start)
            .Select(call => (Call: call, Metadata: FindFunction(call.Name)))
            .Where(match => match.Metadata is
            {
                Deprecated: true,
                SafeDirectReplacement: true,
                Replacement: not null
            })
            .Select(match => new FunctionReplacement(
                match.Call.Name,
                match.Metadata!.Replacement!,
                match.Call.NameSpan.Start,
                match.Call.NameSpan.Length))
            .ToArray();
    }

    private FunctionMetadata? FindFunction(string name) => functions.Functions.FirstOrDefault(function =>
        function.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
        function.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase));
}
