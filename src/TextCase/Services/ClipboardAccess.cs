using System.Runtime.InteropServices;
using static TextCase.Services.NativeMethods;

namespace TextCase.Services;

/// <summary>
/// The clipboard through the Windows functions, not WPF's: the text only, the formats that keep a converted text out of
/// the clipboard history (Win + V), and a copy of what the user had copied, put back after a swap. On the remote desktop,
/// the clipboard of this PC is shared with the remote one (rdpclip): reading it fetches the remote copy.
/// </summary>
internal static class ClipboardAccess
{
    private static readonly int CanIncludeInHistory = RegisterClipboardFormat("CanIncludeInClipboardHistory");
    private static readonly int CanUploadToCloud = RegisterClipboardFormat("CanUploadToCloudClipboard");

    // What is copied back after a swap: text (plain, HTML, RTF), files, images. Not the private formats of an app
    // (rendering all of Excel's would take seconds).
    private static readonly int[] KeptFormats =
    [
        CF_UNICODETEXT, CF_LOCALE, CF_HDROP, CF_DIB,
        RegisterClipboardFormat("HTML Format"),
        RegisterClipboardFormat("Rich Text Format"),
        RegisterClipboardFormat("PNG"),
        RegisterClipboardFormat("Preferred DropEffect"),
        RegisterClipboardFormat("Shell IDList Array"),
        RegisterClipboardFormat("FileNameW"),
    ];

    private const long MaxSnapshotBytes = 64L * 1024 * 1024;

    /// <summary>Changes each time anything is copied (by any app).</summary>
    public static uint Sequence => GetClipboardSequenceNumber();

    /// <summary>When the clipboard last changed (Environment.TickCount64), told by the hidden window of the tray.</summary>
    public static long LastChangeAt { get; private set; }

    public static void MarkChanged() => LastChangeAt = Environment.TickCount64;

    public static string? GetText(IntPtr owner)
    {
        if (!Open(owner))
            return null;
        try
        {
            var handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == IntPtr.Zero)
                return null;
            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
                return null;
            try
            {
                int chars = (int)((long)GlobalSize(handle) / 2);
                string text = Marshal.PtrToStringUni(pointer, chars);
                int end = text.IndexOf('\0');
                return end >= 0 ? text[..end] : text;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>
    /// The clipboard holds <paramref name="text"/> only (no formatting). <paramref name="keepOutOfHistory"/>: not
    /// listed in the clipboard history nor sent to the cloud clipboard (a step of a swap, not something the user copied).
    /// </summary>
    public static bool SetText(IntPtr owner, string text, bool keepOutOfHistory)
    {
        if (!Open(owner))
            return false;
        try
        {
            EmptyClipboard();
            var bytes = new byte[(text.Length + 1) * 2];
            System.Text.Encoding.Unicode.GetBytes(text, 0, text.Length, bytes, 0);
            bool done = Put(CF_UNICODETEXT, bytes);
            if (keepOutOfHistory)
                KeepOutOfHistory();
            return done;
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>What the clipboard holds now (its usual formats), to put it back later; null if nothing to keep.</summary>
    public static IReadOnlyList<(int Format, byte[] Data)>? Take(IntPtr owner)
    {
        if (!Open(owner))
            return null;
        try
        {
            var kept = new List<(int, byte[])>();
            long total = 0;
            foreach (int format in KeptFormats)
            {
                if (format == 0 || !IsClipboardFormatAvailable(format))
                    continue;
                var handle = GetClipboardData(format);
                if (handle == IntPtr.Zero)
                    continue;
                long size = (long)GlobalSize(handle);
                if (size <= 0 || total + size > MaxSnapshotBytes)
                    continue;
                var pointer = GlobalLock(handle);
                if (pointer == IntPtr.Zero)
                    continue;
                var data = new byte[size];
                Marshal.Copy(pointer, data, 0, (int)size);
                GlobalUnlock(handle);
                kept.Add((format, data));
                total += size;
            }
            return kept.Count > 0 ? kept : null;
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>Puts back what <see cref="Take"/> kept (out of the history: it is already there).</summary>
    public static void Restore(IntPtr owner, IReadOnlyList<(int Format, byte[] Data)> snapshot)
    {
        if (!Open(owner))
            return;
        try
        {
            EmptyClipboard();
            foreach (var (format, data) in snapshot)
                Put(format, data);
            KeepOutOfHistory();
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static void KeepOutOfHistory()
    {
        byte[] zero = [0, 0, 0, 0];
        if (CanIncludeInHistory != 0)
            Put(CanIncludeInHistory, zero);
        if (CanUploadToCloud != 0)
            Put(CanUploadToCloud, zero);
    }

    // Another app may hold the clipboard for a moment (a clipboard manager reading it): a few tries.
    private static bool Open(IntPtr owner)
    {
        for (int i = 0; i < 20; i++)
        {
            if (OpenClipboard(owner))
                return true;
            Thread.Sleep(15);
        }
        return false;
    }

    private static bool Put(int format, byte[] data)
    {
        var memory = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)data.Length);
        if (memory == IntPtr.Zero)
            return false;
        var pointer = GlobalLock(memory);
        if (pointer == IntPtr.Zero)
        {
            GlobalFree(memory);
            return false;
        }
        Marshal.Copy(data, 0, pointer, data.Length);
        GlobalUnlock(memory);
        // Once set, the memory belongs to the clipboard.
        if (SetClipboardData(format, memory) != IntPtr.Zero)
            return true;
        GlobalFree(memory);
        return false;
    }
}
