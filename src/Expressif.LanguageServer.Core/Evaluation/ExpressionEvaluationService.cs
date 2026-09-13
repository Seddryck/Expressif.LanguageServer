using Expressif.Values;
using Expressif.Serialization;

namespace Expressif.LanguageServer.Core.Evaluation;

public sealed class ExpressionEvaluationService : IExpressionEvaluationService
{
    public ExpressionEvaluationResult Evaluate(
        string expression,
        string? input = null,
        EvaluationInputFormat inputFormat = EvaluationInputFormat.Literal,
        EvaluationOutputFormat outputFormat = EvaluationOutputFormat.Expressif)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return ExpressionEvaluationResult.Failure("The expression is empty.");

        try
        {
            if (input is null)
            {
                var closedResult = Expression.CreateClosed(expression, new Context()).Evaluate(null);
                return ExpressionEvaluationResult.Success(Serialize(closedResult, outputFormat));
            }

            var value = EvaluationInputParser.Parse(input, inputFormat);
            var result = Expression.Create(expression, new Context()).Evaluate(value);
            return ExpressionEvaluationResult.Success(Serialize(result, outputFormat));
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

    private static string Serialize(object? value, EvaluationOutputFormat outputFormat)
    {
        var serializationFormat = outputFormat switch
        {
            EvaluationOutputFormat.Expressif => ValueSerializationFormat.Raw,
            EvaluationOutputFormat.Json => ValueSerializationFormat.Json,
            _ => throw new ArgumentOutOfRangeException(nameof(outputFormat), outputFormat, "Unknown evaluation output format."),
        };
        return ValueSerializers.Resolve(serializationFormat).Serialize(value);
    }
}
