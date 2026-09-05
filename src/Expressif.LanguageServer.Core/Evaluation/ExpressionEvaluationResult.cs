namespace Expressif.LanguageServer.Core.Evaluation;

public sealed record ExpressionEvaluationResult(bool Succeeded, string? Value, string? Error)
{
    public static ExpressionEvaluationResult Success(string value) => new(true, value, null);
    public static ExpressionEvaluationResult Failure(string error) => new(false, null, error);
}
