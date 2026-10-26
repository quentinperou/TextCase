using Microsoft.Win32;

namespace TextCase.Services;

/// <summary>The Windows settings the app reads or changes: the start with Windows, the light or dark modes.</summary>
internal static class WindowsSettings
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "TextCase";
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Whether TextCase starts with Windows: the Run key, or the startup task of the Store version.</summary>
    public static Task<StartupState> GetStartupAsync() =>
        AppPackage.IsPackaged ? AppPackage.GetStartupAsync() : Task.FromResult(StartWithWindows ? StartupState.On : StartupState.Off);

    public static Task<StartupState> SetStartupAsync(bool on)
    {
        if (AppPackage.IsPackaged)
            return AppPackage.SetStartupAsync(on);
        StartWithWindows = on;
        return GetStartupAsync();
    }

    /// <summary>
    /// TextCase starts, in the notification area, when the user signs in (HKCU Run key). Not in the Store version: a
    /// package writes its own copy of HKCU, which Windows never reads at sign-in.
    /// </summary>
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
    /// True when this exe is the one installed from GitHub (Program Files): only then is the start with Windows turned
    /// on by default and the menu of File Explorer written. A development build never registers itself; the Store
    /// version (in Program Files\WindowsApps) has its startup task, and no menu.
    /// </summary>
    public static bool IsInstalled =>
        !AppPackage.IsPackaged
        && Environment.ProcessPath is { } exe
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
