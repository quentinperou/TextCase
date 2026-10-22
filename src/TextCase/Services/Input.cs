using System.Runtime.InteropServices;
using static TextCase.Services.NativeMethods;

namespace TextCase.Services;

/// <summary>
/// The keys TextCase sends to the app in front: Ctrl+C to copy the selection, Ctrl+V to paste the result. Each key carries
/// its scan code too: the remote desktop client sends scan codes to the remote PC, and a key without one does nothing
/// there.
/// </summary>
internal static class Input
{
    private const int VkControl = 0x11;
    private const int VkMask = 0xE8; // unassigned

    /// <summary>Ctrl+C to the app in front; false when Windows refused it (an app run as administrator).</summary>
    public static bool Copy() => Chord(VkControl, 'C');

    /// <summary>Ctrl+V to the app in front; false when Windows refused it.</summary>
    public static bool Paste() => Chord(VkControl, 'V');

    /// <summary>An unassigned key, pressed and released (see <see cref="KeyboardHook.MaskModifiers"/>).</summary>
    public static void SendMask() => Send([Key(VkMask, up: false, scan: false), Key(VkMask, up: true, scan: false)]);

    private static bool Chord(int modifier, int key) => Send(
    [
        Key(modifier, up: false),
        Key(key, up: false),
        Key(key, up: true),
        Key(modifier, up: true),
    ]);

    private static bool Send(INPUT[] inputs) => SendInput(inputs.Length, inputs, Marshal.SizeOf<INPUT>()) == inputs.Length;

    private static INPUT Key(int vk, bool up, bool scan = true) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = (ushort)vk,
                wScan = scan ? (ushort)MapVirtualKey(vk, MAPVK_VK_TO_VSC) : (ushort)0,
                dwFlags = up ? KEYEVENTF_KEYUP : 0,
                dwExtraInfo = KeyboardHook.OwnInput,
            },
        },
    };
}
