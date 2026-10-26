using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using TextCase.Core;
using TextCase.Services;
using static TextCase.Services.NativeMethods;

namespace TextCase;

public partial class App : Application
{
    /// <summary>Started with Windows (Run key): nothing to open, the shortcuts just work.</summary>
    public const string BackgroundArgument = "--background";

    // Windows display language, kept to come back to it when the user picks "Windows language".
    private static readonly CultureInfo SystemUICulture = CultureInfo.CurrentUICulture;

    private static bool s_themeApplied;
    private static DispatcherTimer? s_saveTimer;
    private static IntPtr s_foregroundHook;
    // Kept in a field: Windows calls it for as long as the hook is installed.
    private static WinEventProc? s_foregroundChanged;
    private SingleInstance? _instance;

    /// <summary>The settings, loaded once at startup; the window changes and saves this instance.</summary>
    public static AppSettings Settings { get; private set; } = new();

    /// <summary>The rules of Title Case and Sentence case, from the settings.</summary>
    public static CaseRules Rules { get; private set; } = CaseRules.Default;

    internal static TrayHost Tray { get; private set; } = null!;

    internal static KeyboardHook Hook { get; private set; } = null!;

    internal static ShortcutMatcher Matcher { get; } = new(isKeyDown: vk => (GetAsyncKeyState(vk) & 0x8000) != 0);

    private static readonly TextSwap Swap = new();

    /// <summary>The shortcuts changed (paused from the menu of the notification icon): the window follows.</summary>
    public static event EventHandler? ShortcutsChanged;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // From the menu of File Explorer: one item renamed, then quit (the running TextCase is not involved, so that
        // many items selected at once are all renamed).
        if (e.Args is [ExplorerMenu.Argument, ..])
        {
            Settings = AppSettings.Load();
            ApplyLanguage(Settings.Language);
            ExplorerMenu.Rename(e.Args);
            Shutdown();
            return;
        }

        Settings = AppSettings.Load();

        // UI language: the one chosen in the settings, else the Windows one (French, otherwise English).
        // TEXTCASE_LANG=en|fr forces one, handy to test both.
        if (Environment.GetEnvironmentVariable("TEXTCASE_LANG") is { Length: > 0 } forced)
            ApplyLanguage(forced);
        else
            ApplyLanguage(Settings.Language);

        // Before the single instance: the version from GitHub may be the one running.
        if (OfferClassicRemoval())
        {
            Shutdown();
            return;
        }

        _instance = SingleInstance.TryStart(e.Args, _ => Dispatcher.BeginInvoke(() => OpenMainWindow()));
        if (_instance is null)
        {
            Shutdown(); // already running: it has been told to open its window
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "TextCase", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        Tray = new TrayHost();
        Tray.IconClicked += (_, _) => OpenMainWindow();
        Tray.MenuRequested += (_, _) => PopupMenu.ShowTray();

        // The hook thread only reports; the work is done here, on the UI thread.
        Matcher.Triggered += trigger => Dispatcher.BeginInvoke(() =>
        {
            if (Matcher.Trace)
                SwapLog.Write($"trace: {trigger.Kind} raised");
            _ = Swap.RunAsync(trigger);
        });
        ApplyDeveloperMode();
        Matcher.PopupKeyPressed += (key, row) => Dispatcher.BeginInvoke(() => CasePopup.Current?.OnKey(key, row));
        Matcher.MaskNeeded += () => Hook.MaskModifiers();
        Matcher.Diagnostic += line => Dispatcher.BeginInvoke(() => SwapLog.Write(line));
        Hook = new KeyboardHook(Matcher.OnKey, point => Dispatcher.BeginInvoke(() => CasePopup.Current?.OnMouseDown(point)));
        // TEXTCASE_NO_HOOK=1: the shortcuts shown but never seen (pictures of the README taken while the author types).
        Matcher.Suspended = Environment.GetEnvironmentVariable("TEXTCASE_NO_HOOK") == "1";
        ApplyRules();
        ApplyShortcuts();
        WatchForeground();
        RefreshExplorerMenu();

        // Start with Windows: on by default for the installed app, once; the user can turn it off in Settings.
        if (!Settings.StartupConfigured && WindowsSettings.IsInstalled)
        {
            WindowsSettings.StartWithWindows = true;
            Settings.StartupConfigured = true;
            Settings.Save();
        }

        // TEXTCASE_PICTURE_POPUP=<file.png>: the popup on a sample text, pictured off screen (README, tests).
        if (Environment.GetEnvironmentVariable("TEXTCASE_PICTURE_POPUP") is { Length: > 0 } picture)
            CasePopup.Picture(Environment.GetEnvironmentVariable("TEXTCASE_PICTURE_TEXT") ?? "CANON EOS R6 MARK II BOÎTIER NU", picture);
        else if (!e.Args.Contains(BackgroundArgument) && !AppPackage.LaunchedAtStartup())
            OpenMainWindow();

        _ = Updater.CheckAtStartupAsync();
        MemoryTrim.Soon();
    }

    /// <summary>
    /// The Store version, the first time it finds the version installed from GitHub on the PC: two TextCase at once
    /// would both answer each shortcut, so it offers to uninstall that one (asked once). True when the uninstaller has
    /// been started: it closes every TextCase, so this one quits.
    /// </summary>
    private static bool OfferClassicRemoval()
    {
        if (!AppPackage.IsPackaged || Settings.ClassicRemovalOffered || AppPackage.ClassicUninstaller() is not { } uninstaller)
            return false;
        Settings.ClassicRemovalOffered = true;
        Settings.Save();
        if (MessageBox.Show(Strings.Get("ClassicRemovalText"), "TextCase", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return false;
        AppPackage.StartClassicUninstall(uninstaller);
        return true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        s_saveTimer?.Stop();
        if (s_foregroundHook != IntPtr.Zero)
            UnhookWinEvent(s_foregroundHook);
        Hook?.Dispose();
        Tray?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    // ---------------------------------------------------------------- The window in front

    private static DispatcherTimer? s_remoteTimer;
    private static long s_remoteSince;
    private static bool s_remoteKeysLogged;

    /// <summary>
    /// The window in front changes: a remote desktop window installs its own keyboard hook, which takes the keys for the
    /// remote PC before any hook installed earlier sees them, so ours is installed again after it, to be called first:
    /// at once, then every second while that window stays in front (the client sets its hook again as its focus
    /// changes). Ours is also installed again for any other window: Windows removes a hook without a word when it once
    /// answered too slowly. The popup closes if the user went to another window.
    /// </summary>
    private static void WatchForeground()
    {
        s_foregroundChanged = (_, _, hwnd, _, _, _, _) =>
        {
            Hook.Reinstall();
            // A window of TextCase in front (the menu of the notification icon) leaves the popup open.
            GetWindowThreadProcessId(hwnd, out int processId);
            if (processId == Environment.ProcessId)
                return;
            // In developer mode the popup stays, for a screenshot (the capture tool comes to the front).
            if (!Settings.DeveloperMode)
                CasePopup.Current?.Finish(null, $"another window in front, {SwapLog.Describe(hwnd)}");
            if (!RemoteDesktop.IsRemoteWindow(hwnd))
            {
                s_remoteTimer?.Stop();
                return;
            }
            SwapLog.Write($"remote desktop in front ({SwapLog.Describe(hwnd)}): the hook is put first again, every second");
            s_remoteSince = Environment.TickCount64;
            s_remoteKeysLogged = false;
            if (s_remoteTimer is null)
            {
                s_remoteTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                s_remoteTimer.Tick += (_, _) =>
                {
                    Hook.Reinstall();
                    // Once per visit: the proof that the keys typed there reach TextCase (for the log).
                    if (!s_remoteKeysLogged && Matcher.LastKeyAt > s_remoteSince)
                    {
                        s_remoteKeysLogged = true;
                        SwapLog.Write("  keys typed in the remote desktop window reach TextCase");
                    }
                };
            }
            s_remoteTimer.Stop();
            s_remoteTimer.Start();
        };
        s_foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, s_foregroundChanged, 0, 0, WINEVENT_OUTOFCONTEXT);
    }

    // ---------------------------------------------------------------- Settings

    /// <summary>The shortcuts of the settings, given to the keyboard hook.</summary>
    internal static void ApplyShortcuts([System.Runtime.CompilerServices.CallerMemberName] string caller = "",
        [System.Runtime.CompilerServices.CallerFilePath] string file = "")
    {
        if (Matcher.Trace)
            SwapLog.Write($"trace: shortcuts applied by {Path.GetFileNameWithoutExtension(file)}.{caller}: paused {Settings.Paused}, popup {Settings.PopupShortcut}");
        var popup = Shortcut.Parse(Settings.PopupShortcut);
        var direct = new Dictionary<Shortcut, CaseMode>();
        foreach (var (mode, text) in Settings.DirectShortcuts)
        {
            var shortcut = Shortcut.Parse(text);
            if (shortcut.IsValid && shortcut != popup)
                direct.TryAdd(shortcut, mode);
        }
        Matcher.Current = new ShortcutMatcher.Config(popup, direct, Settings.DoubleCopy, Settings.DoubleCopyDelay, Settings.Paused);
        ShortcutsChanged?.Invoke(null, EventArgs.Empty);
    }

    internal static void ApplyRules()
    {
        Rules = new CaseRules(Settings.KeepWords, Settings.SmallWordsLowercase, Settings.SmartCases);
        RulesChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>The rules of the smart cases changed (the switch of the popup too): the window follows.</summary>
    public static event EventHandler? RulesChanged;

    internal static void SetSmart(bool smart)
    {
        Settings.SmartCases = smart;
        Settings.Save();
        ApplyRules();
    }

    /// <summary>
    /// Developer mode (About): the log (each swap, each key with Ctrl, Alt or Win; never the text), and the popup kept
    /// open until Esc or a choice, for a screenshot. Off: no log at all.
    /// </summary>
    internal static void SetDeveloperMode(bool on)
    {
        // The line saying it goes off is written before the log stops.
        if (!on)
            SwapLog.Write("developer mode off");
        Settings.DeveloperMode = on;
        Settings.Save();
        ApplyDeveloperMode();
        if (on)
            SwapLog.Write($"developer mode on, TextCase {Updater.CurrentVersion}");
    }

    private static void ApplyDeveloperMode()
    {
        SwapLog.Enabled = Settings.DeveloperMode;
        Matcher.Trace = Settings.DeveloperMode;
        Matcher.KeepPopupOpen = Settings.DeveloperMode;
    }

    /// <summary>
    /// The menu of File Explorer written again (this exe, the language, the cases of the popup): by the app installed
    /// from GitHub only, so that a development build never leaves a menu pointing to it. Not by the Store version: its
    /// registry writes stay in the package's own copy, which File Explorer never reads.
    /// </summary>
    internal static void RefreshExplorerMenu()
    {
        if (WindowsSettings.IsInstalled)
            ExplorerMenu.Apply(Settings.ExplorerMenuEnabled);
    }

    internal static void SetExplorerMenu(bool on)
    {
        Settings.ExplorerMenuEnabled = on;
        Settings.Save();
        ExplorerMenu.Apply(on);
    }

    internal static void SetPaused(bool paused)
    {
        Settings.Paused = paused;
        Settings.Save();
        ApplyShortcuts();
    }

    /// <summary>A setting changed in the window: applied at once, saved a moment later (typing in the word list).</summary>
    internal static void SaveSoon()
    {
        if (s_saveTimer is null)
        {
            s_saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            s_saveTimer.Tick += (_, _) =>
            {
                s_saveTimer.Stop();
                Settings.Save();
            };
        }
        s_saveTimer.Stop();
        s_saveTimer.Start();
    }

    // ---------------------------------------------------------------- Windows, theme, language

    /// <summary>The popup and the toast are dark: the theme of the app, or Windows' mode of the apps.</summary>
    internal static bool IsDark => (Environment.GetEnvironmentVariable("TEXTCASE_THEME") switch
    {
        "Light" => AppTheme.Light,
        "Dark" => AppTheme.Dark,
        _ => Settings.Theme,
    }) switch
    {
        AppTheme.Light => false,
        AppTheme.Dark => true,
        _ => WindowsSettings.AppsAreDark,
    };

    /// <summary>
    /// Loads the Fluent theme (light/dark/system) the first time the window opens: until then the app does not hold the
    /// theme's resources. TEXTCASE_THEME=Light|Dark forces one.
    /// </summary>
    public static void EnsureTheme()
    {
        if (s_themeApplied)
            return;
        s_themeApplied = true;
        ApplyTheme(Environment.GetEnvironmentVariable("TEXTCASE_THEME") switch
        {
            "Light" => AppTheme.Light,
            "Dark" => AppTheme.Dark,
            _ => Settings.Theme,
        });
    }

    /// <summary>Sets the theme of every window, at once: all colors come from the Fluent theme through DynamicResource.</summary>
    public static void ApplyTheme(AppTheme theme)
    {
        Current.ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
    }

    /// <summary>Sets the UI language: <see cref="AppSettings.SystemLanguage"/> or a culture name ("en", "fr").</summary>
    public static void ApplyLanguage(string language)
    {
        var culture = language == AppSettings.SystemLanguage ? SystemUICulture : CultureInfo.GetCultureInfo(language);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>
    /// Opens the app window, or brings it to the front. Texts are read once, when the window is built: after a language
    /// change, the window is built again on the same page.
    /// </summary>
    public static void OpenMainWindow(MainPage? page = null)
    {
        if (Current.Windows.OfType<MainWindow>().FirstOrDefault() is { } open)
        {
            if (page is { } requested)
                open.ShowPage(requested);
            if (open.WindowState == WindowState.Minimized)
                open.WindowState = WindowState.Normal;
            open.Activate();
            return;
        }

        EnsureTheme();
        var window = new MainWindow(page ?? MainPage.Shortcuts);
        window.Closed += (_, _) =>
        {
            Matcher.Suspended = false;
            if (window.LanguageChanged is { } reopenOn)
            {
                ApplyLanguage(Settings.Language);
                RefreshExplorerMenu(); // its labels
                Current.Dispatcher.BeginInvoke(() => OpenMainWindow(reopenOn), DispatcherPriority.ApplicationIdle);
            }
            else
                MemoryTrim.Soon();
        };
        window.Show();
        window.Activate();
    }

    /// <summary>Quits TextCase: the shortcuts and the notification icon go with it.</summary>
    public static void Quit() => Current.Shutdown();
}
