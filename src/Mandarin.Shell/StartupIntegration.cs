using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Mandarin.Shell;

/// <summary>
/// Registers/unregisters Mandarin to launch at Windows sign-in, using the classic
/// per-user Run key (HKEY_CURRENT_USER\...\Run) — no admin rights, no scheduled task,
/// no startup folder shortcut to maintain.
/// </summary>
[SupportedOSPlatform("windows")]
public static class StartupIntegration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Mandarin";

    public static void Register(string exePath)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath)
            ?? throw new InvalidOperationException("Could not open the Windows startup registry key.");

        // Quoted, and started hidden into the tray rather than a visible panel flash.
        runKey.SetValue(ValueName, $"\"{exePath}\" --startup");
    }

    public static void Unregister()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        runKey?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static bool IsRegistered()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return runKey?.GetValue(ValueName) is not null;
    }
}
