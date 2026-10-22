namespace TextCase.Core;

/// <summary>
/// The conversions, in the order of the popup: the cases of the Chrome extension "Change Case" (the author's reference),
/// then alternating case and the clean-ups he asked for.
/// </summary>
public enum CaseMode
{
    Upper,
    Lower,
    Title,
    Sentence,
    Camel,
    Pascal,
    Constant,
    Param,
    Snake,
    Dot,
    Toggle,
    Alternating,
    NoAccents,
    NoCase,
    CleanSpaces,
    JoinLines,
    PlainText,
}

/// <summary>The groups of the popup, split by a line, as in the extension's menu.</summary>
public enum CaseGroup
{
    Case,
    Code,
    Separators,
    Other,
    Cleanup,
}

public static class CaseModes
{
    public static IReadOnlyList<CaseMode> All { get; } = Enum.GetValues<CaseMode>();

    public static CaseGroup GroupOf(CaseMode mode) => mode switch
    {
        CaseMode.Upper or CaseMode.Lower or CaseMode.Title or CaseMode.Sentence => CaseGroup.Case,
        CaseMode.Camel or CaseMode.Pascal or CaseMode.Constant => CaseGroup.Code,
        CaseMode.Param or CaseMode.Snake or CaseMode.Dot => CaseGroup.Separators,
        CaseMode.Toggle or CaseMode.Alternating or CaseMode.NoAccents or CaseMode.NoCase => CaseGroup.Other,
        _ => CaseGroup.Cleanup,
    };

    /// <summary>
    /// The key of the n-th row of the popup: 1 to 9, then A to Z (on an AZERTY keyboard too: the digit keys are read
    /// without Shift).
    /// </summary>
    public static char? KeyOf(int index) => index switch
    {
        < 0 => null,
        < 9 => (char)('1' + index),
        < 9 + 26 => (char)('A' + index - 9),
        _ => null,
    };

    /// <summary>The row of a key typed in the popup (see <see cref="KeyOf"/>), or -1.</summary>
    public static int IndexOfKey(char key) => char.ToUpperInvariant(key) switch
    {
        >= '1' and <= '9' and var c => c - '1',
        >= 'A' and <= 'Z' and var c => 9 + c - 'A',
        _ => -1,
    };
}
