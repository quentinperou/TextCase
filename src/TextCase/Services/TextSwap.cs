using System.Diagnostics;
using TextCase.Core;
using static TextCase.Services.NativeMethods;

namespace TextCase.Services;

/// <summary>
/// One change of case, from a shortcut to the pasted result: the selection is copied (Ctrl+C sent to the app in front,
/// or copied by the user for Ctrl+C twice), converted (by the popup's choice or a direct shortcut), then pasted over the
/// selection as plain text (Ctrl+V), and what the clipboard held before is put back. In a remote desktop window the copy
/// and the paste happen on the remote PC: the text comes and goes through the shared clipboard, so each step waits
/// longer. Runs on the UI thread; its waits are awaited, never blocking.
/// </summary>
internal sealed class TextSwap
{
    private bool _busy;

    public async Task RunAsync(Trigger trigger)
    {
        if (_busy)
        {
            SwapLog.Write($"{trigger.Kind}: ignored, a change of case is still running");
            return;
        }
        _busy = true;
        try
        {
            await SwapAsync(trigger);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Swap failed: {ex}");
            SwapLog.Write($"  failed: {ex.GetType().Name}: {ex.Message} {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
        }
        finally
        {
            _busy = false;
            MemoryTrim.Soon();
        }
    }

    private static async Task SwapAsync(Trigger trigger)
    {
        var settings = App.Settings;
        IntPtr owner = App.Tray.Handle;
        IntPtr target = GetForegroundWindow();
        bool remote = RemoteDesktop.IsRemoteWindow(target);
        var anchor = Anchor(target, remote);
        IReadOnlyList<(int, byte[])>? saved = null;
        string? text;
        var watch = Stopwatch.StartNew();
        SwapLog.Write($"{trigger.Kind}{(trigger.Kind == TriggerKind.Direct ? " " + trigger.Mode : "")} in {SwapLog.Describe(target)}, remote: {remote}"
            + (remote && GetWindowRect(target, out var window)
                ? $" (window {window.Left},{window.Top} → {window.Right},{window.Bottom}, topmost: {(GetWindowLongPtr(target, GWL_EXSTYLE).ToInt64() & WS_EX_TOPMOST) != 0})"
                : ""));

        if (trigger.Kind == TriggerKind.DoubleCopy)
        {
            // The first Ctrl+C has copied the selection already (the clipboard changed since then): go on at once.
            // Else its copy is on its way (from a remote PC, through the shared clipboard): wait for it.
            bool copied = Environment.TickCount64 - ClipboardAccess.LastChangeAt <= settings.DoubleCopyDelay + 300;
            if (!copied)
                copied = await WaitForChangeAsync(ClipboardAccess.Sequence, remote ? 1500 : 400);
            text = ClipboardAccess.GetText(owner);
            // From a remote PC the shared clipboard may still be on its way (its formats announced, the text not yet).
            if (string.IsNullOrEmpty(text) && remote && await WaitForChangeAsync(ClipboardAccess.Sequence, 1500))
                text = ClipboardAccess.GetText(owner);
            SwapLog.Write($"  copy by the app: {(copied ? "there" : "none")} at {watch.ElapsedMilliseconds} ms");
        }
        else
        {
            // Ctrl+Alt still held would make Ctrl+Alt+C of our Ctrl+C.
            await WaitForModifiersAsync();
            if (settings.RestoreClipboard)
                saved = ClipboardAccess.Take(owner);
            SwapLog.Write($"  keys released, clipboard kept ({saved?.Count ?? 0} formats) at {watch.ElapsedMilliseconds} ms");
            uint before = ClipboardAccess.Sequence;
            bool sent = Input.Copy();
            bool changed = await WaitForChangeAsync(before, remote ? 2500 : 800);
            text = changed ? ClipboardAccess.GetText(owner) : null;
            SwapLog.Write($"  Ctrl+C {(sent ? "sent" : "refused by Windows")}, clipboard {(changed ? "changed" : "unchanged")} at {watch.ElapsedMilliseconds} ms");
        }

        if (string.IsNullOrEmpty(text))
        {
            if (saved is not null)
                ClipboardAccess.Restore(owner, saved);
            SwapLog.Write("  no text: stopped");
            Toast.Show(Strings.Get("ToastNothingSelected"), anchor);
            return;
        }
        SwapLog.Write($"  text read: {text.Length} characters, {text.Count(c => c == '\n') + 1} line(s)");

        CaseMode mode;
        if (trigger.Kind == TriggerKind.Direct)
            mode = trigger.Mode;
        else
        {
            // Start and its search box are drawn above every window of an app: the popup goes beside them.
            RECT? beside = ShellWindows.IsStartOrSearch(target) && GetWindowRect(target, out var start) ? start : null;
            var chosen = await CasePopup.ChooseAsync(text, anchor, beside);
            if (chosen is null)
            {
                if (saved is not null)
                    ClipboardAccess.Restore(owner, saved);
                SwapLog.Write("  popup closed without a choice");
                return;
            }
            mode = chosen.Value;
            if (settings.LastMode != mode)
            {
                settings.LastMode = mode;
                settings.Save();
            }
            watch.Restart();
        }

        string result = CaseConverter.Convert(text, mode, App.Rules);
        // On a remote desktop, rdpclip copies the clipboard over: leave it as an ordinary one.
        if (!ClipboardAccess.SetText(owner, result, keepOutOfHistory: saved is not null && !remote))
        {
            SwapLog.Write($"  {mode}: the clipboard could not be written");
            return;
        }
        uint ours = ClipboardAccess.Sequence;
        // The remote PC gets the new clipboard through a channel of its own: it must be there before the paste.
        await Task.Delay(remote ? 400 : 30);
        var front = GetForegroundWindow();
        if (front != target)
        {
            // The user went to another app meanwhile: the result stays in the clipboard, pasted nowhere.
            SwapLog.Write($"  {mode}: not pasted, {SwapLog.Describe(front)} is now in front");
            return;
        }
        await WaitForModifiersAsync();
        bool pasted = Input.Paste();
        SwapLog.Write($"  {mode}: {result.Length} characters, Ctrl+V {(pasted ? "sent" : "refused by Windows")} at {watch.ElapsedMilliseconds} ms (smart: {App.Rules.Smart})");

        if (saved is not null)
        {
            // The app reads the clipboard when it handles Ctrl+V, a little later; the remote PC much later.
            await Task.Delay(remote ? 2500 : 700);
            bool untouched = ClipboardAccess.Sequence == ours;
            if (untouched)
                ClipboardAccess.Restore(owner, saved);
            SwapLog.Write($"  clipboard {(untouched ? "put back" : "left: copied again meanwhile")}");
        }
    }

    private static async Task<bool> WaitForChangeAsync(uint sequence, int timeoutMs)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            if (ClipboardAccess.Sequence != sequence)
                return true;
            await Task.Delay(15);
        }
        return ClipboardAccess.Sequence != sequence;
    }

    // Until the user lets go of the keys of the shortcut (at most 1.5 s).
    private static async Task WaitForModifiersAsync()
    {
        var watch = Stopwatch.StartNew();
        while (App.Matcher.AnyModifierDown && watch.ElapsedMilliseconds < 1500)
            await Task.Delay(15);
    }

    /// <summary>
    /// Where the popup opens, in screen pixels: under the text cursor of the app when Windows knows it (classic apps),
    /// else under the mouse pointer (browsers, the remote desktop).
    /// </summary>
    private static POINT Anchor(IntPtr target, bool remote)
    {
        if (!remote && target != IntPtr.Zero)
        {
            var info = new GUITHREADINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<GUITHREADINFO>() };
            int thread = GetWindowThreadProcessId(target, out _);
            if (GetGUIThreadInfo(thread, ref info) && info.hwndCaret != IntPtr.Zero && info.rcCaret.Height > 0)
            {
                var point = new POINT { X = info.rcCaret.Left, Y = info.rcCaret.Bottom };
                if (ClientToScreen(info.hwndCaret, ref point))
                    return point;
            }
        }
        GetCursorPos(out var cursor);
        cursor.Y += 16; // under the pointer, not on it
        return cursor;
    }
}
