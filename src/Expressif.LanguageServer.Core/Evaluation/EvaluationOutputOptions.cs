namespace Expressif.LanguageServer.Core.Evaluation;

public enum EvaluationOutputFormatting
{
    Compact,
    Pretty,
}

public sealed record EvaluationOutputOptions(
    EvaluationOutputFormatting Formatting = EvaluationOutputFormatting.Compact,
    int Indent = 2);
