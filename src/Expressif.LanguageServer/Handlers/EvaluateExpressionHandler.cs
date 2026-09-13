using Expressif.LanguageServer.Core.Evaluation;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Expressif.LanguageServer.Handlers;

public sealed class EvaluateExpressionHandler(
    IExpressionEvaluationService evaluation,
    ILanguageServerConfiguration configuration,
    ISerializer serializer)
    : ExecuteTypedResponseCommandHandlerBase<string, string, EvaluationInputFormat, EvaluationOutputFormat, ExpressionEvaluationResult>(CommandName, serializer)
{
    public const string CommandName = "expressif.evaluateExpression";

    public override Task<ExpressionEvaluationResult> Handle(
        string expression,
        string? input,
        EvaluationInputFormat inputFormat,
        EvaluationOutputFormat outputFormat,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(evaluation.Evaluate(
            expression,
            input,
            inputFormat,
            outputFormat,
            GetOutputOptions()));
    }

    private EvaluationOutputOptions GetOutputOptions()
    {
        var formatting = string.Equals(
            configuration["expressif:output:formatting"],
            "pretty",
            StringComparison.OrdinalIgnoreCase)
            ? EvaluationOutputFormatting.Pretty
            : EvaluationOutputFormatting.Compact;
        var indent = int.TryParse(configuration["expressif:output:indent"], out var configuredIndent)
            && configuredIndent is >= 0 and <= 16
                ? configuredIndent
                : 2;

        return new EvaluationOutputOptions(formatting, indent);
    }
}
