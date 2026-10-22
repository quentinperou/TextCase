using System.Windows;
using Microsoft.Win32;
using TextCase.Core;
using static TextCase.Services.NativeMethods;

namespace TextCase.Services;

/// <summary>
/// "Change the case of the name" in the right-click menu of File Explorer, on files and folders: the classic menu
/// (Show more options on Windows 11), written in the user's own registry, no administrator rights. Its submenu holds
/// the cases of the popup; each one starts <c>TextCase.exe --rename Upper "path"</c> once per selected item, which
/// renames and quits (the running TextCase is not involved).
/// </summary>
internal static class ExplorerMenu
{
    public const string Argument = "--rename";

    private static readonly string[] Keys = [@"Software\Classes\*\shell\TextCase", @"Software\Classes\Directory\shell\TextCase"];

    /// <summary>Written again from the settings (the cases of the popup, the language, this exe), or removed.</summary>
    public static void Apply(bool on)
    {
        Remove();
        if (!on || Environment.ProcessPath is not { } exe)
            return;
        var modes = CaseModes.All.Where(App.Settings.PopupModes.Contains).ToList();
        if (modes.Count == 0)
            modes = AppSettings.DefaultPopupModes();
        try
        {
            foreach (string path in Keys)
            {
                using var key = Registry.CurrentUser.CreateSubKey(path);
                key.SetValue("MUIVerb", Strings.Get("ExplorerMenuVerb"));
                key.SetValue("Icon", $"\"{exe}\",0");
                // Empty SubCommands: the submenu is the "shell" key below.
                key.SetValue("SubCommands", "");
                for (int i = 0; i < modes.Count; i++)
                {
                    using var verb = key.CreateSubKey($@"shell\{i:D2}{modes[i]}");
                    verb.SetValue("MUIVerb", Strings.Get("Mode" + modes[i]));
                    // Any number of selected items (Explorer hides a verb beyond 15 otherwise).
                    verb.SetValue("MultiSelectModel", "Player");
                    using var command = verb.CreateSubKey("command");
                    command.SetValue("", $"\"{exe}\" {Argument} {modes[i]} \"%1\"");
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            SwapLog.Write($"Explorer menu not written: {ex.Message}");
        }
    }

    public static void Remove()
    {
        foreach (string path in Keys)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
            }
        }
    }

    /// <summary>
    /// <c>--rename Mode "path"</c>, from the menu: the name of that file or folder in that case. A name already taken by
    /// another item is not overwritten: a message says so.
    /// </summary>
    public static void Rename(string[] args)
    {
        if (args.Length < 3 || !Enum.TryParse<CaseMode>(args[1], out var mode))
            return;
        string path = Path.TrimEndingDirectorySeparator(args[2]);
        bool isFolder = Directory.Exists(path);
        if (!isFolder && !File.Exists(path))
            return;
        string name = Path.GetFileName(path);
        var settings = App.Settings;
        var rules = new CaseRules(settings.KeepWords, settings.SmallWordsLowercase, settings.SmartCases);
        if (FileNames.Convert(name, isFolder, mode, rules) is not { } newName)
            return;
        string target = Path.Combine(Path.GetDirectoryName(path)!, newName);

        // A name that differs only by its case is the same item for Windows; any other existing name is another one.
        bool sameItem = string.Equals(name, newName, StringComparison.OrdinalIgnoreCase);
        if (!sameItem && (File.Exists(target) || Directory.Exists(target)))
        {
            MessageBox.Show(Strings.Format("RenameExists", newName), "TextCase", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if ((sameItem ? MoveCase(path, target) : MoveFileEx(path, target, 0)) is false)
        {
            var error = new System.ComponentModel.Win32Exception();
            MessageBox.Show(Strings.Format("RenameFailed", name, error.Message), "TextCase", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        // Explorer may miss a change of case only: tell it.
        SHChangeNotify(isFolder ? SHCNE_RENAMEFOLDER : SHCNE_RENAMEITEM, SHCNF_PATHW, path, target);
    }

    /// <summary>
    /// A change of the case only. NTFS takes it in place; FAT and exFAT (a USB key) take it for no change at all: so
    /// through a temporary name, back to the old name if the second step fails.
    /// </summary>
    private static bool MoveCase(string path, string target)
    {
        string temporary = path + ".textcase";
        if (!MoveFileEx(path, temporary, 0))
            return false;
        if (MoveFileEx(temporary, target, 0))
            return true;
        int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        MoveFileEx(temporary, path, 0);
        System.Runtime.InteropServices.Marshal.SetLastPInvokeError(error);
        return false;
    }
}
