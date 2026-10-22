using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TextCase.Core;
using TextCase.Services;

namespace TextCase.Controls;

/// <summary>
/// A shortcut of the settings: shows it ("Ctrl + Alt + C"); a click records a new one from the next keys pressed (Esc
/// cancels, Backspace removes it). While it records, the shortcuts of TextCase are off, so that the keys reach it.
/// </summary>
internal sealed class ShortcutBox : StackPanel
{
    private readonly Button _button;
    private readonly Button _clear;
    private readonly Func<Shortcut, string?> _conflict;
    private readonly Action<Shortcut> _changed;
    private readonly DispatcherTimer _messageTimer = new() { Interval = TimeSpan.FromMilliseconds(1600) };
    private Shortcut _value;
    private bool _recording;

    /// <param name="conflict">The name of what already uses a shortcut, or null when it is free.</param>
    public ShortcutBox(Shortcut value, Func<Shortcut, string?> conflict, Action<Shortcut> changed, string automationName)
    {
        _value = value;
        _conflict = conflict;
        _changed = changed;
        Orientation = Orientation.Horizontal;

        _button = new Button { MinWidth = 180, HorizontalContentAlignment = HorizontalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(_button, automationName);
        _button.Click += (_, _) => StartRecording();
        _button.PreviewKeyDown += OnKeyDown;
        _button.LostKeyboardFocus += (_, _) => StopRecording();

        _clear = new Button
        {
            Content = new TextBlock { Text = "", FontSize = 12, Style = (Style)Application.Current.FindResource("IconText") },
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
            Margin = new Thickness(4, 0, 0, 0),
            ToolTip = Strings.Get("ShortcutClear"),
        };
        System.Windows.Automation.AutomationProperties.SetName(_clear, Strings.Get("ShortcutClear"));
        _clear.Click += (_, _) => Set(Shortcut.None);

        Children.Add(_button);
        Children.Add(_clear);
        _messageTimer.Tick += (_, _) =>
        {
            _messageTimer.Stop();
            Refresh();
        };
        Refresh();
    }

    /// <summary>The shortcut shown changed elsewhere (another row took it).</summary>
    public void Show(Shortcut value)
    {
        _value = value;
        Refresh();
    }

    private void StartRecording()
    {
        _recording = true;
        App.Matcher.Suspended = true;
        _messageTimer.Stop();
        _button.Content = Strings.Get("ShortcutPress");
        _button.Focus();
    }

    private void StopRecording()
    {
        if (!_recording)
            return;
        _recording = false;
        App.Matcher.Suspended = false;
        if (!_messageTimer.IsEnabled)
            Refresh();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!_recording)
            return;
        e.Handled = true;
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        var modifiers = Modifiers();
        if (modifiers == ShortcutModifiers.None && key == Key.Escape)
        {
            StopRecording();
            return;
        }
        if (modifiers == ShortcutModifiers.None && key is Key.Back or Key.Delete)
        {
            StopRecording();
            Set(Shortcut.None);
            return;
        }
        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (Shortcut.IsModifierKey(vk))
        {
            // Modifiers alone so far: show them as they are pressed.
            _button.Content = string.Join(" + ", ModifierNames(modifiers).Append("…"));
            return;
        }
        var shortcut = new Shortcut(modifiers, vk);
        if (!shortcut.IsValid)
        {
            _button.Content = Strings.Get("ShortcutNeedsModifier");
            return;
        }
        StopRecording();
        if (shortcut == _value)
            return;
        if (_conflict(shortcut) is { } usedBy)
        {
            Message(Strings.Format("ShortcutTaken", usedBy));
            return;
        }
        Set(shortcut);
    }

    private void Set(Shortcut shortcut)
    {
        _value = shortcut;
        Refresh();
        _changed(shortcut);
    }

    private void Message(string text)
    {
        _button.Content = text;
        _messageTimer.Stop();
        _messageTimer.Start();
    }

    private void Refresh()
    {
        _button.Content = _value.IsEmpty ? Strings.Get("ShortcutNone") : Display(_value);
        _clear.Visibility = _value.IsEmpty ? Visibility.Hidden : Visibility.Visible;
    }

    private static ShortcutModifiers Modifiers()
    {
        var modifiers = ShortcutModifiers.None;
        var held = Keyboard.Modifiers;
        if (held.HasFlag(ModifierKeys.Control))
            modifiers |= ShortcutModifiers.Ctrl;
        if (held.HasFlag(ModifierKeys.Alt))
            modifiers |= ShortcutModifiers.Alt;
        if (held.HasFlag(ModifierKeys.Shift))
            modifiers |= ShortcutModifiers.Shift;
        if (held.HasFlag(ModifierKeys.Windows) || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            modifiers |= ShortcutModifiers.Win;
        return modifiers;
    }

    /// <summary>A shortcut as the keyboard writes it, in the UI language: "Ctrl + Maj + U", "Ctrl + Alt + $".</summary>
    public static string Display(Shortcut shortcut) =>
        shortcut.IsEmpty ? "" : string.Join(" + ", ModifierNames(shortcut.Modifiers).Append(KeyDisplayName(shortcut.Key)));

    private static IEnumerable<string> ModifierNames(ShortcutModifiers modifiers) =>
        Shortcut.ModifierNames(modifiers).Select(name => name == "Shift" ? Strings.Get("KeyShift") : name);

    // Letters, digits and F keys by their usual names; the other keys by the name Windows gives them on this keyboard.
    private static string KeyDisplayName(int vk)
    {
        string name = Shortcut.KeyName(vk);
        if (!name.StartsWith("VK_", StringComparison.Ordinal) && vk is not (0x20 or >= 0x21 and <= 0x2E))
            return name;
        int scan = NativeMethods.MapVirtualKey(vk, NativeMethods.MAPVK_VK_TO_VSC);
        // The arrows and the keys of the navigation block are "extended" keys: their names otherwise come from the numpad.
        bool extended = vk is >= 0x21 and <= 0x2E;
        var text = new StringBuilder(64);
        if (scan != 0 && NativeMethods.GetKeyNameText((scan << 16) | (extended ? 1 << 24 : 0), text, text.Capacity) > 0)
            return text.ToString();
        return name;
    }
}
