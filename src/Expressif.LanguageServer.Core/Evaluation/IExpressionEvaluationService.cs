namespace Expressif.LanguageServer.Core.Evaluation;

public interface IExpressionEvaluationService
{
    ExpressionEvaluationResult Evaluate(
        string expression,
        string? input = null,
        EvaluationInputFormat inputFormat = EvaluationInputFormat.Literal,
        EvaluationOutputFormat outputFormat = EvaluationOutputFormat.Expressif,
        EvaluationOutputOptions? outputOptions = null);

    ExpressionEvaluationResult EvaluateMany(
        string expression,
        IReadOnlyList<string> inputs,
        EvaluationInputFormat inputFormat,
        EvaluationOutputFormat outputFormat = EvaluationOutputFormat.Expressif,
        EvaluationOutputOptions? outputOptions = null);
}
