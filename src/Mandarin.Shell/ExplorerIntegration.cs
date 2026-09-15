using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Mandarin.Shell;

/// <summary>
/// Registers/unregisters a "Convert with Mandarin" entry on the Explorer right-click menu
/// for every file type, using a classic per-user registry verb
/// (HKEY_CURRENT_USER\Software\Classes\*\shell\...). This is the traditional, unpackaged
/// way Win32 apps have added shell context-menu entries since Windows Vista — no admin
/// rights, no installer, no COM DLL registration.
///
/// This is a single static verb, not a dynamic submenu of format choices: Explorer runs
/// `Mandarin.App.exe "<path>"`, and the app itself shows the usual format-picker popup for
/// that file. A full per-format submenu would need either an MSIX sparse package (declined
/// for this project — see CLAUDE.md's Packaging section) or a NativeAOT COM
/// IExplorerCommand handler, both a materially larger undertaking than this.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ExplorerIntegration
{
    private const string VerbName = "Mandarin.ConvertWithMandarin";
    private const string VerbKeyPath = @"Software\Classes\*\shell\" + VerbName;
    private const string MenuLabel = "Convert with Mandarin";

    public static void Register(string exePath)
    {
        using var verbKey = Registry.CurrentUser.CreateSubKey(VerbKeyPath)
            ?? throw new InvalidOperationException("Could not create the Explorer context-menu registry key.");

        verbKey.SetValue(string.Empty, MenuLabel);
        verbKey.SetValue("Icon", $"\"{exePath}\",0");

        using var commandKey = verbKey.CreateSubKey("command")
            ?? throw new InvalidOperationException("Could not create the Explorer context-menu command key.");
        commandKey.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
    }

    public static void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree(VerbKeyPath, throwOnMissingSubKey: false);
    }

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(VerbKeyPath);
        return key is not null;
    }
}
