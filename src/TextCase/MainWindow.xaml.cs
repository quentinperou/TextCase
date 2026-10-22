using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TextCase.Controls;
using TextCase.Core;
using TextCase.Services;

namespace TextCase;

/// <summary>The pages of the settings window, in the order of its menu.</summary>
public enum MainPage
{
    Shortcuts,
    Conversions,
    General,
    Updates,
    About,
}

/// <summary>
/// The settings window: the shortcuts, the conversions (a box to try them, the cases of the popup, the word list of the
/// smart cases) and the general settings. Pages are built in code from <see cref="Ui"/> rows; a change applies and
/// saves at once.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Dictionary<MainPage, FrameworkElement> _pages = [];

    public MainWindow(MainPage page)
    {
        InitializeComponent();
        MicaBackdrop.Apply(this);
        foreach (var p in Enum.GetValues<MainPage>())
            Nav.Items.Add(NavItem(p));
        ShowPage(page);
        App.ShortcutsChanged += ShortcutsChanged;
        Closed += (_, _) => App.ShortcutsChanged -= ShortcutsChanged;
        ShowState();
    }

    /// <summary>Set when the language was changed: the window is built again, on this page.</summary>
    public MainPage? LanguageChanged { get; private set; }

    public void ShowPage(MainPage page)
    {
        Nav.SelectedIndex = (int)page;
    }

    private MainPage CurrentPage => (MainPage)Math.Max(0, Nav.SelectedIndex);

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var page = CurrentPage;
        if (!_pages.TryGetValue(page, out var content))
        {
            content = page switch
            {
                MainPage.Shortcuts => BuildShortcutsPage(),
                MainPage.Conversions => BuildConversionsPage(),
                MainPage.Updates => BuildUpdatesPage(),
                MainPage.About => BuildAboutPage(),
                _ => BuildGeneralPage(),
            };
            _pages[page] = content;
        }
        PageHost.Content = content;
        Scroller.ScrollToTop();
    }

    private static ListBoxItem NavItem(MainPage page)
    {
        var (glyph, key) = page switch
        {
            MainPage.Shortcuts => ("", "PageShortcuts"),
            MainPage.Conversions => ("", "PageConversions"),
            MainPage.Updates => ("", "PageUpdates"),
            MainPage.About => ("", "PageAbout"),
            _ => ("", "PageGeneral"),
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 6, 4, 6) };
        row.Children.Add(new TextBlock { Text = glyph, FontSize = 16, Style = (Style)Application.Current.FindResource("IconText"), Margin = new Thickness(0, 0, 14, 0) });
        row.Children.Add(new TextBlock { Text = Strings.Get(key), FontSize = 14 });
        var item = new ListBoxItem { Content = row };
        System.Windows.Automation.AutomationProperties.SetName(item, Strings.Get(key));
        return item;
    }

    // ---------------------------------------------------------------- Shortcuts page

    private CheckBox? _active;
    private readonly List<(Func<Shortcut> Value, ShortcutBox Box)> _shortcutBoxes = [];

    private FrameworkElement BuildShortcutsPage()
    {
        var settings = App.Settings;
        var page = new StackPanel();
        page.Children.Add(Ui.PageTitle(Strings.Get("PageShortcuts")));

        var popup = Box(() => Shortcut.Parse(settings.PopupShortcut), shortcut =>
        {
            settings.PopupShortcut = shortcut.ToString();
            ShortcutsSaved();
        }, Strings.Get("PopupShortcut"));
        var doubleCopy = Ui.Toggle(settings.DoubleCopy, on =>
        {
            settings.DoubleCopy = on;
            ShortcutsSaved();
        }, Strings.Get("DoubleCopy"));
        var delay = Ui.Slider(200, 1000, settings.DoubleCopyDelay, AppSettings.DefaultDoubleCopyDelay, "ms", value =>
        {
            settings.DoubleCopyDelay = value;
            App.ApplyShortcuts();
            App.SaveSoon();
        }, Strings.Get("DoubleCopyDelay"));
        _active = Ui.Toggle(!settings.Paused, on => App.SetPaused(!on), Strings.Get("ShortcutsActive"));

        page.Children.Add(Ui.Group(
            Ui.Row("", Strings.Get("PopupShortcut"), Strings.Get("PopupShortcutDescription"), popup),
            Ui.Row("", Strings.Get("DoubleCopy"), Strings.Get("DoubleCopyDescription"), doubleCopy),
            Ui.Row("", Strings.Get("DoubleCopyDelay"), null, delay),
            Ui.Row("", Strings.Get("ShortcutsActive"), Strings.Get("ShortcutsActiveDescription"), _active)));

        page.Children.Add(Ui.Section(Strings.Get("DirectShortcuts")));
        page.Children.Add(Spaced(Ui.Description(Strings.Get("DirectShortcutsDescription"))));
        page.Children.Add(Ui.List(CaseModes.All.Select(mode =>
        {
            var box = Box(() => Shortcut.Parse(settings.DirectShortcuts.GetValueOrDefault(mode)), shortcut =>
            {
                if (shortcut.IsEmpty)
                    settings.DirectShortcuts.Remove(mode);
                else
                    settings.DirectShortcuts[mode] = shortcut.ToString();
                ShortcutsSaved();
            }, Strings.Get("Mode" + mode));
            return Ui.Row("", Strings.Get("Mode" + mode), null, box);
        })));
        return page;
    }

    private ShortcutBox Box(Func<Shortcut> value, Action<Shortcut> changed, string name)
    {
        var box = new ShortcutBox(value(), shortcut => UsedBy(shortcut, value()), changed, name);
        _shortcutBoxes.Add((value, box));
        return box;
    }

    // What already uses a shortcut (another case, the popup), to refuse it in a second place.
    private static string? UsedBy(Shortcut shortcut, Shortcut current)
    {
        if (shortcut == current)
            return null;
        var settings = App.Settings;
        if (Shortcut.Parse(settings.PopupShortcut) == shortcut)
            return Strings.Get("PopupShortcut");
        foreach (var (mode, text) in settings.DirectShortcuts)
        {
            if (Shortcut.Parse(text) == shortcut)
                return Strings.Get("Mode" + mode);
        }
        // Ctrl+C itself would never copy again.
        if (shortcut == new Shortcut(ShortcutModifiers.Ctrl, 'C') || shortcut == new Shortcut(ShortcutModifiers.Ctrl, 'V'))
            return Strings.Get("ShortcutCopyPaste");
        return null;
    }

    private static void ShortcutsSaved()
    {
        App.Settings.Save();
        App.ApplyShortcuts();
    }

    private void ShortcutsChanged(object? sender, EventArgs e)
    {
        ShowState();
        if (_active is not null)
            Ui.ShowToggle(_active, !App.Settings.Paused);
        foreach (var (value, box) in _shortcutBoxes)
            box.Show(value());
    }

    // Under the name of the app: the shortcut of the popup, or "Paused".
    private void ShowState()
    {
        var popup = Shortcut.Parse(App.Settings.PopupShortcut);
        StateLine.Text = App.Settings.Paused ? Strings.Get("StatePaused")
            : popup.IsValid ? ShortcutBox.Display(popup)
            : App.Settings.DoubleCopy ? Strings.Get("DoubleCopy") : "";
    }

    // ---------------------------------------------------------------- Conversions page

    private TextBox? _tryText;
    private Grid? _tryResults;
    private DispatcherTimer? _wordsTimer;

    private FrameworkElement BuildConversionsPage()
    {
        var settings = App.Settings;
        var page = new StackPanel();
        page.Children.Add(Ui.PageTitle(Strings.Get("PageConversions")));

        // A box to try the cases (and the shortcuts: select in it, then Ctrl+Alt+C).
        _tryText = new TextBox
        {
            Text = "CANON EOS R6 MARK II BOÎTIER NU",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 36,
            MaxHeight = 120,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 10, 0, 12),
        };
        System.Windows.Automation.AutomationProperties.SetName(_tryText, Strings.Get("TryIt"));
        _tryText.TextChanged += (_, _) => ShowTry();
        _tryResults = new Grid();
        _tryResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        _tryResults.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var tryPanel = new StackPanel();
        tryPanel.Children.Add(Ui.Row("", Strings.Get("TryIt"), Strings.Get("TryItDescription")));
        tryPanel.Children.Add(_tryText);
        tryPanel.Children.Add(_tryResults);
        page.Children.Add(new Border { Style = (Style)Application.Current.FindResource("Card"), Child = tryPanel });
        ShowTry();

        // The cases shown in the popup.
        page.Children.Add(Ui.Section(Strings.Get("PopupModes")));
        var modes = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var mode in CaseModes.All)
        {
            var check = new CheckBox
            {
                // A TextBlock: as text, "CONSTANT_CASE" would lose its "_" to an access key.
                Content = new TextBlock { Text = Strings.Get("Mode" + mode) },
                IsChecked = settings.PopupModes.Contains(mode),
                Width = 200,
                Margin = new Thickness(0, 2, 8, 2),
            };
            System.Windows.Automation.AutomationProperties.SetName(check, Strings.Get("Mode" + mode));
            void Changed(object sender, RoutedEventArgs e)
            {
                settings.PopupModes.Remove(mode);
                if (check.IsChecked == true)
                    settings.PopupModes.Add(mode);
                settings.Save();
            }
            check.Checked += Changed;
            check.Unchecked += Changed;
            modes.Children.Add(check);
        }
        var modesPanel = new StackPanel();
        modesPanel.Children.Add(Ui.Row("", Strings.Get("PopupModes"), Strings.Get("PopupModesDescription")));
        modesPanel.Children.Add(modes);
        page.Children.Add(new Border { Style = (Style)Application.Current.FindResource("Card"), Child = modesPanel });

        // The rules of Title Case and Sentence case, used only in the smart mode.
        page.Children.Add(Ui.Section(Strings.Get("SmartCases")));
        _smart = Ui.Toggle(settings.SmartCases, App.SetSmart, Strings.Get("SmartMode"));
        page.Children.Add(Ui.Card("", Strings.Get("SmartMode"), Strings.Get("SmartModeDescription"), _smart));
        var small = Ui.Toggle(settings.SmallWordsLowercase, on =>
        {
            settings.SmallWordsLowercase = on;
            settings.Save();
            App.ApplyRules();
        }, Strings.Get("SmallWords"));
        _smallCard = Ui.Card("", Strings.Get("SmallWords"), Strings.Get("SmallWordsDescription"), small);
        page.Children.Add(_smallCard);

        var words = new TextBox
        {
            Text = string.Join(Environment.NewLine, settings.KeepWords),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 180,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(38, 10, 0, 2),
        };
        System.Windows.Automation.AutomationProperties.SetName(words, Strings.Get("KeepWords"));
        words.TextChanged += (_, _) =>
        {
            // Applied once the typing stops.
            _wordsTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _wordsTimer.Stop();
            _wordsTimer.Tick -= WordsTyped;
            _wordsTimer.Tick += WordsTyped;
            _wordsTimer.Start();
        };
        void WordsTyped(object? sender, EventArgs e)
        {
            _wordsTimer!.Stop();
            settings.KeepWords = CaseRules.ParseWords(words.Text);
            settings.Save();
            App.ApplyRules();
        }
        var reset = Ui.Button(Strings.Get("KeepWordsReset"), () => words.Text = string.Join(Environment.NewLine, CaseRules.DefaultKeepWords));
        var wordsPanel = new StackPanel();
        wordsPanel.Children.Add(Ui.Row("", Strings.Get("KeepWords"), Strings.Get("KeepWordsDescription"), reset));
        wordsPanel.Children.Add(words);
        _wordsCard = new Border { Style = (Style)Application.Current.FindResource("Card"), Child = wordsPanel };
        page.Children.Add(_wordsCard);

        App.RulesChanged += RulesChanged;
        Closed += (_, _) => App.RulesChanged -= RulesChanged;
        RulesChanged(null, EventArgs.Empty);
        return page;
    }

    private CheckBox? _smart;
    private Border? _smallCard;
    private Border? _wordsCard;

    // The smart mode changed (here or by the switch of the popup): the results and the cards of its rules follow.
    private void RulesChanged(object? sender, EventArgs e)
    {
        bool smart = App.Settings.SmartCases;
        if (_smart is not null)
            Ui.ShowToggle(_smart, smart);
        if (_smallCard is not null)
            _smallCard.IsEnabled = smart;
        if (_wordsCard is not null)
            _wordsCard.IsEnabled = smart;
        ShowTry();
    }

    // Each case of the text of the "Try it" box, as the popup would show them.
    private void ShowTry()
    {
        if (_tryText is null || _tryResults is null)
            return;
        _tryResults.Children.Clear();
        _tryResults.RowDefinitions.Clear();
        string text = _tryText.Text;
        int row = 0;
        foreach (var mode in CaseModes.All)
        {
            _tryResults.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var name = Ui.Description(Strings.Get("Mode" + mode));
            name.Margin = new Thickness(38, 3, 8, 3);
            var result = new TextBox
            {
                Text = CaseConverter.Convert(text, mode, App.Rules).Replace("\r\n", " ↵ ").Replace("\n", " ↵ "),
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 3, 0, 3),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(name, row);
            Grid.SetRow(result, row);
            Grid.SetColumn(result, 1);
            _tryResults.Children.Add(name);
            _tryResults.Children.Add(result);
            row++;
        }
    }

    private static FrameworkElement Spaced(FrameworkElement element)
    {
        element.Margin = new Thickness(2, -4, 0, 8);
        return element;
    }

    // ---------------------------------------------------------------- General page

    private FrameworkElement BuildGeneralPage()
    {
        var settings = App.Settings;
        var page = new StackPanel();
        page.Children.Add(Ui.PageTitle(Strings.Get("PageGeneral")));

        page.Children.Add(Ui.Card("", Strings.Get("StartWithWindows"), Strings.Get("StartWithWindowsHint"),
            Ui.Toggle(WindowsSettings.StartWithWindows, on => WindowsSettings.StartWithWindows = on, Strings.Get("StartWithWindows"))));

        page.Children.Add(Ui.Card("", Strings.Get("RestoreClipboard"), Strings.Get("RestoreClipboardDescription"),
            Ui.Toggle(settings.RestoreClipboard, on =>
            {
                settings.RestoreClipboard = on;
                settings.Save();
            }, Strings.Get("RestoreClipboard"))));

        page.Children.Add(Ui.Card("", Strings.Get("Language"), null,
            Ui.Choice(
                [(AppSettings.SystemLanguage, Strings.Get("LanguageSystem")), ("en", "English"), ("fr", "Français")],
                settings.Language, language =>
                {
                    if (language == settings.Language)
                        return;
                    settings.Language = language;
                    settings.Save();
                    LanguageChanged = CurrentPage;
                    Dispatcher.BeginInvoke(Close);
                })));

        page.Children.Add(Ui.Card("", Strings.Get("Theme"), null,
            Ui.Choice(
                [(AppTheme.System, Strings.Get("ThemeSystem")), (AppTheme.Light, Strings.Get("ThemeLight")), (AppTheme.Dark, Strings.Get("ThemeDark"))],
                settings.Theme, theme =>
                {
                    settings.Theme = theme;
                    settings.Save();
                    App.ApplyTheme(theme);
                })));

        page.Children.Add(Ui.Card("", Strings.Get("MenuQuit"), Strings.Get("QuitDescription"),
            Ui.Button(Strings.Get("MenuQuit"), App.Quit)));

        return page;
    }

    // ---------------------------------------------------------------- Updates page

    // As in ScreenClip and PageCompose: one card, the state and the check / install button first, then the check at
    // startup and the channel.
    private TextBlock? _updateStatus;
    private TextBlock? _updateNotes;
    private ProgressBar? _updateProgress;
    private Button? _updateButton;
    private bool _updating;

    private FrameworkElement BuildUpdatesPage()
    {
        var settings = App.Settings;
        var page = new StackPanel();
        page.Children.Add(Ui.PageTitle(Strings.Get("PageUpdates")));

        _updateStatus = Ui.Description("");
        _updateNotes = Ui.Description("");
        var notes = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(Strings.Get("UpdateWhatsNew")));
        notes.Click += (_, _) =>
        {
            if (Updater.Available is { } release)
                Updater.OpenReleasePage(release);
        };
        _updateNotes.Inlines.Clear();
        _updateNotes.Inlines.Add(notes);
        _updateProgress = new ProgressBar { Maximum = 1, Margin = new Thickness(0, 8, 0, 2), Visibility = Visibility.Collapsed };
        _updateButton = new Button { MinWidth = 220 };
        _updateButton.Click += async (_, _) =>
        {
            if (Updater.Available is { } release)
                await InstallUpdateAsync(release);
            else
                await CheckForUpdatesAsync();
        };
        var texts = Ui.Texts(Strings.Get("PageUpdates"));
        texts.Children.Add(_updateStatus);
        texts.Children.Add(_updateNotes);
        texts.Children.Add(_updateProgress);

        var atStartup = Ui.Toggle(settings.CheckForUpdatesAtStartup, on =>
        {
            settings.CheckForUpdatesAtStartup = on;
            settings.Save();
        }, Strings.Get("UpdateCheckAtStartup"));
        var channel = Ui.Choice(
            [(UpdateChannel.Stable, Strings.Get("UpdateChannelStable")), (UpdateChannel.Beta, Strings.Get("UpdateChannelBeta"))],
            settings.UpdateChannel, async value =>
            {
                if (value == settings.UpdateChannel)
                    return;
                settings.UpdateChannel = value;
                settings.Save();
                // What is offered depends on the channel: look again.
                if (!_updating)
                    await CheckForUpdatesAsync();
            }, 220);
        System.Windows.Automation.AutomationProperties.SetName(channel, Strings.Get("UpdateChannel"));

        page.Children.Add(Ui.Group(
            Ui.Row("", texts, _updateButton),
            Ui.Row("", Strings.Get("UpdateCheckAtStartup"), Strings.Get("UpdateCheckAtStartupDescription"), atStartup),
            Ui.Row("", Strings.Get("UpdateChannel"), Strings.Get("UpdateChannelDescription"), channel)));

        Updater.Checked += UpdaterChecked;
        Closed += (_, _) => Updater.Checked -= UpdaterChecked;
        ShowUpdateState();
        return page;
    }

    private void UpdaterChecked(object? sender, EventArgs e) => ShowUpdateState();

    /// <summary>Shows what the last check found, or <paramref name="status"/> (progress, error) instead.</summary>
    private void ShowUpdateState(string? status = null)
    {
        if (_updateStatus is null || _updateNotes is null || _updateButton is null)
            return;
        var available = Updater.Available;
        _updateStatus.Text = status
            ?? (available is not null ? Strings.Format("UpdateStatusAvailable", Updater.CurrentVersion, available.Version)
                : Updater.HasChecked ? Strings.Format("UpdateStatusUpToDate", Updater.CurrentVersion)
                : Strings.Format("UpdateStatusIdle", Updater.CurrentVersion));
        _updateNotes.Visibility = available is not null && status is null ? Visibility.Visible : Visibility.Collapsed;
        _updateButton.Content = available is null ? Strings.Get("UpdateCheck") : Strings.Get("UpdateInstall");
        if (available is null)
            _updateButton.ClearValue(StyleProperty);
        else
            _updateButton.SetResourceReference(StyleProperty, "AccentButtonStyle");
        _updateButton.IsEnabled = !_updating;
    }

    private async Task CheckForUpdatesAsync()
    {
        _updating = true;
        ShowUpdateState(Strings.Get("UpdateStatusChecking"));
        string? error = null;
        try
        {
            await Updater.CheckAsync(App.Settings.UpdateChannel);
        }
        catch (Exception ex)
        {
            error = Strings.Format("UpdateStatusFailed", Updater.CurrentVersion, Updater.Describe(ex));
        }
        _updating = false;
        ShowUpdateState(error);
    }

    private async Task InstallUpdateAsync(ReleaseInfo release)
    {
        if (_updateProgress is null || _updateStatus is null)
            return;
        _updating = true;
        ShowUpdateState(Strings.Get("UpdateDownloading"));
        _updateProgress.Value = 0;
        _updateProgress.IsIndeterminate = false;
        _updateProgress.Visibility = Visibility.Visible;
        var progress = new Progress<double>(p =>
        {
            _updateProgress.Value = p;
            if (p < 1)
                return;
            // Downloaded: the installer now waits for the UAC prompt, then installs.
            _updateProgress.IsIndeterminate = true;
            _updateStatus.Text = Strings.Get("UpdateStarting");
        });

        string status;
        try
        {
            if (await Updater.InstallAsync(release, progress))
                return; // the app is closing, the installer takes over
            status = Strings.Get("UpdateCancelled");
        }
        catch (Exception ex)
        {
            status = Strings.Format("ErrorUpdate", Updater.Describe(ex));
        }
        _updating = false;
        _updateProgress.Visibility = Visibility.Collapsed;
        ShowUpdateState(status);
    }

    // ---------------------------------------------------------------- About page

    // As in ScreenClip: the app, its version and its links.
    private FrameworkElement BuildAboutPage()
    {
        var page = new StackPanel();
        page.Children.Add(Ui.PageTitle(Strings.Get("PageAbout")));

        var about = new DockPanel();
        var icon = new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/TextCase.png")),
            Width = 40, Height = 40, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Top,
        };
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
        DockPanel.SetDock(icon, Dock.Left);
        about.Children.Add(icon);
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock { Text = "TextCase", FontWeight = FontWeights.SemiBold });
        texts.Children.Add(Ui.Description(Strings.Format("AboutVersion", Updater.CurrentVersion)));
        var developedBy = Ui.Description("");
        developedBy.Inlines.Clear();
        developedBy.Inlines.Add(new System.Windows.Documents.Run(Strings.Get("AboutDevelopedBy")));
        developedBy.Inlines.Add(Link("QuentinPerou", "https://github.com/quentinperou"));
        texts.Children.Add(developedBy);
        var source = Ui.Description("");
        source.Inlines.Clear();
        source.Inlines.Add(Link(Strings.Get("AboutSourceCode"), "https://github.com/quentinperou/TextCase"));
        texts.Children.Add(source);
        about.Children.Add(texts);
        page.Children.Add(new Border { Style = (Style)Application.Current.FindResource("Card"), Child = about });
        return page;
    }

    private static System.Windows.Documents.Hyperlink Link(string text, string url)
    {
        var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(text)) { NavigateUri = new Uri(url) };
        link.RequestNavigate += (_, e) => OpenUrl(e.Uri.AbsoluteUri);
        return link;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Debug.WriteLine($"Open failed: {ex.Message}");
        }
    }
}
