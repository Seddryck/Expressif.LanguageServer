using Expressif.Values;
using Expressif.Serialization;

namespace Expressif.LanguageServer.Core.Evaluation;

public sealed class ExpressionEvaluationService : IExpressionEvaluationService
{
    public ExpressionEvaluationResult Evaluate(
        string expression,
        string? input = null,
        EvaluationInputFormat inputFormat = EvaluationInputFormat.Literal,
        EvaluationOutputFormat outputFormat = EvaluationOutputFormat.Expressif,
        EvaluationOutputOptions? outputOptions = null)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return ExpressionEvaluationResult.Failure("The expression is empty.");

        try
        {
            outputOptions ??= new EvaluationOutputOptions();
            if (input is null)
            {
                var closedResult = Expression.CreateClosed(expression, new Context()).Evaluate(null);
                return ExpressionEvaluationResult.Success(Serialize(closedResult, outputFormat, outputOptions));
            }

            var value = EvaluationInputParser.Parse(input, inputFormat);
            var result = Expression.Create(expression, new Context()).Evaluate(value);
            return ExpressionEvaluationResult.Success(Serialize(result, outputFormat, outputOptions));
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

    private static string Serialize(
        object? value,
        EvaluationOutputFormat outputFormat,
        EvaluationOutputOptions outputOptions)
    {
        var serializationFormat = outputFormat switch
        {
            EvaluationOutputFormat.Expressif => ValueSerializationFormat.Raw,
            EvaluationOutputFormat.Json => ValueSerializationFormat.Json,
            _ => throw new ArgumentOutOfRangeException(nameof(outputFormat), outputFormat, "Unknown evaluation output format."),
        };
        var valueFormat = outputOptions.Formatting switch
        {
            EvaluationOutputFormatting.Compact => ValueFormat.Compact,
            EvaluationOutputFormatting.Pretty => ValueFormat.Pretty,
            _ => throw new ArgumentOutOfRangeException(nameof(outputOptions), outputOptions, "Unknown evaluation output formatting."),
        };
        var indent = outputOptions.Formatting == EvaluationOutputFormatting.Pretty
            ? new string(' ', outputOptions.Indent)
            : string.Empty;
        var serialized = ValueSerializers.Resolve(serializationFormat).Serialize(value, valueFormat, indent);
        return outputOptions.Formatting == EvaluationOutputFormatting.Pretty
            ? $"\n{serialized}"
            : serialized;
    }
}
