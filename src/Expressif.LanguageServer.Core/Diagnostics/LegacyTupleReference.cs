namespace Expressif.LanguageServer.Core.Diagnostics;

/// <summary>A deprecated reference and its optional safe replacement, with UTF-16 source offsets.</summary>
public sealed record LegacyTupleReference(string Text, int Start, int Length, string? Replacement)
{
    public string Message => $"Tuple reference '{Text}' is deprecated." +
        (Replacement is null
            ? " Use dollar-minus notation; the equivalent index exceeds the supported range."
            : $" Use '{Replacement}' instead.");

    public bool Overlaps(int start, int length) => length == 0
        ? start >= Start && start <= Start + Length
        : start < Start + Length && (long)start + length > Start;
}
