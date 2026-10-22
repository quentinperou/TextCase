using System.Runtime.InteropServices;
using System.Text;

namespace TextCase.Services;

/// <summary>The Windows functions the app calls directly (notification icon, keyboard hook, input, clipboard, windows).</summary>
internal static class NativeMethods
{
    // ---------------------------------------------------------------- Messages

    public const int WM_CLOSE = 0x0010;
    public const int WM_QUERYENDSESSION = 0x0011;
    public const int WM_ENDSESSION = 0x0016;
    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int WM_CONTEXTMENU = 0x007B;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_XBUTTONDOWN = 0x020B;
    public const int WM_APP = 0x8000;
    public const int NIN_SELECT = 0x0400;
    public const int NIN_KEYSELECT = 0x0401;
    public const int MA_NOACTIVATE = 3;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegisterWindowMessage(string message);

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public int message;
        public IntPtr wParam;
        public IntPtr lParam;
        public int time;
        public POINT pt;
    }

    public const int PM_NOREMOVE = 0;

    [DllImport("user32.dll")]
    public static extern int GetMessage(out MSG msg, IntPtr hwnd, int filterMin, int filterMax);

    [DllImport("user32.dll")]
    public static extern bool PeekMessage(out MSG msg, IntPtr hwnd, int filterMin, int filterMax, int remove);

    [DllImport("user32.dll")]
    public static extern bool PostThreadMessage(int threadId, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern int GetCurrentThreadId();

    // ---------------------------------------------------------------- Notification icon

    public const int NIM_ADD = 0;
    public const int NIM_MODIFY = 1;
    public const int NIM_DELETE = 2;
    public const int NIM_SETVERSION = 4;
    public const int NIF_MESSAGE = 0x01;
    public const int NIF_ICON = 0x02;
    public const int NIF_TIP = 0x04;
    public const int NIF_SHOWTIP = 0x80;
    public const int NOTIFYICON_VERSION_4 = 4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int ExtractIconEx(string file, int index, IntPtr[]? large, IntPtr[]? small, int count);

    [DllImport("comctl32.dll", CharSet = CharSet.Unicode)]
    public static extern int LoadIconWithScaleDown(IntPtr instance, IntPtr name, int width, int height, out IntPtr icon);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? name);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr icon);

    public const int SM_CXSMICON = 49;

    [DllImport("user32.dll")]
    public static extern int GetSystemMetricsForDpi(int index, int dpi);

    [DllImport("user32.dll")]
    public static extern int GetDpiForSystem();

    // ---------------------------------------------------------------- Keyboard and mouse hooks

    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;
    public const int LLKHF_INJECTED = 0x10;

    public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public int mouseData;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, int threadId);

    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    // ---------------------------------------------------------------- Input

    public const int INPUT_KEYBOARD = 1;
    public const int KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const int KEYEVENTF_KEYUP = 0x0002;
    public const int MAPVK_VK_TO_VSC = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public int type;
        public InputUnion u;
    }

    // The mouse member gives the union its full size (40 bytes for an INPUT on x64).
    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public int dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public int dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SendInput(int count, INPUT[] inputs, int size);

    [DllImport("user32.dll")]
    public static extern int MapVirtualKey(int code, int mapType);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vk);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetKeyNameText(int lParam, StringBuilder name, int size);

    // ---------------------------------------------------------------- Clipboard

    public const int CF_UNICODETEXT = 13;
    public const int CF_DIB = 8;
    public const int CF_HDROP = 15;
    public const int CF_LOCALE = 16;
    public const int CF_DIBV5 = 17;
    public const int GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll")]
    public static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    public static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    public static extern IntPtr GetClipboardData(int format);

    [DllImport("user32.dll")]
    public static extern IntPtr SetClipboardData(int format, IntPtr data);

    [DllImport("user32.dll")]
    public static extern bool IsClipboardFormatAvailable(int format);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegisterClipboardFormat(string name);

    [DllImport("user32.dll")]
    public static extern uint GetClipboardSequenceNumber();

    public const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll")]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GlobalAlloc(int flags, UIntPtr bytes);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll")]
    public static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll")]
    public static extern UIntPtr GlobalSize(IntPtr memory);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GlobalFree(IntPtr memory);

    // ---------------------------------------------------------------- Windows

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_POPUP = unchecked((int)0x80000000);

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const int SWP_NOSIZE = 0x0001;
    public const int SWP_NOZORDER = 0x0004;
    public const int SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;

        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, int flags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);

    [DllImport("user32.dll")]
    public static extern int GetWindowThreadProcessId(IntPtr hwnd, out int processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hwnd, StringBuilder name, int size);

    [StructLayout(LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [DllImport("user32.dll")]
    public static extern bool GetGUIThreadInfo(int threadId, ref GUITHREADINFO info);

    public const int MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT point, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    // ---------------------------------------------------------------- Window events (the window in front)

    public const int EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const int WINEVENT_OUTOFCONTEXT = 0x0000;

    public delegate void WinEventProc(IntPtr hook, int eventType, IntPtr hwnd, int idObject, int idChild, int thread, int time);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(int eventMin, int eventMax, IntPtr module, WinEventProc callback, int processId, int threadId, int flags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hook);

    // ---------------------------------------------------------------- Memory

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    public static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimum, IntPtr maximum);
}
