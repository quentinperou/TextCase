using TextCase.Core;

namespace TextCase.Tests;

/// <summary>The keys as the hook sees them, with a clock of the test: what is taken, what is triggered.</summary>
public class ShortcutMatcherTests
{
    private const int LCtrl = 0xA2, RAlt = 0xA5, LAlt = 0xA4, LShift = 0xA0, LWin = 0x5B, C = 0x43, U = 0x55, Two = 0x32, Zero = 0x30, Delete = 0x2E;

    private long _now = 10_000;
    private readonly HashSet<int> _windowsDown = [];
    private readonly List<Trigger> _triggers = [];
    private readonly List<(PopupKey Key, char Row)> _popupKeys = [];
    private readonly ShortcutMatcher _matcher;

    public ShortcutMatcherTests()
    {
        _matcher = new ShortcutMatcher(() => _now, vk => _windowsDown.Contains(vk))
        {
            Current = new ShortcutMatcher.Config(Shortcut.Parse("Ctrl+Alt+C"),
                new Dictionary<Shortcut, CaseMode> { [Shortcut.Parse("Ctrl+Alt+U")] = CaseMode.Upper }, DoubleCopy: true, DoubleCopyDelay: 500, Paused: false),
        };
        _matcher.Triggered += _triggers.Add;
        _matcher.PopupKeyPressed += (key, row) => _popupKeys.Add((key, row));
    }

    private bool Down(int vk, int afterMs = 50)
    {
        _now += afterMs;
        _windowsDown.Add(vk);
        return _matcher.OnKey(new KeyEvent(vk, Down: true));
    }

    private bool Up(int vk, int afterMs = 50)
    {
        _now += afterMs;
        _windowsDown.Remove(vk);
        return _matcher.OnKey(new KeyEvent(vk, Down: false));
    }

    private void Tap(int vk, int afterMs = 50)
    {
        Down(vk, afterMs);
        Up(vk);
    }

    [Fact]
    public void PopupShortcut_IsTakenWithItsRelease()
    {
        Down(LCtrl);
        Down(LAlt);
        Assert.True(Down(C));
        Assert.True(Up(C));
        Assert.False(Up(LAlt));
        Assert.Equal([new Trigger(TriggerKind.Popup)], _triggers);
    }

    [Fact]
    public void DirectShortcut_GivesItsCase()
    {
        Down(LCtrl);
        Down(LAlt);
        Assert.True(Down(U));
        Assert.Equal([new Trigger(TriggerKind.Direct, CaseMode.Upper)], _triggers);
    }

    [Fact]
    public void AltGr_IsNeverAShortcut()
    {
        // AltGr: left Ctrl + right Alt, as Windows sends it.
        Down(LCtrl);
        Down(RAlt, 0);
        Assert.False(Down(C));
        Assert.Empty(_triggers);
    }

    [Fact]
    public void CtrlCTwice_CtrlHeld()
    {
        Down(LCtrl);
        Tap(C);
        Assert.False(Down(C, 200)); // never taken: the app copies too
        Assert.Equal([new Trigger(TriggerKind.DoubleCopy)], _triggers);
    }

    [Fact]
    public void CtrlCTwice_CtrlReleasedBetween()
    {
        Down(LCtrl);
        Tap(C);
        Up(LCtrl);
        Down(LCtrl, 100);
        Down(C, 100);
        Assert.Equal([new Trigger(TriggerKind.DoubleCopy)], _triggers);
    }

    [Fact]
    public void CtrlCTwice_TooSlow_IsLogged()
    {
        var lines = new List<string>();
        _matcher.Diagnostic += lines.Add;
        Down(LCtrl);
        Tap(C);
        Down(C, 700);
        Assert.Empty(_triggers);
        Assert.Contains(lines, l => l.Contains("ms apart"));
    }

    [Fact]
    public void CtrlCTwice_AnotherKeyBetween_DoesNotCount()
    {
        Down(LCtrl);
        Tap(C);
        Tap(0x56); // Ctrl+V
        Down(C, 50);
        Assert.Empty(_triggers);
    }

    [Fact]
    public void HeldC_AutoRepeat_IsNotASecondPress()
    {
        Down(LCtrl);
        Down(C);
        Down(C, 500); // the auto-repeat of the held key
        Down(C, 33);
        Assert.Empty(_triggers);
    }

    [Fact]
    public void MissedRelease_OfC_DoesNotBlockTheNextPresses()
    {
        Down(LCtrl);
        Down(C); // its release never comes (the remote desktop client had the keys)
        Up(LCtrl);
        Down(LCtrl, 2000);
        Down(C, 50);
        Up(C);
        Down(C, 150);
        Assert.Equal([new Trigger(TriggerKind.DoubleCopy)], _triggers);
    }

    [Fact]
    public void MissedRelease_OfAModifier_IsForgottenWhenWindowsSaysItIsUp()
    {
        Down(LShift);
        _windowsDown.Remove(LShift); // released, but the hook never saw it (Win + L, the remote desktop)
        _now += 5000;
        Down(LCtrl);
        Down(LAlt);
        Assert.True(Down(C));
        Assert.Equal([new Trigger(TriggerKind.Popup)], _triggers);
    }

    [Fact]
    public void HeldModifier_IsKeptWhileWindowsSaysItIsDown()
    {
        Down(LShift);
        _now += 5000; // held for a while, no repeat (another key was pressed after it)
        Down(LCtrl);
        Down(LAlt);
        Assert.False(Down(C)); // Ctrl+Alt+Shift+C is not the popup shortcut
        Assert.Empty(_triggers);
    }

    [Fact]
    public void OwnKeys_AreNeverShortcuts()
    {
        _matcher.OnKey(new KeyEvent(LCtrl, true, Own: true));
        _matcher.OnKey(new KeyEvent(C, true, Own: true));
        _matcher.OnKey(new KeyEvent(C, false, Own: true));
        _matcher.OnKey(new KeyEvent(C, true, Own: true));
        Assert.Empty(_triggers);
    }

    [Fact]
    public void Paused_NothingIsTaken()
    {
        _matcher.Current = _matcher.Current with { Paused = true };
        Down(LCtrl);
        Down(LAlt);
        Assert.False(Down(C));
        Assert.Empty(_triggers);
    }

    [Fact]
    public void Popup_TakesItsKeys_AndLetsTheOthersGo()
    {
        _matcher.PopupOpen = true;
        Assert.True(Down(Two));
        Assert.True(Up(Two));
        Assert.True(Down(Zero));
        Assert.False(Down(Delete));
        Assert.Equal([(PopupKey.Row, '2'), (PopupKey.Smart, '\0'), (PopupKey.Cancel, '\0')], _popupKeys);
    }

    [Fact]
    public void Popup_InDeveloperMode_OtherKeysLeaveItOpen()
    {
        _matcher.PopupOpen = true;
        _matcher.KeepPopupOpen = true;
        Down(LWin);
        Down(LShift);
        Assert.False(Down(0x53)); // Win+Shift+S, the screenshot of Windows: to Windows, the popup stays
        Up(0x53);
        Up(LShift);
        Up(LWin);
        Assert.True(Down(0x1B)); // Esc still closes it
        Assert.Equal([(PopupKey.Cancel, '\0')], _popupKeys);
    }

    [Fact]
    public void Popup_AShortcutWithCtrl_ClosesIt()
    {
        _matcher.PopupOpen = true;
        Down(LCtrl);
        Assert.False(Down(Two));
        Assert.Equal([(PopupKey.Cancel, '\0')], _popupKeys);
    }

    [Fact]
    public void AnyModifierDown_FollowsTheReleases()
    {
        Down(LWin);
        Assert.True(_matcher.AnyModifierDown);
        Up(LWin);
        Assert.False(_matcher.AnyModifierDown);
    }
}
