using Expressif.LanguageServer.Core.Documents;
using Expressif.Syntax;

namespace Expressif.LanguageServer.Core.Folding;

public sealed class DocumentFoldingService : IDocumentFoldingService
{
    public IReadOnlyList<DocumentFoldingRange> GetRanges(DocumentSnapshot document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SyntaxDocument is null || document.Text.Length == 0)
            return [];

        var lines = new LineMap(document.Text);
        var candidates = new List<Candidate>();

        foreach (var node in DescendantsAndSelf(document.SyntaxDocument))
        {
            switch (node)
            {
                case RootExpressionSyntax expression:
                    AddPipeline(candidates, expression);
                    break;
                case InputBindingExpressionSyntax binding:
                    AddInputBinding(candidates, binding, document.Text);
                    break;
                case BlockCommentSyntax comment:
                    candidates.Add(new(comment.Span.Start, comment.Span.End, FoldingKind.Comment));
                    break;
                default:
                    AddDelimited(candidates, node);
                    break;
            }
        }

        return candidates
            .Where(candidate => candidate.Start >= 0 && candidate.End <= document.Text.Length &&
                                candidate.Start < candidate.End)
            .Select(candidate => new DocumentFoldingRange(
                lines.GetPosition(candidate.Start),
                lines.GetPosition(candidate.End),
                candidate.Kind))
            .Where(range => range.Start.Line < range.End.Line)
            .Distinct()
            .OrderBy(range => range.Start.Line)
            .ThenBy(range => range.Start.Character)
            .ThenByDescending(range => range.End.Line)
            .ThenByDescending(range => range.End.Character)
            .ToArray();
    }

    private static void AddPipeline(ICollection<Candidate> ranges, RootExpressionSyntax expression)
    {
        var stages = expression switch
        {
            ClosedExpressionSyntax closed => new SyntaxNode[] { closed.Value }.Concat(closed.Pipeline).ToArray(),
            OpenExpressionSyntax open when open.Source is not null
                => new SyntaxNode[] { open.Source }.Concat(open.Pipeline).ToArray(),
            OpenExpressionSyntax open => open.Pipeline.Cast<SyntaxNode>().ToArray(),
            _ => []
        };

        if (stages.Length > 1)
            ranges.Add(new(stages[0].Span.End, stages[^1].Span.End));
    }

    private static void AddInputBinding(
        ICollection<Candidate> ranges,
        InputBindingExpressionSyntax binding,
        string text)
    {
        var introductionEnd = FindInputBindingIntroductionEnd(binding, text);
        if (introductionEnd >= 0)
            ranges.Add(new(introductionEnd, binding.Body.Span.End));
    }

    private static int FindInputBindingIntroductionEnd(InputBindingExpressionSyntax binding, string text)
    {
        var searchStart = Math.Max(binding.Span.Start, binding.Binding?.Span.End ?? binding.Span.Start);
        var searchEnd = Math.Min(binding.Body.Span.Start, text.Length);
        for (var position = searchStart; position + 1 < searchEnd; position++)
        {
            if (text[position] == ':' && text[position + 1] == '>')
                return position + 2;
        }

        return -1;
    }

    private static void AddDelimited(ICollection<Candidate> ranges, SyntaxNode node)
    {
        var delimiters = node switch
        {
            FunctionCallSyntax { HasParentheses: true } => ('(', ')'),
            ParenthesizedExpressionSyntax => ('(', ')'),
            TupleLiteralSyntax => ('(', ')'),
            PairLiteralSyntax => ('(', ')'),
            PositionalBindingPatternSyntax => ('(', ')'),
            ArrayLiteralSyntax => ('{', '}'),
            RecordLiteralSyntax => ('{', '}'),
            GroupingLiteralSyntax => ('{', '}'),
            DictionaryLiteralSyntax => ('{', '}'),
            ParameterizedExpressionSyntax => ('{', '}'),
            _ => ((char Open, char Close)?)null
        };

        if (delimiters is not { } pair)
            return;

        var relativeOpen = node.Text.IndexOf(pair.Open);
        var relativeClose = node.Text.LastIndexOf(pair.Close);
        if (relativeOpen >= 0 && relativeClose > relativeOpen)
            ranges.Add(new(node.Span.Start + relativeOpen, node.Span.Start + relativeClose + 1));
    }

    private static IEnumerable<SyntaxNode> DescendantsAndSelf(SyntaxNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        foreach (var descendant in DescendantsAndSelf(child))
            yield return descendant;
    }

    private readonly record struct Candidate(int Start, int End, FoldingKind Kind = FoldingKind.Region);

    private sealed class LineMap
    {
        private readonly int[] starts;

        public LineMap(string text)
        {
            var lineStarts = new List<int> { 0 };
            for (var position = 0; position < text.Length; position++)
            {
                if (text[position] == '\n')
                    lineStarts.Add(position + 1);
            }

            starts = lineStarts.ToArray();
        }

        public FoldingPosition GetPosition(int offset)
        {
            var index = Array.BinarySearch(starts, offset);
            var line = index >= 0 ? index : ~index - 1;
            return new(line, offset - starts[line]);
        }
    }
}
