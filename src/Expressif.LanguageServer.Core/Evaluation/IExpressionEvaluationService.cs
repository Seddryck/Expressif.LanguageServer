namespace Expressif.LanguageServer.Core.Evaluation;

public interface IExpressionEvaluationService
{
    ExpressionEvaluationResult Evaluate(string expression, string input);
}
