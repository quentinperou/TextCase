using System.Globalization;
using System.Text;

namespace TextCase.Core;

/// <summary>
/// The conversions of the popup. Pure functions of the text: line breaks, indentation and punctuation are kept, so that
/// the result pasted over the selection only changes what the conversion is about.
/// </summary>
public static class CaseConverter
{
    public static string Convert(string text, CaseMode mode, CaseRules? rules = null)
    {
        rules ??= CaseRules.Default;
        return mode switch
        {
            CaseMode.Upper => text.ToUpperInvariant(),
            CaseMode.Lower => text.ToLowerInvariant(),
            CaseMode.Title => rules.Smart ? Smart(text, rules, title: true) : SimpleTitle(text),
            CaseMode.Sentence => rules.Smart ? Smart(text, rules, title: false) : SimpleSentence(text),
            CaseMode.Camel or CaseMode.Pascal or CaseMode.Constant or CaseMode.Param or CaseMode.Snake or CaseMode.Dot
                or CaseMode.NoCase => PerLine(text, line => Code(line, mode)),
            CaseMode.Toggle => Toggle(text),
            CaseMode.Alternating => Alternate(text),
            CaseMode.NoAccents => RemoveAccents(text),
            CaseMode.CleanSpaces => CleanSpaces(text),
            CaseMode.JoinLines => JoinLines(text),
            _ => text,
        };
    }

    // ---------------------------------------------------------------- Title Case and Sentence case

    /// <summary>
    /// Title Case (every word capitalized, the small words of a title in lowercase) or Sentence case (only the start of
    /// each sentence and of each line). In both, what cannot be guessed is kept: the words of the list
    /// (<see cref="CaseRules"/>), words in mixed case (iPhone, McDonald), words with digits (R6, 24-70mm), Roman
    /// numerals (Mark II), and the acronyms of a line that is not written in capitals.
    /// </summary>
    private static string Smart(string text, CaseRules rules, bool title)
    {
        var result = new StringBuilder(text.Length);
        bool start = true;
        bool capsLine = IsCapsLine(text, 0);
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (c is '\n' or '\r')
            {
                result.Append(c);
                start = true;
                i++;
                if (c == '\n' || i >= text.Length || text[i] != '\n')
                    capsLine = IsCapsLine(text, i);
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                result.Append(c);
                i++;
                continue;
            }
            int end = i;
            while (end < text.Length && !char.IsWhiteSpace(text[end]))
                end++;
            result.Append(SmartToken(text[i..end], rules, title, capsLine, ref start));
            i = end;
        }
        return result.ToString();
    }

    /// <summary>
    /// A line "in capitals": fewer than a quarter of its letters in lowercase. Its words in capitals are then words like
    /// the others, not acronyms ("CANON EOS R6" from a supplier's list).
    /// </summary>
    private static bool IsCapsLine(string text, int from)
    {
        int letters = 0, lower = 0;
        for (int i = from; i < text.Length && text[i] is not ('\n' or '\r'); i++)
        {
            if (!char.IsLetter(text[i]))
                continue;
            letters++;
            if (char.IsLower(text[i]))
                lower++;
        }
        return lower * 4 < letters;
    }

    // One word with its punctuation ("(Canon," "l'appareil" "24-70MM"). start: it opens a sentence (or a title).
    private static string SmartToken(string token, CaseRules rules, bool title, bool capsLine, ref bool start)
    {
        int a = 0;
        while (a < token.Length && !char.IsLetterOrDigit(token[a]))
            a++;
        int b = token.Length;
        while (b > a && !char.IsLetterOrDigit(token[b - 1]))
            b--;
        if (a == b)
        {
            // "!" alone, as French typography writes it, ends a sentence; a dash or a colon alone between two parts of a
            // title starts the next part as a title.
            if (token.IndexOfAny(['.', '!', '?', '…']) >= 0 || (title && token is "-" or "–" or "—" or ":" or "|"))
                start = true;
            return token;
        }

        string core = token[a..b];
        string converted = rules.TryKeep(core, out var spelling) ? spelling : SmartCore(core, rules, title, capsLine, start);
        string after = token[b..];
        start = after.IndexOfAny(['.', '!', '?', '…']) >= 0 || (title && after.Contains(':'));
        return token[..a] + converted + after;
    }

    private static string SmartCore(string core, CaseRules rules, bool title, bool capsLine, bool start)
    {
        var result = new StringBuilder(core.Length);
        bool firstSegment = true;
        string? previous = null;
        int i = 0;
        while (i < core.Length)
        {
            if (!char.IsLetter(core[i]))
            {
                result.Append(core[i]);
                i++;
                continue;
            }
            int end = i;
            while (end < core.Length && (char.IsLetter(core[end]) || IsMark(core[end])))
                end++;
            string segment = core[i..end];
            bool afterApostrophe = i > 0 && IsApostrophe(core[i - 1]);
            bool beforeApostrophe = end < core.Length && IsApostrophe(core[end]);
            // Letters against a digit belong to a model or a measure: R6, A7IV, 70MM, 1er (not the "f" of "f/2.8").
            bool withDigit = (i > 0 && char.IsDigit(core[i - 1])) || (end < core.Length && char.IsDigit(core[end]));
            // "don't", "it's", "aujourd'hui": the end of the same word. After "l'", "d'", "qu'": a new word.
            bool continuation = afterApostrophe && previous is not null && !CaseRules.IsElision(previous);
            bool first = start && firstSegment;

            string converted;
            if (rules.TryKeep(segment, out var spelling))
                converted = spelling;
            else if (IsMixedCase(segment) || withDigit || (!capsLine && segment.Length >= 2 && IsAllUpper(segment)))
                converted = segment;
            else if (CaseRules.IsRomanNumeral(segment) && !continuation)
                converted = segment.ToUpperInvariant();
            else if (continuation)
                converted = segment.ToLowerInvariant();
            else if (title)
            {
                bool small = rules.SmallWordsLowercase
                    && (CaseRules.IsSmallWord(segment) || (beforeApostrophe && CaseRules.IsElision(segment)));
                converted = small && !first ? segment.ToLowerInvariant() : Capitalize(segment);
            }
            else if (first)
                converted = Capitalize(segment);
            else if (segment is "i" or "I" && (core.Length == 1 || beforeApostrophe))
                converted = "I"; // English "I", "I'm"
            else
                converted = segment.ToLowerInvariant();

            result.Append(converted);
            firstSegment = false;
            previous = segment;
            i = end;
        }
        return result.ToString();
    }

    private static bool IsApostrophe(char c) => c is '\'' or '’';

    private static bool IsMark(char c) => CharUnicodeInfo.GetUnicodeCategory(c)
        is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    // A capital after a lowercase letter or after the first letter: iPhone, McDonald, PowerShot.
    private static bool IsMixedCase(string word) =>
        word.Any(char.IsLower) && word.Skip(1).Any(char.IsUpper);

    private static bool IsAllUpper(string word) => !word.Any(char.IsLower);

    private static string Capitalize(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();

    /// <summary>
    /// Title Case without the smart rules: the first letter of each word in capital (a word starts after a space, a
    /// dash or a slash: Saint-Jean, Noir/Blanc), every other letter in lowercase. A word starting with a digit keeps its
    /// letters in lowercase (35mm).
    /// </summary>
    private static string SimpleTitle(string text)
    {
        var chars = text.ToLowerInvariant().ToCharArray();
        bool wordStart = true;
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (char.IsWhiteSpace(c) || c is '-' or '–' or '—' or '/')
                wordStart = true;
            else if (char.IsLetterOrDigit(c))
            {
                if (wordStart && char.IsLetter(c))
                    chars[i] = char.ToUpperInvariant(c);
                wordStart = false;
            }
        }
        return new string(chars);
    }

    /// <summary>
    /// Sentence case without the smart rules: everything in lowercase but the first letter of the text, of each line,
    /// and after . ! ? followed by a space.
    /// </summary>
    private static string SimpleSentence(string text)
    {
        var chars = text.ToLowerInvariant().ToCharArray();
        bool start = true;
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c is '\n' or '\r')
                start = true;
            else if (c is '.' or '!' or '?' or '…')
            {
                if (i + 1 >= chars.Length || char.IsWhiteSpace(chars[i + 1]))
                    start = true;
            }
            else if (char.IsLetterOrDigit(c))
            {
                if (start && char.IsLetter(c))
                    chars[i] = char.ToUpperInvariant(c);
                start = false;
            }
        }
        return new string(chars);
    }

    // ---------------------------------------------------------------- Code formats

    /// <summary>Applies <paramref name="convert"/> to each line, keeping the line breaks as they are.</summary>
    private static string PerLine(string text, Func<string, string> convert)
    {
        var result = new StringBuilder(text.Length);
        int i = 0;
        while (i <= text.Length)
        {
            int end = text.IndexOfAny(['\r', '\n'], i);
            if (end < 0)
                end = text.Length;
            result.Append(convert(text[i..end]));
            if (end == text.Length)
                break;
            int breakLength = text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? 2 : 1;
            result.Append(text, end, breakLength);
            i = end + breakLength;
        }
        return result.ToString();
    }

    // One line: its words joined in the format; the indentation and the spaces at its end stay.
    private static string Code(string line, CaseMode mode)
    {
        int a = 0;
        while (a < line.Length && char.IsWhiteSpace(line[a]))
            a++;
        int b = line.Length;
        while (b > a && char.IsWhiteSpace(line[b - 1]))
            b--;
        var words = Words(line[a..b]);
        if (words.Count == 0)
            return line;

        string body = mode switch
        {
            CaseMode.Camel => string.Concat(words.Select((w, i) => i == 0 ? w.ToLowerInvariant() : Capitalize(w))),
            CaseMode.Pascal => string.Concat(words.Select(Capitalize)),
            CaseMode.Constant => string.Join('_', words.Select(w => w.ToUpperInvariant())),
            CaseMode.Param => string.Join('-', words.Select(w => w.ToLowerInvariant())),
            CaseMode.Snake => string.Join('_', words.Select(w => w.ToLowerInvariant())),
            CaseMode.Dot => string.Join('.', words.Select(w => w.ToLowerInvariant())),
            _ => string.Join(' ', words.Select(w => w.ToLowerInvariant())),
        };
        return line[..a] + body + line[b..];
    }

    /// <summary>
    /// The words of a line for the code formats: split on anything but letters and digits, and inside a word between a
    /// lowercase letter or a digit and a capital (camelCase, R6Mark), and before the last capital of an acronym followed by
    /// lowercase letters (HTTPServer → HTTP Server).
    /// </summary>
    public static List<string> Words(string line)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (!char.IsLetterOrDigit(c) && !IsMark(c))
            {
                Flush();
                continue;
            }
            if (current.Length > 0 && char.IsUpper(c))
            {
                char previous = line[i - 1];
                bool hump = char.IsLower(previous) || char.IsDigit(previous);
                bool acronymEnd = char.IsUpper(previous) && i + 1 < line.Length && char.IsLower(line[i + 1]);
                if (hump || acronymEnd)
                    Flush();
            }
            current.Append(c);
        }
        Flush();
        return words;

        void Flush()
        {
            if (current.Length > 0)
                words.Add(current.ToString());
            current.Clear();
        }
    }

    // ---------------------------------------------------------------- Other

    private static string Toggle(string text)
    {
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsUpper(chars[i]))
                chars[i] = char.ToLowerInvariant(chars[i]);
            else if (char.IsLower(chars[i]))
                chars[i] = char.ToUpperInvariant(chars[i]);
        }
        return new string(chars);
    }

    // aLtErNaTiNg: one letter in two in capitals, counted over the whole text (spaces and digits do not count).
    private static string Alternate(string text)
    {
        var chars = text.ToCharArray();
        int n = 0;
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetter(chars[i]))
                continue;
            chars[i] = n++ % 2 == 0 ? char.ToLowerInvariant(chars[i]) : char.ToUpperInvariant(chars[i]);
        }
        return new string(chars);
    }

    private static readonly Dictionary<char, string> Unaccented = new()
    {
        ['œ'] = "oe", ['Œ'] = "OE", ['æ'] = "ae", ['Æ'] = "AE", ['ß'] = "ss", ['ø'] = "o", ['Ø'] = "O",
        ['đ'] = "d", ['Đ'] = "D", ['ł'] = "l", ['Ł'] = "L", ['ı'] = "i",
    };

    /// <summary>
    /// The accents of Latin letters removed (é → e, ç → c, œ → oe). The marks of other scripts are needed to read them
    /// and stay.
    /// </summary>
    private static string RemoveAccents(string text)
    {
        string decomposed = text.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        char baseLetter = '\0';
        foreach (char c in decomposed)
        {
            bool mark = CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark;
            if (mark && IsLatin(baseLetter))
                continue;
            if (Unaccented.TryGetValue(c, out var replacement))
                result.Append(replacement);
            else
                result.Append(c);
            if (!mark)
                baseLetter = c;
        }
        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool IsLatin(char c) => c is >= 'A' and <= 'ɏ' or >= 'Ḁ' and <= 'ỿ';

    // ---------------------------------------------------------------- Clean-ups

    /// <summary>
    /// Extra spaces removed: runs of spaces and tabs become one space, spaces at the start and end of the lines go,
    /// blank lines at the start and end go, and several blank lines in a row become one. A line break at the very end
    /// stays (the selection of whole lines).
    /// </summary>
    private static string CleanSpaces(string text)
    {
        var lines = SplitLines(text).Select(CollapseSpaces).ToList();
        var kept = new List<string>();
        foreach (var line in lines)
        {
            if (line.Length == 0 && (kept.Count == 0 || kept[^1].Length == 0))
                continue;
            kept.Add(line);
        }
        while (kept.Count > 0 && kept[^1].Length == 0)
            kept.RemoveAt(kept.Count - 1);
        return WithFinalBreak(string.Join(NewLineOf(text), kept), text);
    }

    private static string CollapseSpaces(string line)
    {
        var result = new StringBuilder(line.Length);
        foreach (char c in line)
        {
            bool space = c is ' ' or '\t';
            if (space && (result.Length == 0 || result[^1] == ' '))
                continue;
            result.Append(space ? ' ' : c);
        }
        if (result.Length > 0 && result[^1] == ' ')
            result.Length--;
        return result.ToString();
    }

    /// <summary>
    /// The lines joined into one, separated by a space (a word cut by a hyphen at the end of a line is joined without
    /// one: "photo-" + "graphie"). A line break at the very end stays.
    /// </summary>
    private static string JoinLines(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var line in SplitLines(text).Select(CollapseSpaces).Where(l => l.Length > 0))
        {
            if (result.Length > 0 && !(result[^1] == '-' && char.IsLower(line[0])))
                result.Append(' ');
            result.Append(line);
        }
        return WithFinalBreak(result.ToString(), text);
    }

    private static string[] SplitLines(string text)
    {
        string body = text.TrimEnd('\r', '\n');
        return body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private static string NewLineOf(string text) =>
        text.Contains("\r\n") ? "\r\n" : text.Contains('\n') ? "\n" : text.Contains('\r') ? "\r" : Environment.NewLine;

    private static string WithFinalBreak(string result, string original) =>
        result.Length > 0 && (original.EndsWith('\n') || original.EndsWith('\r')) ? result + NewLineOf(original) : result;
}
