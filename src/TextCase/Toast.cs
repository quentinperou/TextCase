using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using static TextCase.Services.NativeMethods;

namespace TextCase;

/// <summary>
/// A short message where the popup would have opened ("No text selected"), gone after a moment. Like the popup, it
/// never takes the focus.
/// </summary>
internal sealed class Toast : Window
{
    private static Toast? s_open;

    private readonly POINT _anchor;

    public static void Show(string message, POINT anchor)
    {
        s_open?.Close();
        s_open = new Toast(message, anchor);
        s_open.Show();
    }

    private Toast(string message, POINT anchor)
    {
        _anchor = anchor;
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
        Left = -32000;
        Top = -32000;
        ThemeMode = ThemeMode.None;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        UseLayoutRounding = true;

        var colors = PopupColors.Current();
        var text = new TextBlock { Text = message, FontSize = 13, Foreground = colors.Text, Margin = new Thickness(14, 9, 14, 10) };
        var surface = new Border
        {
            Background = colors.Surface,
            BorderBrush = colors.Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = text,
        };
        var shadow = new Border
        {
            Background = colors.Surface,
            CornerRadius = new CornerRadius(8),
            Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Direction = 270, Opacity = 0.2, Color = Colors.Black },
        };
        var root = new Grid { Margin = new Thickness(12) };
        root.Children.Add(shadow);
        root.Children.Add(surface);
        Content = root;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            long style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW));
        };
        ContentRendered += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            GetWindowRect(hwnd, out var rect);
            int margin = (int)(12 * VisualTreeHelper.GetDpi(this).DpiScaleX);
            SetWindowPos(hwnd, HWND_TOPMOST, _anchor.X - margin, _anchor.Y - margin + 6, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
        };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Close();
        };
        timer.Start();
        Closed += (_, _) =>
        {
            timer.Stop();
            if (s_open == this)
                s_open = null;
        };
    }
}
