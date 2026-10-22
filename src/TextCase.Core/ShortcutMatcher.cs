namespace TextCase.Core;

/// <summary>A key pressed or released, as the low-level keyboard hook sees it.</summary>
/// <param name="Own">Sent by TextCase itself (its Ctrl+C / Ctrl+V): never a shortcut.</param>
public readonly record struct KeyEvent(int Vk, bool Down, bool Own = false);

public enum TriggerKind
{
    /// <summary>The popup shortcut: TextCase copies the selection, shows the popup, pastes the result.</summary>
    Popup,

    /// <summary>A direct shortcut: the selection converted at once, no popup.</summary>
    Direct,

    /// <summary>Ctrl+C twice: the app has just copied the selection itself, the popup opens on it.</summary>
    DoubleCopy,
}

public readonly record struct Trigger(TriggerKind Kind, CaseMode Mode = default);

/// <summary>A key for the open popup: a move, Enter, Escape, the key of a row (1–9, A–Z), or 0 for the Smart switch.</summary>
public enum PopupKey
{
    Up,
    Down,
    Home,
    End,
    Choose,
    Cancel,
    Row,

    /// <summary>0: the smart cases on or off.</summary>
    Smart,
}

/// <summary>
/// What the keyboard hook does with each key, on the hook thread: a shortcut of TextCase is taken (the app in front never
/// sees it) and reported; Ctrl+C is only watched (twice in a row opens the popup); while the popup is open, its keys are
/// taken for it and any other key closes it. It only reads its own fields and raises events: the work is done on the UI
/// thread.
/// <para>
/// The keys held are followed from the hook's own events: in the remote desktop window, the client takes the keys for the
/// remote PC and Windows' own key state never sees them. A release can be missed (the remote desktop client had the keys
/// for a moment, Win + L locked the session): a key pressed again more than a second after its last press is a new
/// press, not the auto-repeat of a key held down; and a modifier seen long ago that Windows says is up is forgotten.
/// Otherwise one missed release would stop every shortcut.
/// </para>
/// </summary>
public sealed class ShortcutMatcher
{
    private const int VkLShift = 0xA0, VkRShift = 0xA1, VkLCtrl = 0xA2, VkRCtrl = 0xA3, VkLAlt = 0xA4, VkRAlt = 0xA5;
    private const int VkLWin = 0x5B, VkRWin = 0x5C, VkC = 0x43;
    private static readonly int[] ModifierKeys = [VkLShift, VkRShift, VkLCtrl, VkRCtrl, VkLAlt, VkRAlt, VkLWin, VkRWin];

    /// <summary>Longer than the first auto-repeat delay of Windows (1 s at most): a new press, not a repeat.</summary>
    private const long RepeatWindowMs = 1100;

    /// <summary>A modifier pressed longer ago than this, and up for Windows, lost its release.</summary>
    private const long StaleModifierMs = 2000;

    private readonly bool[] _down = new bool[256];
    private readonly long[] _lastDown = new long[256];
    // Keys taken when pressed: their release (and their auto-repeat) is taken too.
    private readonly bool[] _taken = new bool[256];
    private readonly DoublePress _doubleCopy = new();
    private readonly Func<long> _now;
    private readonly Func<int, bool>? _isKeyDown;
    private volatile Config _config = new(Shortcut.None, new Dictionary<Shortcut, CaseMode>(), false, 500, false);
    private volatile bool _popupOpen;
    private volatile bool _suspended;

    /// <param name="clock">Milliseconds (the tests give their own clock).</param>
    /// <param name="isKeyDown">Windows' state of a key (GetAsyncKeyState), to forget a modifier whose release was missed.</param>
    public ShortcutMatcher(Func<long>? clock = null, Func<int, bool>? isKeyDown = null)
    {
        _now = clock ?? (() => Environment.TickCount64);
        _isKeyDown = isKeyDown;
    }

    /// <summary>The shortcuts of the settings; replaced as a whole when they change.</summary>
    public sealed record Config(Shortcut Popup, IReadOnlyDictionary<Shortcut, CaseMode> Direct, bool DoubleCopy, int DoubleCopyDelay, bool Paused);

    public Config Current
    {
        get => _config;
        set => _config = value;
    }

    /// <summary>The popup is open: its keys go to it.</summary>
    public bool PopupOpen
    {
        get => _popupOpen;
        set => _popupOpen = value;
    }

    /// <summary>No shortcut at all, while the user types a new one in the settings.</summary>
    public bool Suspended
    {
        get => _suspended;
        set => _suspended = value;
    }

    /// <summary>A shortcut was pressed (raised on the hook thread).</summary>
    public event Action<Trigger>? Triggered;

    /// <summary>A key for the open popup, with the row key typed (raised on the hook thread).</summary>
    public event Action<PopupKey, char>? PopupKeyPressed;

    /// <summary>A shortcut with Alt or Win was taken: its release must be masked (raised on the hook thread).</summary>
    public event Action? MaskNeeded;

    /// <summary>Something worth a line in the log: a Ctrl+C twice a little too slow, a release missed.</summary>
    public event Action<string>? Diagnostic;

    /// <summary>Each key pressed with Ctrl, Alt or Win goes to <see cref="Diagnostic"/> too, with the state (debugging).</summary>
    public bool Trace { get; set; }

    /// <summary>A modifier is held down by the user (Ctrl, Alt, Shift, Win).</summary>
    public bool AnyModifierDown => Modifiers() != ShortcutModifiers.None;

    private long _lastKeyAt;

    /// <summary>When the hook last saw a key of the user (on the clock of <see cref="ShortcutMatcher(Func{long}, Func{int, bool})"/>).</summary>
    public long LastKeyAt => Interlocked.Read(ref _lastKeyAt);

    /// <summary>Called by the hook for each key; true takes it.</summary>
    public bool OnKey(KeyEvent e)
    {
        if (e.Own)
            return false;
        Interlocked.Exchange(ref _lastKeyAt, _now());
        int vk = e.Vk & 0xFF;
        if (!e.Down)
        {
            _down[vk] = false;
            bool taken = _taken[vk];
            _taken[vk] = false;
            return taken;
        }

        long now = _now();
        bool repeat = _down[vk] && now - _lastDown[vk] < RepeatWindowMs;
        if (!repeat)
            _taken[vk] = false; // a press after a missed release starts again
        _down[vk] = true;
        _lastDown[vk] = now;
        if (Trace && !repeat && !Shortcut.IsModifierKey(vk) && Modifiers() is var held
            && (held & (ShortcutModifiers.Ctrl | ShortcutModifiers.Alt | ShortcutModifiers.Win)) != 0)
            Diagnostic?.Invoke($"trace: {new Shortcut(held, vk)} (suspended: {_suspended}, paused: {_config.Paused}, popup: {_popupOpen}, "
                + $"altgr: {_down[VkRAlt] && _down[VkLCtrl] && !_down[VkLAlt]}, popup shortcut: {_config.Popup}, double Ctrl+C: {_config.DoubleCopy})");
        if (_suspended)
            return false;
        if (_popupOpen)
            return PopupKeyDown(vk, repeat);
        if (repeat)
            return _taken[vk];
        if (Shortcut.IsModifierKey(vk))
            return false;

        var config = _config;
        if (config.Paused)
            return false;
        var modifiers = Modifiers();
        // AltGr is Ctrl + right Alt: on AZERTY and many other keyboards it types characters, never a shortcut.
        bool altGr = _down[VkRAlt] && _down[VkLCtrl] && !_down[VkLAlt];
        if (!altGr)
        {
            var pressed = new Shortcut(modifiers, vk);
            if (config.Popup.IsValid && pressed == config.Popup)
                return Take(vk, modifiers, new Trigger(TriggerKind.Popup));
            if (config.Direct.TryGetValue(pressed, out var mode))
                return Take(vk, modifiers, new Trigger(TriggerKind.Direct, mode));
        }

        if (config.DoubleCopy && vk == VkC && modifiers == ShortcutModifiers.Ctrl)
        {
            _doubleCopy.DelayMs = config.DoubleCopyDelay;
            if (_doubleCopy.Press(now))
                Triggered?.Invoke(new Trigger(TriggerKind.DoubleCopy));
            else if (_doubleCopy.LastGapMs is long gap && gap < 1500)
                Diagnostic?.Invoke($"Ctrl+C twice: {gap} ms apart, more than the {config.DoubleCopyDelay} ms set");
        }
        else
            _doubleCopy.Reset(); // another key between the two Ctrl+C
        return false;
    }

    private bool Take(int vk, ShortcutModifiers modifiers, Trigger trigger)
    {
        _taken[vk] = true;
        _doubleCopy.Reset();
        if ((modifiers & (ShortcutModifiers.Alt | ShortcutModifiers.Win)) != 0)
            MaskNeeded?.Invoke();
        Triggered?.Invoke(trigger);
        return true;
    }

    // The popup reads its keys without the focus: they are taken from the app in front, which keeps its selection.
    private bool PopupKeyDown(int vk, bool repeat)
    {
        if (Shortcut.IsModifierKey(vk))
            return false;
        var modifiers = Modifiers();
        bool shiftOnly = modifiers is ShortcutModifiers.None or ShortcutModifiers.Shift;
        PopupKey? key = !shiftOnly ? null : vk switch
        {
            0x26 or 0x21 => PopupKey.Up,              // Up, Page Up
            0x28 or 0x22 => PopupKey.Down,            // Down, Page Down
            0x09 => modifiers == ShortcutModifiers.Shift ? PopupKey.Up : PopupKey.Down, // Tab
            0x24 => PopupKey.Home,
            0x23 => PopupKey.End,
            0x0D or 0x20 => PopupKey.Choose,          // Enter, Space
            0x1B => PopupKey.Cancel,                  // Escape
            0x30 or 0x60 => PopupKey.Smart,           // 0, numpad 0
            >= 0x31 and <= 0x39 or >= 0x41 and <= 0x5A or >= 0x61 and <= 0x69 => PopupKey.Row,
            _ => null,
        };
        if (key is null)
        {
            // Any other key (Alt+Tab, a letter with Ctrl, Delete…): the popup closes and the key goes to the app.
            PopupKeyPressed?.Invoke(PopupKey.Cancel, '\0');
            return false;
        }
        _taken[vk] = true;
        // Held down, the arrows go on moving; a row key or Enter counts once.
        if (repeat && key is not (PopupKey.Up or PopupKey.Down))
            return true;
        char row = vk is >= 0x61 and <= 0x69 ? (char)('1' + vk - 0x61) : (char)vk;
        PopupKeyPressed?.Invoke(key.Value, key == PopupKey.Row ? row : '\0');
        return true;
    }

    private ShortcutModifiers Modifiers()
    {
        long now = _now();
        var modifiers = ShortcutModifiers.None;
        foreach (int vk in ModifierKeys)
        {
            if (!_down[vk])
                continue;
            // Its release was missed: held for Windows too, or pressed a moment ago, it is still down.
            if (now - _lastDown[vk] > StaleModifierMs && _isKeyDown is not null && !_isKeyDown(vk))
            {
                _down[vk] = false;
                Diagnostic?.Invoke($"key 0x{vk:X2} seen down {(now - _lastDown[vk]) / 1000} s ago, up for Windows: forgotten");
                continue;
            }
            modifiers |= vk switch
            {
                VkLCtrl or VkRCtrl => ShortcutModifiers.Ctrl,
                VkLAlt or VkRAlt => ShortcutModifiers.Alt,
                VkLShift or VkRShift => ShortcutModifiers.Shift,
                _ => ShortcutModifiers.Win,
            };
        }
        return modifiers;
    }
}
