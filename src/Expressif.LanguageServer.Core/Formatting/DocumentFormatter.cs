using System.Text;
using Expressif.LanguageServer.Core.Documents;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Formatting;

public sealed class DocumentFormatter : IDocumentFormatter
{
    public string Format(DocumentSnapshot document, DocumentFormattingOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        var syntaxDocument = GetSyntaxDocument(document);
        if (syntaxDocument is null)
            return document.Text;

        var layout = Layout.Create(syntaxDocument);
        var tokens = Tokenize(document.Text, layout).ToArray();
        if (tokens.Length == 0)
            return document.Text;

        var writer = new FormattingWriter(options, layout);
        foreach (var token in tokens)
            writer.Write(token);
        return writer.Complete();
    }

    private static SourceFileSyntax? GetSyntaxDocument(DocumentSnapshot document)
    {
        if (document.SyntaxDocument is not null && document.SyntaxErrors.Count == 0)
            return document.SyntaxDocument;

        if (!document.Text.Contains("|#>", StringComparison.Ordinal))
            return null;

        var normalized = document.Text.Replace("|#>", "|  ", StringComparison.Ordinal);
        try
        {
            return ExpressifSyntax.ParseDocument(normalized);
        }
        catch (ExpressifSyntaxException)
        {
            return null;
        }
    }

    private static IEnumerable<FormatToken> Tokenize(string source, Layout layout)
    {
        var position = 0;
        var lineBreakBefore = false;
        while (position < source.Length)
        {
            if (char.IsWhiteSpace(source[position]))
            {
                lineBreakBefore |= source[position] is '\r' or '\n';
                position++;
                continue;
            }

            var pipelineLength = GetPipelineOperatorLength(source, position, layout);
            if (pipelineLength > 0)
            {
                yield return new(TokenKind.Pipeline, source.Substring(position, pipelineLength), position,
                    lineBreakBefore);
                position += pipelineLength;
                lineBreakBefore = false;
                continue;
            }

            if (layout.ProtectedByStart.TryGetValue(position, out var protectedSpan))
            {
                yield return new(protectedSpan.Kind, protectedSpan.Text, position, lineBreakBefore);
                position += protectedSpan.Text.Length;
                lineBreakBefore = false;
                continue;
            }

            var pair = position + 1 < source.Length ? source.Substring(position, 2) : string.Empty;
            if (pair is "|>" or ":=" or "=>")
            {
                yield return new(TokenKind.BinaryOperator, source.Substring(position, 2), position, lineBreakBefore);
                position += 2;
                lineBreakBefore = false;
                continue;
            }

            var start = position;
            var kind = source[position] switch
            {
                _ when layout.OpenDelimiters.Contains(position) => TokenKind.OpenDelimiter,
                _ when layout.CloseDelimiters.Contains(position) => TokenKind.CloseDelimiter,
                '(' or '{' or '[' => TokenKind.OpenDelimiter,
                ')' or '}' or ']' => TokenKind.CloseDelimiter,
                ',' => TokenKind.Comma,
                _ => TokenKind.Atom
            };

            if (kind != TokenKind.Atom)
            {
                position++;
                yield return new(kind, source[start..position], start, lineBreakBefore);
                lineBreakBefore = false;
                continue;
            }

            while (position < source.Length &&
                   !char.IsWhiteSpace(source[position]) &&
                   !IsStructuralCharacter(source[position]) &&
                   !StartsFormattingOperator(source, position) &&
                   !layout.ProtectedByStart.ContainsKey(position))
            {
                position++;
            }

            if (position == start)
                position++;
            yield return new(TokenKind.Atom, source[start..position], start, lineBreakBefore);
            lineBreakBefore = false;
        }
    }

    private static int GetPipelineOperatorLength(string source, int position, Layout layout)
    {
        if (!layout.PipelineOperators.Contains(position))
            return 0;

        if (source.AsSpan(position).StartsWith("|#>"))
            return 3;
        return source.AsSpan(position).StartsWith("|>") ? 2 : 1;
    }

    private static bool IsStructuralCharacter(char value)
        => value is '(' or ')' or '{' or '}' or '[' or ']' or ',' or '|';

    private static bool StartsFormattingOperator(string source, int position)
        => position + 1 < source.Length && source.Substring(position, 2) is "|>" or ":=" or "=>";

    private sealed class Layout
    {
        private Layout(
            IReadOnlyDictionary<int, ProtectedSpan> protectedByStart,
            IReadOnlySet<int> pipelineOperators,
            IReadOnlySet<int> multilinePipelineOperators,
            IReadOnlySet<int> multilineDelimiters,
            IReadOnlySet<int> openDelimiters,
            IReadOnlySet<int> closeDelimiters)
        {
            ProtectedByStart = protectedByStart;
            PipelineOperators = pipelineOperators;
            MultilinePipelineOperators = multilinePipelineOperators;
            MultilineDelimiters = multilineDelimiters;
            OpenDelimiters = openDelimiters;
            CloseDelimiters = closeDelimiters;
        }

        public IReadOnlyDictionary<int, ProtectedSpan> ProtectedByStart { get; }
        public IReadOnlySet<int> PipelineOperators { get; }
        public IReadOnlySet<int> MultilinePipelineOperators { get; }
        public IReadOnlySet<int> MultilineDelimiters { get; }
        public IReadOnlySet<int> OpenDelimiters { get; }
        public IReadOnlySet<int> CloseDelimiters { get; }

        public static Layout Create(SourceFileSyntax sourceFile)
        {
            var protectedSpans = DescendantsAndSelf(sourceFile)
                .Select(ToProtectedSpan)
                .Where(span => span is not null)
                .Cast<ProtectedSpan>()
                .OrderBy(span => span.Start)
                .ToDictionary(span => span.Start);

            var pipelineOperators = new HashSet<int>();
            var multilinePipelineOperators = new HashSet<int>();
            foreach (var root in DescendantsAndSelf(sourceFile).OfType<RootExpressionSyntax>())
            {
                var stages = GetStages(root);
                var operators = DescendantsAndSelf(root)
                    .OfType<MapShorthandSyntax>()
                    .Where(shorthand => shorthand.Text.StartsWith("|>", StringComparison.Ordinal))
                    .Select(shorthand => shorthand.Span.Start)
                    .ToList();
                var multiline = operators.Any(position => HasLineBreakImmediatelyBefore(sourceFile.Text, position));
                for (var index = 1; index < stages.Count; index++)
                {
                    multiline |= ContainsStructuralLineBreak(sourceFile.Text,
                        stages[index - 1].Span.End, stages[index].Span.Start, protectedSpans.Values);
                    var operatorPosition = FindPipelineOperator(
                        sourceFile.Text, stages[index - 1].Span.End, stages[index].Span.Start, protectedSpans.Values);
                    if (operatorPosition >= 0)
                        operators.Add(operatorPosition);
                }

                pipelineOperators.UnionWith(operators);
                if (multiline)
                    multilinePipelineOperators.UnionWith(operators);
            }

            var intervals = DescendantsAndSelf(sourceFile).OfType<IntervalLiteralSyntax>()
                .Where(interval => interval.Span.Length >= 3)
                .ToArray();
            var openDelimiters = intervals.Select(interval => interval.Span.Start + 1).ToHashSet();
            var closeDelimiters = intervals.Select(interval => interval.Span.End - 1).ToHashSet();
            var multilineDelimiters = FindMultilineDelimiters(
                sourceFile.Text, protectedSpans.Values, openDelimiters, closeDelimiters);
            return new(protectedSpans, pipelineOperators, multilinePipelineOperators, multilineDelimiters,
                openDelimiters, closeDelimiters);
        }

        private static bool HasLineBreakImmediatelyBefore(string source, int position)
        {
            for (var index = position - 1; index >= 0 && char.IsWhiteSpace(source[index]); index--)
            {
                if (source[index] is '\r' or '\n')
                    return true;
            }
            return false;
        }

        private static ProtectedSpan? ToProtectedSpan(SyntaxNode node) => node switch
        {
            LineCommentSyntax => new(node.Span.Start, node.Text, TokenKind.LineComment),
            BlockCommentSyntax => new(node.Span.Start, node.Text, TokenKind.BlockComment),
            QuotedLiteralSyntax => new(node.Span.Start, node.Text, TokenKind.Atom),
            TemporalLiteralSyntax => new(node.Span.Start, node.Text, TokenKind.Atom),
            ArgumentNameSyntax argument when argument.QuotingStyle is not null
                => new(node.Span.Start, node.Text, TokenKind.Atom),
            RecordFieldNameSyntax field when field.QuotingStyle is not null
                => new(node.Span.Start, node.Text, TokenKind.Atom),
            BinaryOperatorSyntax => new(node.Span.Start, node.Text, TokenKind.BinaryOperator),
            UnaryOperatorSyntax => new(node.Span.Start, node.Text, TokenKind.UnaryOperator),
            _ => null
        };

        private static IReadOnlyList<SyntaxNode> GetStages(RootExpressionSyntax root) => root switch
        {
            ClosedExpressionSyntax closed => new SyntaxNode[] { closed.Value }.Concat(closed.Pipeline).ToArray(),
            OpenExpressionSyntax open when open.Source is not null
                => new SyntaxNode[] { open.Source }.Concat(open.Pipeline).ToArray(),
            OpenExpressionSyntax open => open.Pipeline.Cast<SyntaxNode>().ToArray(),
            _ => []
        };

        private static int FindPipelineOperator(
            string source, int start, int end, IEnumerable<ProtectedSpan> protectedSpans)
        {
            for (var position = start; position < end; position++)
            {
                if (source[position] == '|' && !IsProtected(position, protectedSpans))
                    return position;
            }
            return -1;
        }

        private static HashSet<int> FindMultilineDelimiters(
            string source,
            IEnumerable<ProtectedSpan> protectedSpans,
            IReadOnlySet<int> openDelimiters,
            IReadOnlySet<int> closeDelimiters)
        {
            var result = new HashSet<int>();
            var stack = new Stack<int>();
            for (var position = 0; position < source.Length; position++)
            {
                if (TrySkipProtected(position, protectedSpans, out var protectedEnd))
                {
                    position = protectedEnd - 1;
                    continue;
                }

                var value = source[position];
                if (openDelimiters.Contains(position) ||
                    !closeDelimiters.Contains(position) && value is '(' or '{' or '[')
                {
                    stack.Push(position);
                    continue;
                }

                if (!closeDelimiters.Contains(position) && value is not ')' and not '}' and not ']' ||
                    stack.Count == 0)
                    continue;

                var open = stack.Pop();
                if (ContainsStructuralLineBreak(source, open + 1, position, protectedSpans))
                    result.Add(open);
            }
            return result;
        }

        private static bool ContainsStructuralLineBreak(
            string source, int start, int end, IEnumerable<ProtectedSpan> protectedSpans)
        {
            for (var position = start; position < end; position++)
            {
                if (TrySkipProtected(position, protectedSpans, out var protectedEnd))
                {
                    position = protectedEnd - 1;
                    continue;
                }

                if (source[position] is '\r' or '\n')
                    return true;
            }
            return false;
        }

        private static bool IsProtected(int position, IEnumerable<ProtectedSpan> protectedSpans)
            => protectedSpans.Any(span => position >= span.Start && position < span.Start + span.Text.Length);

        private static bool TrySkipProtected(
            int position, IEnumerable<ProtectedSpan> protectedSpans, out int end)
        {
            var span = protectedSpans.FirstOrDefault(candidate => candidate.Start == position);
            end = span is null ? position : span.Start + span.Text.Length;
            return span is not null;
        }

        private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
        {
            yield return node;
            foreach (var child in node.Children)
                foreach (var descendant in DescendantsAndSelf(child))
                    yield return descendant;
        }
    }

    private sealed class FormattingWriter(DocumentFormattingOptions options, Layout layout)
    {
        private readonly StringBuilder result = new();
        private readonly Stack<bool> delimiterLayouts = new();
        private int indentation;
        private int contentEnd;
        private TokenKind? previous;

        public void Write(FormatToken token)
        {
            switch (token.Kind)
            {
                case TokenKind.OpenDelimiter:
                    WriteOpen(token);
                    break;
                case TokenKind.CloseDelimiter:
                    WriteClose(token);
                    break;
                case TokenKind.Comma:
                    TrimHorizontalWhitespace();
                    result.Append(token.Text);
                    MarkContent();
                    if (delimiterLayouts.TryPeek(out var multiline) && multiline)
                        EnsureLineBreak(indentation);
                    else
                        EnsureSpace();
                    break;
                case TokenKind.Pipeline:
                    TrimHorizontalWhitespace();
                    if (layout.MultilinePipelineOperators.Contains(token.Start))
                        EnsureLineBreak(indentation);
                    else
                        EnsureSpace();
                    result.Append(token.Text);
                    MarkContent();
                    EnsureSpace();
                    break;
                case TokenKind.BinaryOperator:
                    EnsureSpace();
                    result.Append(token.Text);
                    MarkContent();
                    EnsureSpace();
                    break;
                case TokenKind.UnaryOperator:
                    WriteAtom(token, addSpaceAfter: false);
                    break;
                case TokenKind.LineComment:
                    WriteComment(token);
                    EnsureLineBreak(indentation);
                    break;
                case TokenKind.BlockComment:
                    WriteComment(token);
                    break;
                default:
                    WriteAtom(token, addSpaceAfter: false);
                    break;
            }
            previous = token.Kind;
        }

        public string Complete()
        {
            result.Length = contentEnd;
            if (options.InsertFinalNewLine)
                result.Append(options.NewLine);
            return result.ToString();
        }

        private void WriteOpen(FormatToken token)
        {
            if (token.LineBreakBefore)
                EnsureLineBreak(indentation);
            result.Append(token.Text);
            MarkContent();
            var multiline = layout.MultilineDelimiters.Contains(token.Start);
            delimiterLayouts.Push(multiline);
            indentation++;
            if (multiline)
                EnsureLineBreak(indentation);
        }

        private void WriteClose(FormatToken token)
        {
            indentation = Math.Max(0, indentation - 1);
            var multiline = delimiterLayouts.Count > 0 && delimiterLayouts.Pop();
            if (multiline)
                EnsureLineBreak(indentation);
            else
                TrimHorizontalWhitespace();
            result.Append(token.Text);
            MarkContent();
        }

        private void WriteAtom(FormatToken token, bool addSpaceAfter)
        {
            if (token.LineBreakBefore)
                EnsureLineBreak(indentation);
            else if (previous is TokenKind.Atom or TokenKind.CloseDelimiter or TokenKind.BinaryOperator
                     or TokenKind.LineComment or TokenKind.BlockComment)
                EnsureSpace();

            result.Append(token.Text);
            MarkContent();
            if (addSpaceAfter)
                EnsureSpace();
        }

        private void WriteComment(FormatToken token)
        {
            if (token.LineBreakBefore)
                EnsureLineBreak(indentation);
            else if (result.Length > 0 && !IsAtLineStart() && previous is not TokenKind.OpenDelimiter)
                EnsureSpace();
            result.Append(token.Text);
            MarkContent();
        }

        private void EnsureSpace()
        {
            if (result.Length > 0 && !char.IsWhiteSpace(result[^1]))
                result.Append(' ');
        }

        private void EnsureLineBreak(int level)
        {
            TrimHorizontalWhitespace();
            if (result.Length > 0 && !EndsWithLineBreak())
                result.Append(options.NewLine);
            if (result.Length > 0)
                result.Append(string.Concat(Enumerable.Repeat(options.Indentation, level)));
        }

        private void TrimHorizontalWhitespace()
        {
            while (result.Length > 0 && result[^1] is ' ' or '\t')
                result.Length--;
        }

        private bool EndsWithLineBreak()
            => result.Length > 0 && result[^1] is '\r' or '\n';

        private bool IsAtLineStart()
            => result.Length == 0 || EndsWithLineBreak();

        private void MarkContent() => contentEnd = result.Length;
    }

    private sealed record ProtectedSpan(int Start, string Text, TokenKind Kind);
    private sealed record FormatToken(TokenKind Kind, string Text, int Start, bool LineBreakBefore);

    private enum TokenKind
    {
        Atom,
        OpenDelimiter,
        CloseDelimiter,
        Comma,
        Pipeline,
        BinaryOperator,
        UnaryOperator,
        LineComment,
        BlockComment
    }
}
