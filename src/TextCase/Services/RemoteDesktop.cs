using System.Diagnostics;
using System.Text;
using static TextCase.Services.NativeMethods;

namespace TextCase.Services;

/// <summary>
/// The windows of a remote desktop client: Remote Desktop Connection (mstsc) and the Remote Desktop / Windows App client
/// (msrdc). There, the copy and the paste happen on the remote PC and the text comes through the shared clipboard, which
/// takes longer: TextCase waits more for it.
/// </summary>
internal static class RemoteDesktop
{
    private static readonly string[] Processes = ["mstsc", "msrdc", "msrdcw"];

    private static int s_lastProcessId;
    private static bool s_lastIsRemote;

    public static bool IsRemoteWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;
        var name = new StringBuilder(64);
        if (GetClassName(hwnd, name, name.Capacity) > 0 && name.ToString() == "TscShellContainerClass")
            return true;
        GetWindowThreadProcessId(hwnd, out int processId);
        if (processId == s_lastProcessId)
            return s_lastIsRemote;
        bool remote;
        try
        {
            using var process = Process.GetProcessById(processId);
            remote = Processes.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            remote = false;
        }
        (s_lastProcessId, s_lastIsRemote) = (processId, remote);
        return remote;
    }
}
