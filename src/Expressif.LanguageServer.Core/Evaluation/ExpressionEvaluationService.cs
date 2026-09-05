using Expressif.Values;

namespace Expressif.LanguageServer.Core.Evaluation;

public sealed class ExpressionEvaluationService : IExpressionEvaluationService
{
    public ExpressionEvaluationResult Evaluate(string expression, string input)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return ExpressionEvaluationResult.Failure("The expression is empty.");

        try
        {
            var value = new ParameterValueConverter().Parse(input);
            var result = Expression.Create(expression, new Context()).Evaluate(value);
            return ExpressionEvaluationResult.Success(ValueFormatter.Format(result));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return ExpressionEvaluationResult.Failure(exception.Message);
        }
    }
}
