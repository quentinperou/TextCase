using System.Diagnostics.CodeAnalysis;

namespace TextCase.Core;

[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// A keyboard shortcut: modifiers and one key, as a Windows virtual-key code. Saved in the settings as text,
/// "Ctrl+Alt+C" (the names of the keys in English, whatever the keyboard; the app shows them in the UI language).
/// </summary>
public readonly record struct Shortcut(ShortcutModifiers Modifiers, int Key)
{
    public static Shortcut None => default;

    public bool IsEmpty => Key == 0;

    /// <summary>
    /// Usable as a global shortcut: with Ctrl, Alt or Win (Shift alone would take a capital letter from every app), or an
    /// F key. Never a modifier key alone.
    /// </summary>
    public bool IsValid =>
        Key != 0 && !IsModifierKey(Key)
        && ((Modifiers & (ShortcutModifiers.Ctrl | ShortcutModifiers.Alt | ShortcutModifiers.Win)) != 0 || IsFunctionKey(Key));

    public static bool IsFunctionKey(int key) => key is >= 0x70 and <= 0x87;

    /// <summary>Shift, Ctrl, Alt, Win (left, right or either), and the lock keys.</summary>
    public static bool IsModifierKey(int key) => key is 0x10 or 0x11 or 0x12 or (>= 0xA0 and <= 0xA5) or 0x5B or 0x5C or 0x14 or 0x90 or 0x91;

    public override string ToString() =>
        IsEmpty ? "" : string.Join('+', ModifierNames(Modifiers).Append(KeyName(Key)));

    public static IEnumerable<string> ModifierNames(ShortcutModifiers modifiers)
    {
        if (modifiers.HasFlag(ShortcutModifiers.Ctrl)) yield return "Ctrl";
        if (modifiers.HasFlag(ShortcutModifiers.Alt)) yield return "Alt";
        if (modifiers.HasFlag(ShortcutModifiers.Shift)) yield return "Shift";
        if (modifiers.HasFlag(ShortcutModifiers.Win)) yield return "Win";
    }

    /// <summary>"Ctrl+Alt+C"; an empty or unknown text gives <see cref="None"/>.</summary>
    public static Shortcut Parse(string? text) => TryParse(text, out var shortcut) ? shortcut : None;

    public static bool TryParse([NotNullWhen(true)] string? text, out Shortcut shortcut)
    {
        shortcut = None;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        var modifiers = ShortcutModifiers.None;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var modifier = parts[i].ToLowerInvariant() switch
            {
                "ctrl" or "control" => ShortcutModifiers.Ctrl,
                "alt" => ShortcutModifiers.Alt,
                "shift" => ShortcutModifiers.Shift,
                "win" or "windows" => ShortcutModifiers.Win,
                _ => (ShortcutModifiers?)null,
            };
            if (modifier is null)
                return false;
            modifiers |= modifier.Value;
        }
        int key = KeyCode(parts[^1]);
        if (key == 0)
            return false;
        shortcut = new Shortcut(modifiers, key);
        return true;
    }

    private static readonly (int Code, string Name)[] Named =
    [
        (0x08, "Backspace"), (0x09, "Tab"), (0x0D, "Enter"), (0x13, "Pause"), (0x1B, "Esc"), (0x20, "Space"),
        (0x21, "PageUp"), (0x22, "PageDown"), (0x23, "End"), (0x24, "Home"), (0x25, "Left"), (0x26, "Up"), (0x27, "Right"),
        (0x28, "Down"), (0x2C, "PrintScreen"), (0x2D, "Insert"), (0x2E, "Delete"),
        (0x6A, "Multiply"), (0x6B, "Add"), (0x6D, "Subtract"), (0x6E, "Decimal"), (0x6F, "Divide"),
    ];

    /// <summary>The English name of a virtual key: "C", "7", "F5", "Num4", "Space"; "VK_BA" for the others.</summary>
    public static string KeyName(int key)
    {
        if (key is >= 0x41 and <= 0x5A or >= 0x30 and <= 0x39)
            return ((char)key).ToString();
        if (IsFunctionKey(key))
            return $"F{key - 0x6F}";
        if (key is >= 0x60 and <= 0x69)
            return $"Num{key - 0x60}";
        foreach (var (code, name) in Named)
        {
            if (code == key)
                return name;
        }
        return $"VK_{key:X2}";
    }

    public static int KeyCode(string name)
    {
        if (name.Length == 1 && char.ToUpperInvariant(name[0]) is var c && (c is >= 'A' and <= 'Z' or >= '0' and <= '9'))
            return c;
        if (name.Length is 2 or 3 && name[0] is 'F' or 'f' && int.TryParse(name[1..], out int f) && f is >= 1 and <= 24)
            return 0x6F + f;
        if (name.StartsWith("Num", StringComparison.OrdinalIgnoreCase) && name.Length == 4 && char.IsAsciiDigit(name[3]))
            return 0x60 + (name[3] - '0');
        foreach (var (code, known) in Named)
        {
            if (string.Equals(known, name, StringComparison.OrdinalIgnoreCase))
                return code;
        }
        if (name.StartsWith("VK_", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(name[3..], System.Globalization.NumberStyles.HexNumber, null, out int vk) && vk is > 0 and < 0xFF)
            return vk;
        return 0;
    }
}

/// <summary>
/// Ctrl+C twice in a row, as DeepL: two presses within <see cref="DelayMs"/> trigger once. Fed by the keyboard hook
/// with real presses only (not the auto-repeat of a held key).
/// </summary>
public sealed class DoublePress
{
    private long? _first;
    private long? _last;

    public int DelayMs { get; set; } = 500;

    /// <summary>The time since the press before the last one, if any (for the log: a double press a little too slow).</summary>
    public long? LastGapMs { get; private set; }

    /// <summary>A press at <paramref name="timeMs"/> (any clock in milliseconds); true when it is the second one.</summary>
    public bool Press(long timeMs)
    {
        LastGapMs = _last is long last && timeMs >= last ? timeMs - last : null;
        _last = timeMs;
        if (_first is long first && timeMs >= first && timeMs - first <= DelayMs)
        {
            _first = null;
            return true;
        }
        _first = timeMs;
        return false;
    }

    public void Reset()
    {
        _first = null;
        _last = null;
    }
}
