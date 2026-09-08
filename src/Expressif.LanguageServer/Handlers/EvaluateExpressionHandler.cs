using Expressif.LanguageServer.Core.Evaluation;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;

namespace Expressif.LanguageServer.Handlers;

public sealed class EvaluateExpressionHandler(
    IExpressionEvaluationService evaluation,
    ISerializer serializer)
    : ExecuteTypedResponseCommandHandlerBase<string, string, EvaluationInputFormat, ExpressionEvaluationResult>(CommandName, serializer)
{
    public const string CommandName = "expressif.evaluateExpression";

    public override Task<ExpressionEvaluationResult> Handle(
        string expression,
        string? input,
        EvaluationInputFormat inputFormat,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(evaluation.Evaluate(expression, input, inputFormat));
    }
}
