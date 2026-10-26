using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
#if STORE
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
#endif

namespace TextCase.Services;

/// <summary>
/// The Microsoft Store version: the same exe, run from its MSIX package (store\AppxManifest.xml, built by
/// scripts\build-store.ps1 with <c>STORE</c> defined). In the package the start with Windows is the startup task of the
/// manifest, the Store updates the app, File Explorer has no menu of TextCase, and the version installed from GitHub,
/// if it is on the PC too, is offered for removal. The GitHub build has no Windows SDK projection (some 25 MB): there
/// the startup task is never asked for, the exe never runs from a package.
/// </summary>
internal static class AppPackage
{
    private const int AppModelErrorNoPackage = 15700;

    // The version installed from GitHub: Inno Setup's uninstall entry (AppId of installer\TextCase.iss + "_is1").
    private const string ClassicUninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{4D4C29C7-FEEA-4F84-8A74-1DB539E852D1}_is1";

    /// <summary>True when this process runs with a package identity: the Store version.</summary>
    public static bool IsPackaged { get; } = ReadIsPackaged();

    /// <summary>The page of the app in the Microsoft Store (it shows "Update" when there is one).</summary>
    public static string StorePage => $"ms-windows-store://pdp/?PFN={ReadFamilyName()}";

#if STORE
    /// <summary>The startup task declared in store\AppxManifest.xml.</summary>
    private const string StartupTaskId = "TextCaseStartup";

    /// <summary>Started by its startup task when the user signed in: nothing to open, as with --background.</summary>
    public static bool LaunchedAtStartup()
    {
        if (!IsPackaged)
            return false;
        try
        {
            return AppInstance.GetActivatedEventArgs()?.Kind == ActivationKind.StartupTask;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>The state of the startup task.</summary>
    public static async Task<StartupState> GetStartupAsync()
    {
        try
        {
            return Map((await StartupTask.GetAsync(StartupTaskId)).State);
        }
        catch (Exception ex) when (ex is COMException or ArgumentException)
        {
            return StartupState.Off;
        }
    }

    /// <summary>
    /// Turns the startup task on or off. Windows asks nothing for a desktop app, but once the user turned it off in
    /// Task Manager or Settings the app cannot turn it on again (<see cref="StartupState.OffByUser"/>).
    /// </summary>
    public static async Task<StartupState> SetStartupAsync(bool on)
    {
        try
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            if (on)
                return Map(await task.RequestEnableAsync());
            task.Disable();
            return Map(task.State);
        }
        catch (Exception ex) when (ex is COMException or ArgumentException)
        {
            return StartupState.Off;
        }
    }

    private static StartupState Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled => StartupState.On,
        StartupTaskState.DisabledByUser => StartupState.OffByUser,
        StartupTaskState.EnabledByPolicy or StartupTaskState.DisabledByPolicy => StartupState.ByPolicy,
        _ => StartupState.Off,
    };
#else
    public static bool LaunchedAtStartup() => false;

    public static Task<StartupState> GetStartupAsync() => Task.FromResult(StartupState.Off);

    public static Task<StartupState> SetStartupAsync(bool on) => Task.FromResult(StartupState.Off);
#endif

    /// <summary>The uninstaller of the version installed from GitHub, when it is on this PC.</summary>
    public static string? ClassicUninstaller()
    {
        try
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = machine.OpenSubKey(ClassicUninstallKey);
            return key?.GetValue("UninstallString") is string command && command.Trim('"') is var exe && File.Exists(exe) ? exe : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Starts the uninstaller of the version installed from GitHub (its own questions, its administrator prompt). It
    /// closes every TextCase, this one too.
    /// </summary>
    public static void StartClassicUninstall(string uninstaller)
    {
        try
        {
            // Through File Explorer: started by this process, it would run inside the package, its changes to the
            // registry kept apart from the real one.
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{uninstaller}\"") { UseShellExecute = false });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Debug.WriteLine($"Uninstall of the GitHub version: {ex.Message}");
        }
    }

    private static bool ReadIsPackaged()
    {
        int length = 0;
        return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
    }

    private static string ReadFamilyName()
    {
        int length = 0;
        GetCurrentPackageFamilyName(ref length, null);
        var name = new char[length];
        return GetCurrentPackageFamilyName(ref length, name) == 0 ? new string(name, 0, Math.Max(0, length - 1)) : "";
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(ref int packageFamilyNameLength, char[]? packageFamilyName);
}

/// <summary>Whether TextCase starts with Windows.</summary>
internal enum StartupState
{
    Off,
    On,
    /// <summary>Turned off by the user in Task Manager or Settings: only there can it be turned on again.</summary>
    OffByUser,
    /// <summary>Set by the administrator of the PC.</summary>
    ByPolicy,
}
