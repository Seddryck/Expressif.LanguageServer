using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Expressif.LanguageServer.Handlers;

internal static class DocumentPositions
{
    internal static bool TryGetOffset(string text, Position position, out int offset)
    {
        offset = 0;
        if (position.Line < 0 || position.Character < 0)
            return false;
        for (var line = 0; line < position.Line; line++)
        {
            var newline = text.IndexOf('\n', offset);
            if (newline < 0)
                return false;
            offset = newline + 1;
        }

        var lineEnd = text.IndexOf('\n', offset);
        if (lineEnd < 0)
            lineEnd = text.Length;
        var lineLength = lineEnd - offset;
        if (lineLength > 0 && text[offset + lineLength - 1] == '\r')
            lineLength--;
        if (position.Character > lineLength)
            return false;

        offset += position.Character;
        return true;
    }

    internal static Position GetPosition(string text, int offset)
    {
        var line = 0;
        var lineStart = 0;
        for (var index = 0; index < offset; index++)
        {
            if (text[index] != '\n')
                continue;
            line++;
            lineStart = index + 1;
        }

        return new Position(line, offset - lineStart);
    }
}
