using System.Runtime.InteropServices;
using TextCase.Core;
using static TextCase.Services.NativeMethods;

namespace TextCase.Services;

/// <summary>
/// The low-level keyboard hook (and, while the popup is open, the mouse hook), on a thread of its own with its own
/// message loop: the hooks are called for every key of the session and must answer at once, whatever the UI thread is
/// doing.
/// <para>
/// A hook rather than RegisterHotKey: the remote desktop client (mstsc) installs a hook of its own when its window is
/// active and takes the keys for the remote PC before any shortcut of this PC is seen. The last hook installed is called
/// first, so this one is installed again each time a remote desktop window comes to the front (<see cref="Reinstall"/>).
/// The hook also lets TextCase see Ctrl+C twice without taking it, and read the keys of the popup without taking the
/// focus from the app where the text is selected.
/// </para>
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    /// <summary>The extra information of the keys TextCase sends (SendInput), to recognise them in the hook.</summary>
    public static readonly IntPtr OwnInput = new(0x54584341); // "TXCA"

    private const int ReinstallMessage = WM_APP + 1;
    private const int WatchMouseMessage = WM_APP + 2;
    private const int MaskMessage = WM_APP + 3;
    private const int QuitMessage = 0x0012; // WM_QUIT

    private readonly Func<KeyEvent, bool> _onKey;
    private readonly Action<POINT> _onMouseDown;
    // Kept in fields: the hooks call them for as long as they are installed.
    private readonly HookProc _keyboardProc;
    private readonly HookProc _mouseProc;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private int _threadId;
    private IntPtr _keyboard;
    private IntPtr _mouse;

    /// <param name="onKey">Called on the hook thread for each key; true takes the key (the app never sees it).</param>
    /// <param name="onMouseDown">Called on the hook thread for each mouse button pressed while the mouse is watched.</param>
    public KeyboardHook(Func<KeyEvent, bool> onKey, Action<POINT> onMouseDown)
    {
        _onKey = onKey;
        _onMouseDown = onMouseDown;
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
        _thread = new Thread(Run) { IsBackground = true, Name = "TextCase keyboard hook", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
        _ready.Wait();
    }

    /// <summary>Puts the hook first again (after another app installed its own, the remote desktop client).</summary>
    public void Reinstall() => PostThreadMessage(_threadId, ReinstallMessage, IntPtr.Zero, IntPtr.Zero);

    /// <summary>Watches the mouse buttons (the popup closes on a click elsewhere), or stops watching them.</summary>
    public void WatchMouse(bool watch) => PostThreadMessage(_threadId, WatchMouseMessage, new IntPtr(watch ? 1 : 0), IntPtr.Zero);

    /// <summary>
    /// Sends an unassigned key just after a shortcut with Alt or Win was taken: Windows then does not take the release
    /// of Alt alone for the menu bar, nor of Win alone for Start.
    /// </summary>
    public void MaskModifiers() => PostThreadMessage(_threadId, MaskMessage, IntPtr.Zero, IntPtr.Zero);

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE); // creates the message queue of the thread
        InstallKeyboard();
        _ready.Set();
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            switch (msg.message)
            {
                case ReinstallMessage:
                    InstallKeyboard();
                    break;
                case WatchMouseMessage when msg.wParam != IntPtr.Zero:
                    if (_mouse == IntPtr.Zero)
                        _mouse = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(null), 0);
                    break;
                case WatchMouseMessage:
                    if (_mouse != IntPtr.Zero)
                        UnhookWindowsHookEx(_mouse);
                    _mouse = IntPtr.Zero;
                    break;
                case MaskMessage:
                    Input.SendMask();
                    break;
            }
        }
        if (_mouse != IntPtr.Zero)
            UnhookWindowsHookEx(_mouse);
        if (_keyboard != IntPtr.Zero)
            UnhookWindowsHookEx(_keyboard);
    }

    // The new hook first, then the old one removed: no key goes unseen in between.
    private void InstallKeyboard()
    {
        var hook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero)
            return;
        if (_keyboard != IntPtr.Zero)
            UnhookWindowsHookEx(_keyboard);
        _keyboard = hook;
    }

    private IntPtr KeyboardProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int message = wParam.ToInt32();
            bool down = message is WM_KEYDOWN or WM_SYSKEYDOWN;
            bool own = data.dwExtraInfo == OwnInput;
            try
            {
                if (_onKey(new KeyEvent(data.vkCode & 0xFF, down, own)))
                    return new IntPtr(1);
            }
            catch (Exception ex)
            {
                // A key is never lost to a bug of the app.
                System.Diagnostics.Debug.WriteLine($"Keyboard hook: {ex}");
            }
        }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    private IntPtr MouseProc(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt32() is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            _onMouseDown(data.pt);
        }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }

    public void Dispose()
    {
        PostThreadMessage(_threadId, QuitMessage, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
        _ready.Dispose();
    }
}
