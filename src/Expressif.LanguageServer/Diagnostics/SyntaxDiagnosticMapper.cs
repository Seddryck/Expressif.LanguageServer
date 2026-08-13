using System.Text;
using Expressif.Syntax;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Expressif.LanguageServer.Diagnostics;

internal static class SyntaxDiagnosticMapper
{
    public static Diagnostic Map(string source, SyntaxError error) => new()
    {
        Range = new Range(
            ToPosition(source, error.Span.Start),
            ToPosition(source, error.Span.End)),
        Severity = DiagnosticSeverity.Error,
        Source = "expressif",
        Message = CreateMessage(error)
    };

    private static Position ToPosition(string source, int utf8Offset)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        var clampedOffset = Math.Clamp(utf8Offset, 0, bytes.Length);
        var textOffset = Encoding.UTF8.GetCharCount(bytes, 0, clampedOffset);
        var line = 0;
        var lineStart = 0;

        for (var index = 0; index < textOffset; index++)
        {
            if (source[index] != '\n')
                continue;

            line++;
            lineStart = index + 1;
        }

        return new(line, textOffset - lineStart);
    }

    private static string CreateMessage(SyntaxError error)
    {
        var node = error.NodeType.Replace('_', ' ');
        if (error.IsMissing)
            return $"Missing {node}.";
        if (!string.IsNullOrWhiteSpace(error.Text))
            return $"Unexpected syntax '{error.Text}'.";
        return $"Invalid {node} syntax.";
    }
}
