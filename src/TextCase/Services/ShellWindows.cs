using System.Diagnostics;
using static TextCase.Services.NativeMethods;

namespace TextCase.Services;

/// <summary>
/// Start and its search box. Windows draws them in a layer above every window of an app, even topmost, even owned by
/// the taskbar (tried in 0.1.2: the popup stayed behind the search box): the popup opens beside them.
/// </summary>
internal static class ShellWindows
{
    private static readonly string[] StartProcesses = ["SearchHost", "StartMenuExperienceHost", "ShellExperienceHost", "SearchApp"];

    public static bool IsStartOrSearch(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;
        GetWindowThreadProcessId(hwnd, out int processId);
        try
        {
            using var process = Process.GetProcessById(processId);
            return StartProcesses.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}
