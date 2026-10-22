using System.Text.Json;
using System.Text.Json.Serialization;
using TextCase.Core;

namespace TextCase.Services;

/// <summary>Light or dark appearance of the app window and the popup; <see cref="System"/> follows Windows.</summary>
public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>
/// User preferences, saved in %LOCALAPPDATA%\TextCase\settings.json. The app keeps one instance for its whole run
/// (<see cref="App.Settings"/>): the window changes it and saves it, and the change applies at once.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Follow the Windows display language.</summary>
    public const string SystemLanguage = "system";

    /// <summary>UI languages with a translation (Properties/Strings*.resx). English is the default.</summary>
    public static readonly string[] SupportedLanguages = ["en", "fr"];

    public const string DefaultPopupShortcut = "Ctrl+Alt+C";
    public const int DefaultDoubleCopyDelay = 500;

    /// <summary>The direct shortcuts of a new install: the four basic cases.</summary>
    public static Dictionary<CaseMode, string> DefaultDirectShortcuts() => new()
    {
        [CaseMode.Upper] = "Ctrl+Alt+U",
        [CaseMode.Lower] = "Ctrl+Alt+L",
        [CaseMode.Title] = "Ctrl+Alt+T",
        [CaseMode.Sentence] = "Ctrl+Alt+S",
    };

    /// <summary>
    /// %LOCALAPPDATA%\TextCase\settings.json, or the file named by TEXTCASE_SETTINGS: the test runs of Claude use their
    /// own (.local\test-settings.json), never the author's. The swap log sits next to it.
    /// </summary>
    public static readonly string FilePath = Environment.GetEnvironmentVariable("TEXTCASE_SETTINGS") is { Length: > 0 } test
        ? Path.GetFullPath(test)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TextCase", "settings.json");

    /// <summary><see cref="SystemLanguage"/> or one of <see cref="SupportedLanguages"/>.</summary>
    public string Language { get; set; } = SystemLanguage;

    [JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>The shortcut of the popup ("Ctrl+Alt+C"); empty: none.</summary>
    public string PopupShortcut { get; set; } = DefaultPopupShortcut;

    /// <summary>Ctrl+C twice in a row opens the popup on what was copied.</summary>
    public bool DoubleCopy { get; set; } = true;

    /// <summary>The longest time between the two Ctrl+C, in milliseconds.</summary>
    public int DoubleCopyDelay { get; set; } = DefaultDoubleCopyDelay;

    /// <summary>One shortcut per case, converting without the popup.</summary>
    public Dictionary<CaseMode, string> DirectShortcuts { get; set; } = DefaultDirectShortcuts();

    /// <summary>All the shortcuts off for a while (the menu of the notification icon).</summary>
    public bool Paused { get; set; }

    /// <summary>The cases shown in the popup (in the order of <see cref="CaseModes.All"/>).</summary>
    public List<CaseMode> PopupModes { get; set; } = DefaultPopupModes();

    /// <summary>The popup of a new install: the four basic cases only, the others ticked by the user (his wish).</summary>
    public static List<CaseMode> DefaultPopupModes() => [CaseMode.Upper, CaseMode.Lower, CaseMode.Title, CaseMode.Sentence];

    /// <summary>
    /// Title Case and Sentence case keep the words of the list, acronyms, digits (the popup's switch). Off by default:
    /// the author's wish, every letter changes until he turns it on.
    /// </summary>
    public bool SmartCases { get; set; }

    /// <summary>The case chosen last time, highlighted when the popup opens.</summary>
    public CaseMode? LastMode { get; set; }

    /// <summary>The words kept as written by Title Case and Sentence case (<see cref="CaseRules"/>).</summary>
    public List<string> KeepWords { get; set; } = [.. CaseRules.DefaultKeepWords];

    /// <summary>The small words of a title (de, la, of, the…) stay in lowercase.</summary>
    public bool SmallWordsLowercase { get; set; } = true;

    /// <summary>What the clipboard held is put back after a swap (TextCase copies the selection to read it).</summary>
    public bool RestoreClipboard { get; set; } = true;

    /// <summary>
    /// Set once the “Start with Windows” default (on) has been applied, so that turning it off in Settings stays off.
    /// </summary>
    public bool StartupConfigured { get; set; }

    /// <summary>Ask GitHub for a newer version when the app starts.</summary>
    public bool CheckForUpdatesAtStartup { get; set; } = true;

    /// <summary>Releases offered as updates: stable only, or betas too.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<UpdateChannel>))]
    public UpdateChannel UpdateChannel { get; set; } = UpdateChannel.Stable;

    // Settings of a newer version (a dev build, a beta) are written back as they are, not dropped.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? OtherSettings { get; set; }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // A missing or damaged file just means default settings: preferences are never worth an error message.
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath) && JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) is { } settings)
            {
                if (!SupportedLanguages.Contains(settings.Language))
                    settings.Language = SystemLanguage;
                settings.PopupShortcut ??= "";
                settings.DirectShortcuts ??= [];
                settings.PopupModes = [.. (settings.PopupModes ?? DefaultPopupModes()).Where(Enum.IsDefined).Distinct()];
                settings.KeepWords ??= [.. CaseRules.DefaultKeepWords];
                settings.DoubleCopyDelay = Math.Clamp(settings.DoubleCopyDelay, 200, 1000);
                return settings;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SwapLog.Write($"settings not saved: {ex.Message}");
        }
    }
}
