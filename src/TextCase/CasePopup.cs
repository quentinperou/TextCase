using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using TextCase.Core;
using TextCase.Services;
using static TextCase.Services.NativeMethods;

namespace TextCase;

/// <summary>
/// The popup, as chosen by the author (proposal B): the selected text, then each case with its key and its result;
/// the last case used is highlighted. It never takes the focus: the app keeps its selection (and the remote desktop
/// window its keyboard), the keys come from the keyboard hook (<see cref="OnKey"/>), the mouse works as usual. Built
/// each time it opens, closed after the choice.
/// </summary>
internal sealed class CasePopup : Window
{
    private const double ShadowRoom = 12;
    private const int PreviewLength = 2000;

    private readonly List<Row> _rows = [];
    private readonly TaskCompletionSource<CaseMode?> _result = new();
    private readonly POINT _anchor;
    private readonly PopupColors _colors = PopupColors.Current();
    private int _selected;
    private bool _finished;
    private string _sample = "";
    private Border? _smart;
    private TextBlock? _smartCheck;
    private TextBlock? _smartText;

    private sealed record Row(CaseMode Mode, Border Back, Border Pill, Border Badge, TextBlock Key, TextBlock Value);

    /// <summary>The popup open now, if any.</summary>
    public static CasePopup? Current { get; private set; }

    /// <summary>Shows the popup for <paramref name="text"/>; the case chosen, or null when it was closed.</summary>
    /// <param name="beside">
    /// The rectangle of Start or its search box, in screen pixels: Windows draws them in a layer above every window of
    /// an app (even topmost, even owned by the taskbar: tried in 0.1.2), so the popup goes beside them, narrower if
    /// needed, never under them.
    /// </param>
    public static Task<CaseMode?> ChooseAsync(string text, POINT anchor, RECT? beside = null)
    {
        Current?.Finish(null, "another popup opens");
        var popup = new CasePopup(text, anchor, beside);
        Current = popup;
        App.Matcher.PopupOpen = true;
        App.Hook.WatchMouse(true);
        popup.Show();
        return popup._result.Task;
    }

    /// <summary>
    /// The popup on <paramref name="text"/>, drawn off screen into a PNG then closed: for the pictures of the README and
    /// the tests (TEXTCASE_PICTURE_POPUP). It reads no key.
    /// </summary>
    public static void Picture(string text, string path)
    {
        var popup = new CasePopup(text, default) { _picture = true };
        popup.ContentRendered += (_, _) =>
        {
            var content = (FrameworkElement)popup.Content;
            const double scale = 1.5;
            // The content is drawn at its margin (the room of the shadow): the picture takes it on both sides.
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)Math.Ceiling((content.ActualWidth + 2 * ShadowRoom) * scale), (int)Math.Ceiling((content.ActualHeight + 2 * ShadowRoom) * scale),
                96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var file = File.Create(path))
                encoder.Save(file);
            popup.Close();
        };
        popup.Show();
    }

    private bool _picture;
    private readonly RECT? _beside;
    private bool? _onRight;
    private double _panelWidth = 600;

    /// <summary>The narrowest the popup gets to fit beside Start.</summary>
    private const double MinPanelWidth = 360;

    private CasePopup(string text, POINT anchor, RECT? beside = null)
    {
        _anchor = anchor;
        _beside = beside;
        if (beside is { } avoid)
            FitBeside(avoid);
        Title = "TextCase";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        // Drawn off screen first, then moved where it belongs once its size is known (Place).
        Left = -32000;
        Top = -32000;
        // Its own colors: the Fluent theme of the settings window must not paint it.
        ThemeMode = ThemeMode.None;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 14;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Content = Build(text);

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            long style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW));
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        };
        ContentRendered += (_, _) =>
        {
            if (_picture)
                return;
            Place();
            KeepOnTop();
        };
        DpiChanged += (_, _) =>
        {
            if (!_picture)
                Dispatcher.BeginInvoke(Place);
        };
    }

    // ---------------------------------------------------------------- Content

    private FrameworkElement Build(string text)
    {
        var settings = App.Settings;
        var modes = CaseModes.All.Where(settings.PopupModes.Contains).ToList();
        if (modes.Count == 0)
            modes = AppSettings.DefaultPopupModes();
        string sample = text.Length > PreviewLength ? text[..PreviewLength] : text;
        _sample = sample;
        var rules = App.Rules;

        var panel = new StackPanel { Width = _panelWidth };
        int lines = text.Split('\n').Length - (text.EndsWith('\n') ? 1 : 0);
        string header = lines > 1 ? Strings.Format("PopupHeaderLines", lines) : Strings.Get("PopupHeader");
        var top = new DockPanel { Margin = new Thickness(10, 5, 2, 5) };
        var smart = BuildSmartSwitch();
        DockPanel.SetDock(smart, Dock.Right);
        top.Children.Add(smart);
        top.Children.Add(new TextBlock { Text = header, FontSize = 12, Foreground = _colors.Text3, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(top);
        panel.Children.Add(new Border
        {
            BorderBrush = _colors.Divider,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(6, 0, 6, 6),
            Child = new TextBlock { Text = Preview(sample), FontSize = 13, Foreground = _colors.Text2, TextTrimming = TextTrimming.CharacterEllipsis },
        });

        CaseGroup? group = null;
        foreach (var mode in modes)
        {
            if (group is not null && CaseModes.GroupOf(mode) != group)
                panel.Children.Add(new Border { Height = 1, Background = _colors.Divider, Margin = new Thickness(-4, 4, -4, 4) });
            group = CaseModes.GroupOf(mode);
            panel.Children.Add(BuildRow(mode, _rows.Count, CaseConverter.Convert(sample, mode, rules)));
        }

        string keys = _rows.Count <= 9 ? $"1–{_rows.Count}" : $"1–9, A–{CaseModes.KeyOf(_rows.Count - 1)}";
        panel.Children.Add(new Border
        {
            BorderBrush = _colors.Divider,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(-4, 4, -4, 0),
            Padding = new Thickness(14, 8, 14, 9),
            Child = new TextBlock { Text = Strings.Format("PopupHint", keys), FontSize = 11.5, Foreground = _colors.Text3 },
        });

        int last = settings.LastMode is { } lastMode ? _rows.FindIndex(r => r.Mode == lastMode) : -1;
        Select(Math.Max(0, last));

        var surface = new Border
        {
            Background = _colors.Surface,
            BorderBrush = _colors.Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4, 0, 4, 0),
            Child = panel,
        };
        // The shadow on a shape of its own: an effect on the content would blur its text.
        var shadow = new Border
        {
            Background = _colors.Surface,
            CornerRadius = new CornerRadius(8),
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Direction = 270, Opacity = 0.22, Color = Colors.Black },
        };
        var root = new Grid { Margin = new Thickness(ShadowRoom) };
        root.Children.Add(shadow);
        root.Children.Add(surface);
        return root;
    }

    private Border BuildRow(CaseMode mode, int index, string result)
    {
        var key = new TextBlock
        {
            Text = CaseModes.KeyOf(index)?.ToString() ?? "",
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var badge = new Border { MinWidth = 20, Height = 20, CornerRadius = new CornerRadius(4), Child = key, VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock
        {
            Text = Strings.Get("Mode" + mode),
            FontSize = 12.5,
            Foreground = _colors.Text2,
            Margin = new Thickness(12, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var value = new TextBlock
        {
            Text = Preview(result),
            Foreground = _colors.Text,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(144) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(name, 1);
        Grid.SetColumn(value, 2);
        grid.Children.Add(badge);
        grid.Children.Add(name);
        grid.Children.Add(value);

        var pill = new Border
        {
            Width = 3,
            Height = 15,
            CornerRadius = new CornerRadius(1.5),
            Background = _colors.Accent,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(-10, 0, 0, 0),
            Visibility = Visibility.Hidden,
        };
        var content = new Grid();
        content.Children.Add(grid);
        content.Children.Add(pill);
        var back = new Border
        {
            Height = 31,
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 1, 0, 1),
            Padding = new Thickness(10, 0, 10, 0),
            Background = Brushes.Transparent,
            Child = content,
        };
        System.Windows.Automation.AutomationProperties.SetName(back, $"{Strings.Get("Mode" + mode)}: {result}");
        back.MouseEnter += (_, _) => Select(index);
        back.MouseLeftButtonUp += (_, _) => Choose(index);
        _rows.Add(new Row(mode, back, pill, badge, key, value));
        return back;
    }

    // "Smart" on or off, at the top right (key 0): Title Case and Sentence case with or without their rules.
    private Border BuildSmartSwitch()
    {
        var key = new Border
        {
            MinWidth = 18,
            Height = 18,
            CornerRadius = new CornerRadius(4),
            Background = _colors.Badge,
            Margin = new Thickness(0, 0, 7, 0),
            Child = new TextBlock
            {
                Text = "0",
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _colors.Text2,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        _smartCheck = new TextBlock
        {
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 12,
            Margin = new Thickness(0, 1, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _smartText = new TextBlock { Text = Strings.Get("PopupSmart"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(key);
        row.Children.Add(_smartCheck);
        row.Children.Add(_smartText);
        _smart = new Border
        {
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 2, 9, 2),
            Background = Brushes.Transparent,
            Child = row,
            ToolTip = Strings.Get("PopupSmartHint"),
        };
        System.Windows.Automation.AutomationProperties.SetName(_smart, Strings.Get("PopupSmart"));
        _smart.MouseLeftButtonUp += (_, _) => ToggleSmart();
        ShowSmart();
        return _smart;
    }

    private void ShowSmart()
    {
        if (_smart is null || _smartCheck is null || _smartText is null)
            return;
        bool on = App.Settings.SmartCases;
        _smart.BorderBrush = on ? _colors.Accent : _colors.Divider;
        _smartCheck.Text = on ? "" : "";
        _smartCheck.Foreground = on ? _colors.Accent : _colors.Text3;
        _smartText.Foreground = on ? _colors.Text : _colors.Text3;
    }

    private void ToggleSmart()
    {
        App.SetSmart(!App.Settings.SmartCases);
        ShowSmart();
        var rules = App.Rules;
        foreach (var row in _rows)
            row.Value.Text = Preview(CaseConverter.Convert(_sample, row.Mode, rules));
    }

    /// <summary>One line of a text: its line breaks shown as ↵, at most a line's worth of it.</summary>
    private static string Preview(string text)
    {
        string flat = text.Replace("\r\n", "\n").TrimEnd('\n').Replace("\n", " ↵ ").Replace('\r', ' ').Replace('\t', ' ');
        return flat.Length > 200 ? flat[..200] + "…" : flat;
    }

    private void Select(int index)
    {
        if (_rows.Count == 0)
            return;
        _selected = (index % _rows.Count + _rows.Count) % _rows.Count;
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            bool on = i == _selected;
            row.Back.Background = on ? _colors.Selection : Brushes.Transparent;
            row.Pill.Visibility = on ? Visibility.Visible : Visibility.Hidden;
            row.Badge.Background = on ? _colors.Accent : _colors.Badge;
            row.Key.Foreground = on ? _colors.OnAccent : _colors.Text2;
        }
    }

    private void Choose(int index)
    {
        if (index >= 0 && index < _rows.Count)
            Finish(_rows[index].Mode);
    }

    // ---------------------------------------------------------------- Keys and mouse (from the hooks)

    /// <summary>A key of the popup, read by the keyboard hook (the popup has no focus).</summary>
    public void OnKey(PopupKey key, char row)
    {
        switch (key)
        {
            case PopupKey.Up: Select(_selected - 1); break;
            case PopupKey.Down: Select(_selected + 1); break;
            case PopupKey.Home: Select(0); break;
            case PopupKey.End: Select(_rows.Count - 1); break;
            case PopupKey.Choose: Choose(_selected); break;
            case PopupKey.Row: Choose(CaseModes.IndexOfKey(row)); break;
            case PopupKey.Smart: ToggleSmart(); break;
            default: Finish(null, "Esc, or a key that is not the popup's"); break;
        }
    }

    /// <summary>A mouse button pressed anywhere: outside the popup, it closes.</summary>
    public void OnMouseDown(POINT point)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
            return;
        int margin = (int)(ShadowRoom * VisualTreeHelper.GetDpi(this).DpiScaleX);
        bool inside = point.X >= rect.Left + margin && point.X < rect.Right - margin
            && point.Y >= rect.Top + margin && point.Y < rect.Bottom - margin;
        if (inside)
            return;
        // In developer mode it stays open, for a screenshot: Esc or a choice closes it.
        string where = $"a click outside it, at {point.X},{point.Y} (popup {rect.Left + margin},{rect.Top + margin} → {rect.Right - margin},{rect.Bottom - margin})";
        if (App.Settings.DeveloperMode)
            SwapLog.Write($"  {where}: kept open (developer mode)");
        else
            Finish(null, where);
    }

    // Some windows put themselves above the others again (the remote desktop client in full screen): while the popup is
    // open, it goes back to the top of the topmost windows a few times a second.
    private System.Windows.Threading.DispatcherTimer? _keepOnTop;

    private void KeepOnTop()
    {
        _keepOnTop = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _keepOnTop.Tick += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        };
        _keepOnTop.Start();
    }

    /// <param name="reason">Why it closes without a choice, for the log.</param>
    public void Finish(CaseMode? mode, string? reason = null)
    {
        if (_finished)
            return;
        _finished = true;
        if (reason is not null)
            SwapLog.Write($"  popup closed: {reason}");
        if (Current == this)
            Current = null;
        _keepOnTop?.Stop();
        App.Matcher.PopupOpen = false;
        App.Hook.WatchMouse(false);
        Close();
        _result.TrySetResult(mode);
    }

    // ---------------------------------------------------------------- Place

    // Under the anchor (the text cursor or the pointer), its visible border at the anchor's left; above it when there
    // is no room below; always on the screen of the anchor.
    private void Place()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
            return;
        var monitor = MonitorFromPoint(_anchor, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info))
            return;
        var work = info.rcWork;
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int margin = (int)(ShadowRoom * scale);
        int x = _anchor.X - margin;
        int y = _anchor.Y - margin + 6;
        if (_beside is { } avoid && _onRight is { } onRight)
        {
            // Beside Start, its top on Start's top.
            int gap = (int)(8 * scale);
            x = onRight ? avoid.Right + gap - margin : avoid.Left - gap - rect.Width + margin;
            y = avoid.Top - margin;
        }
        else if (y + rect.Height - margin > work.Bottom)
            y = _anchor.Y - rect.Height + margin - 30;
        x = Math.Clamp(x, work.Left - margin, Math.Max(work.Left - margin, work.Right - rect.Width + margin));
        y = Math.Clamp(y, work.Top - margin, Math.Max(work.Top - margin, work.Bottom - rect.Height + margin));
        SetWindowPos(hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
        SwapLog.Write($"  popup at {x + margin},{y + margin} ({rect.Width - 2 * margin}×{rect.Height - 2 * margin})"
            + (_beside is { } b ? $", beside Start {b.Left},{b.Top} → {b.Right},{b.Bottom}: {(_onRight is null ? "no room" : _onRight.Value ? "right" : "left")}" : ""));
    }

    /// <summary>
    /// Room beside Start (<paramref name="avoid"/>) on its screen: the wider side, the popup narrower if needed (down to
    /// <see cref="MinPanelWidth"/>). No room: it opens as usual, under Start.
    /// </summary>
    private void FitBeside(RECT avoid)
    {
        var center = new POINT { X = (avoid.Left + avoid.Right) / 2, Y = (avoid.Top + avoid.Bottom) / 2 };
        var monitor = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info))
            return;
        double scale = GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 ? dpi / 96.0 : 1;
        int right = info.rcWork.Right - avoid.Right;
        int left = avoid.Left - info.rcWork.Left;
        // The panel inside the window: the shadow room, the border and padding, a gap from Start.
        double width = Math.Max(right, left) / scale - 2 * ShadowRoom - 10 - 8;
        if (width < MinPanelWidth)
            return;
        _panelWidth = Math.Min(600, width);
        _onRight = right >= left;
    }

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(MA_NOACTIVATE);
        }
        return IntPtr.Zero;
    }
}

/// <summary>
/// The colors of the popup and the toast, light or dark like the app (its theme, or Windows' apps mode), with the
/// emerald of the icon as accent.
/// </summary>
internal sealed record PopupColors(Brush Surface, Brush Stroke, Brush Text, Brush Text2, Brush Text3, Brush Divider,
    Brush Selection, Brush Accent, Brush OnAccent, Brush Badge)
{
    public static PopupColors Current() => App.IsDark ? Dark : Light;

    private static readonly PopupColors Light = new(B("#F9F9F9"), B("#E3E3E3"), B("#1B1B1B"), B("#616161"), B("#8A8A8A"),
        B("#E5E5E5"), B("#E1F3EA"), B("#0E9F6E"), B("#FFFFFF"), B("#EEEEEE"));

    private static readonly PopupColors Dark = new(B("#2C2C2C"), B("#444444"), B("#FFFFFF"), B("#C5C5C5"), B("#9A9A9A"),
        B("#404040"), B("#1F3A2F"), B("#3DD68C"), B("#0B2018"), B("#3A3A3A"));

    private static SolidColorBrush B(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
