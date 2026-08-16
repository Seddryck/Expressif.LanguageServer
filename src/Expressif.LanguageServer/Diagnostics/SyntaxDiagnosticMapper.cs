using System.Text;
using Expressif.Syntax;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Expressif.LanguageServer.Diagnostics;

internal static class SyntaxDiagnosticMapper
{
    public static Diagnostic Map(string source, SyntaxError error)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(error);

        var (start, end) = GetUsefulTextRange(source, error.Span);
        return new()
        {
            Range = new Range(ToPosition(source, start), ToPosition(source, end)),
            Severity = DiagnosticSeverity.Error,
            Source = "expressif",
            Message = CreateMessage(error)
        };
    }

    private static (int Start, int End) GetUsefulTextRange(string source, SourceSpan span)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        var startByte = Math.Clamp(span.Start, 0, bytes.Length);
        var endByte = Math.Clamp(span.End, startByte, bytes.Length);
        var start = Encoding.UTF8.GetCharCount(bytes, 0, startByte);
        var end = Encoding.UTF8.GetCharCount(bytes, 0, endByte);

        if (start != end || source.Length == 0)
            return (start, end);

        if (start < source.Length && source[start] is not ('\r' or '\n'))
            return (start, NextCodePoint(source, start));

        var previous = PreviousCodePoint(source, start);
        while (previous > 0 && source[previous] is '\r' or '\n')
            previous = PreviousCodePoint(source, previous);

        return source[previous] is '\r' or '\n' ? (start, end) : (previous, NextCodePoint(source, previous));
    }

    private static int NextCodePoint(string source, int offset)
        => offset + (char.IsHighSurrogate(source[offset]) &&
                     offset + 1 < source.Length &&
                     char.IsLowSurrogate(source[offset + 1]) ? 2 : 1);

    private static int PreviousCodePoint(string source, int offset)
        => offset >= 2 && char.IsLowSurrogate(source[offset - 1]) && char.IsHighSurrogate(source[offset - 2])
            ? offset - 2
            : Math.Max(0, offset - 1);

    private static Position ToPosition(string source, int textOffset)
    {
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
