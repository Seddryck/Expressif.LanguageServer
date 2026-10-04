using System.Text;
using Expressif.Serialization;
using Expressif.Values;

namespace Expressif.LanguageServer.Core.Evaluation;

internal static class EvaluationInputParser
{
    public static object? Parse(string input, EvaluationInputFormat format)
        => format switch
        {
            EvaluationInputFormat.Literal => new ParameterValueConverter().Parse(input),
            EvaluationInputFormat.Json => JsonValueReader.Read(input),
            EvaluationInputFormat.Csv => ParseCsv(input),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported input format."),
        };

    public static object?[] ParseMany(IReadOnlyList<string> inputs, EvaluationInputFormat format)
    {
        if (format != EvaluationInputFormat.JsonFiles)
            throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported multiple-input format.");

        var values = new object?[inputs.Count];
        for (var index = 0; index < inputs.Count; index++)
        {
            try
            {
                values[index] = JsonValueReader.Read(inputs[index]);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                throw new FormatException($"Invalid JSON in selected file {index + 1}: {exception.Message}", exception);
            }
        }
        return values;
    }

    private static object?[] ParseCsv(string input)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        IReadOnlyList<CsvSourceOption>? options = input.Contains("\r\n", StringComparison.Ordinal)
            ? null
            : [new("line-terminator", "\n")];
        return CsvValueReader.Read(stream, options).ToArray();
    }
}
