namespace Expressif.LanguageServer.Core.Evaluation;

public sealed record ExpressionEvaluationResult(
    bool Succeeded,
    bool RequiresInput,
    string? Value,
    string? Error)
{
    public static ExpressionEvaluationResult Success(string value) => new(true, false, value, null);
    public static ExpressionEvaluationResult InputRequired() => new(false, true, null, null);
    public static ExpressionEvaluationResult Failure(string error) => new(false, false, null, error);
}
