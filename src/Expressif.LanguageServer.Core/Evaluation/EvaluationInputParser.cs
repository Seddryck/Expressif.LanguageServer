using System.Text;
using System.Text.Json;
using Expressif.Values;
using PocketCsvReader;

namespace Expressif.LanguageServer.Core.Evaluation;

internal static class EvaluationInputParser
{
    public static object? Parse(string input, EvaluationInputFormat format)
        => format switch
        {
            EvaluationInputFormat.Literal => new ParameterValueConverter().Parse(input),
            EvaluationInputFormat.Json => ParseJson(input),
            EvaluationInputFormat.Csv => ParseCsv(input),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported input format."),
        };

    private static object? ParseJson(string input)
    {
        using var document = JsonDocument.Parse(input);
        return ConvertJsonValue(document.RootElement);
    }

    private static object? ConvertJsonValue(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => ConvertJsonObject(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDecimal(out var number) => number,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => throw new FormatException($"Unsupported JSON value kind '{element.ValueKind}'."),
        };

    private static RecordValue ConvertJsonObject(JsonElement element)
    {
        var record = new RecordValue();
        foreach (var property in element.EnumerateObject())
            record.Set(property.Name, ConvertJsonValue(property.Value));
        return record;
    }

    private static object?[] ParseCsv(string input)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        var rows = new CsvReader().ToArrayString(stream).ToArray();
        if (rows.Length == 0)
            throw new FormatException("CSV input is empty. A header row is required.");

        var headers = rows[0];
        if (headers.Length == 0 || headers.Any(string.IsNullOrWhiteSpace))
            throw new FormatException("CSV input contains an empty header.");
        if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
            throw new FormatException("CSV input contains duplicate headers.");

        return rows.Skip(1).Select((row, index) => ConvertCsvRow(headers, row, index + 2)).ToArray();
    }

    private static RecordValue ConvertCsvRow(string?[] headers, string?[] values, int rowNumber)
    {
        if (values.Length != headers.Length)
            throw new FormatException(
                $"CSV row {rowNumber} contains {values.Length} fields; expected {headers.Length}.");

        var record = new RecordValue();
        for (var index = 0; index < headers.Length; index++)
            record.Set(headers[index]!, values[index]);
        return record;
    }
}
