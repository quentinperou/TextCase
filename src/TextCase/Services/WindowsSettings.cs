using Microsoft.Win32;

namespace TextCase.Services;

/// <summary>The Windows settings the app reads or changes: the start with Windows, the light or dark modes.</summary>
internal static class WindowsSettings
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "TextCase";
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>TextCase starts, in the notification area, when the user signs in (HKCU Run key).</summary>
    public static bool StartWithWindows
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) is string;
        }
        set
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (value && Environment.ProcessPath is { } exe)
                    key.SetValue(RunValue, $"\"{exe}\" {App.BackgroundArgument}");
                else
                    key.DeleteValue(RunValue, throwOnMissingValue: false);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
            }
        }
    }

    /// <summary>
    /// True when this exe is the installed one (Program Files): only then is the start with Windows turned on by
    /// default. A development build never registers itself.
    /// </summary>
    public static bool IsInstalled =>
        Environment.ProcessPath is { } exe
        && exe.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase);

    /// <summary>Apps are dark: "Choose your mode" of Windows, the apps part.</summary>
    public static bool AppsAreDark => !ReadLight("AppsUseLightTheme", light: true);

    /// <summary>The taskbar (and Start) is dark: "Choose your mode" of Windows, the Windows part (dark by default).</summary>
    public static bool TaskbarIsDark => !ReadLight("SystemUsesLightTheme", light: false);

    private static bool ReadLight(string name, bool light)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(name) is int value ? value != 0 : light;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return light;
        }
    }
}
