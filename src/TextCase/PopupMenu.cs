using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using TextCase.Services;

namespace TextCase;

/// <summary>
/// The menu of the notification icon, drawn as Windows' own (Menus.xaml) in Windows' mode. Built each time it opens, so
/// that it shows the current state and language, and nothing of it stays in memory afterwards.
/// </summary>
internal static class PopupMenu
{
    // A check mark in the icon column.
    private const string Check = "";

    private static ContextMenu? s_open;

    public static void ShowTray() => Show(() =>
    {
        var menu = new ContextMenu { MinWidth = 240 };
        menu.Items.Add(Item(Strings.Get("MenuOpen"), "", () => App.OpenMainWindow()));
        var pause = Item(Strings.Get("MenuPause"), App.Settings.Paused ? Check : "", () => App.SetPaused(!App.Settings.Paused));
        menu.Items.Add(pause);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Strings.Get("MenuQuit"), "", App.Quit));
        return menu;
    });

    private static void Show(Func<ContextMenu> build)
    {
        if (s_open is { IsOpen: true })
        {
            s_open.IsOpen = false;
            return;
        }

        // A WPF menu of an app with no window in front loses the mouse capture as it opens, and closes at once. So the
        // menu belongs to a tiny invisible window under the pointer, activated first (a click on the icon gives the
        // app the right to come to the front), and closed with the menu.
        var host = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Width = 1,
            Height = 1,
            Title = "TextCase Menu",
            ThemeMode = ThemeMode.None,
        };
        host.SourceInitialized += (_, _) =>
        {
            NativeMethods.GetCursorPos(out var pointer);
            NativeMethods.SetWindowPos(new WindowInteropHelper(host).Handle, NativeMethods.HWND_TOPMOST,
                pointer.X, pointer.Y, 1, 1, NativeMethods.SWP_NOACTIVATE);
        };
        host.Show();
        NativeMethods.SetForegroundWindow(new WindowInteropHelper(host).Handle);
        host.Activate();

        var menu = build();
        Dress(menu);
        menu.PlacementTarget = host;
        menu.Placement = PlacementMode.MousePoint;
        menu.HorizontalOffset = -ShadowRoom;
        menu.VerticalOffset = -ShadowRoom;
        // Not the popup itself: activating it would deactivate the host, which closes the menu.
        menu.Closed += (_, _) =>
        {
            s_open = null;
            host.Close();
            MemoryTrim.Soon();
        };
        s_open = menu;
        menu.IsOpen = true;
    }

    // The margin of the menu around its border, for its shadow (Menus.xaml).
    private const int ShadowRoom = 12;

    // The look of Windows 11's menus, in Windows' mode (that of the taskbar), whatever the theme of the app window.
    private static void Dress(ContextMenu menu)
    {
        menu.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/TextCase;component/Menus.xaml", UriKind.Relative)));
        menu.Style = (Style)menu.Resources["WindowsMenu"];
        bool dark = WindowsSettings.TaskbarIsDark;
        (string Key, string Light, string Dark)[] colors =
        [
            ("MenuSurface", "#F9F9F9", "#2C2C2C"),
            ("MenuStroke", "#E3E3E3", "#424242"),
            ("MenuText", "#1B1B1B", "#FFFFFF"),
            ("MenuTextSecondary", "#5F5F5F", "#C5C5C5"),
            ("MenuTextDisabled", "#A0A0A0", "#7A7A7A"),
            ("MenuHover", "#EDEDED", "#383838"),
            ("MenuDivider", "#E5E5E5", "#404040"),
        ];
        foreach (var (key, light, darkColor) in colors)
            menu.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? darkColor : light));
    }

    private static MenuItem Item(string header, string glyph, Action action)
    {
        var item = new MenuItem { Header = header };
        if (glyph.Length > 0)
            item.Icon = new TextBlock { Text = glyph, FontSize = 16, Style = (Style)Application.Current.FindResource("IconText") };
        item.Click += (_, _) => action();
        return item;
    }
}
