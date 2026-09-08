using Expressif.Values;

namespace Expressif.LanguageServer.Core.Evaluation;

public sealed class ExpressionEvaluationService : IExpressionEvaluationService
{
    public ExpressionEvaluationResult Evaluate(
        string expression,
        string? input = null,
        EvaluationInputFormat inputFormat = EvaluationInputFormat.Literal)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return ExpressionEvaluationResult.Failure("The expression is empty.");

        try
        {
            if (input is null)
            {
                var closedResult = Expression.CreateClosed(expression, new Context()).Evaluate(null);
                return ExpressionEvaluationResult.Success(ValueFormatter.Format(closedResult));
            }

            var value = EvaluationInputParser.Parse(input, inputFormat);
            var result = Expression.Create(expression, new Context()).Evaluate(value);
            return ExpressionEvaluationResult.Success(ValueFormatter.Format(result));
        }
        catch (ExpressionRequiresInputException)
        {
            return ExpressionEvaluationResult.InputRequired();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return ExpressionEvaluationResult.Failure(exception.Message);
        }
    }
}
