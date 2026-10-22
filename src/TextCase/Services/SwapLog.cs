using System.Diagnostics;
using System.Text;
using static TextCase.Services.NativeMethods;

namespace TextCase.Services;

/// <summary>
/// The log of developer mode (About), to understand a swap that failed (above all through the remote desktop): which
/// app, how long each step took, how long the text was, whether the paste was sent, the keys pressed with Ctrl, Alt or
/// Win. **Never the text itself.** In %LOCALAPPDATA%\TextCase\swap.log (next to the settings), cut back to its last
/// half beyond 100 KB. Developer mode off: nothing is written.
/// </summary>
internal static class SwapLog
{
    public static readonly string FilePath = Path.Combine(Path.GetDirectoryName(AppSettings.FilePath)!, "swap.log");

    private const long MaxBytes = 100 * 1024;

    /// <summary>Developer mode is on.</summary>
    public static bool Enabled { get; set; }

    public static void Write(string line)
    {
        if (!Enabled)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
            {
                var lines = File.ReadAllLines(FilePath);
                File.WriteAllLines(FilePath, lines[(lines.Length / 2)..]);
            }
            File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {line}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The app of a window, for the log: "notepad (Notepad)", "mstsc (TscShellContainerClass)".</summary>
    public static string Describe(IntPtr hwnd)
    {
        var name = new StringBuilder(64);
        GetClassName(hwnd, name, name.Capacity);
        GetWindowThreadProcessId(hwnd, out int processId);
        string process;
        try
        {
            using var p = Process.GetProcessById(processId);
            process = p.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            process = "?";
        }
        return $"{process} ({name})";
    }
}
