using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Mandarin.App.Themes;

/// <summary>
/// Applies the Windows 11 acrylic system backdrop (frosted glass) plus rounded
/// corners to a window.
///
/// The backdrop is drawn by DWM behind the window's client area, so it only
/// shows through where the WPF content is transparent. That requires a window
/// with <c>AllowsTransparency="False"</c> and a <c>WindowChrome</c> whose
/// <c>GlassFrameThickness</c> is -1; a layered (per-pixel alpha) window cannot
/// host a system backdrop at all.
///
/// Everything here degrades quietly: on Windows 10, or if DWM refuses, the
/// caller is told so it can fall back to an opaque surface.
/// </summary>
internal static class WindowBackdrop
{
    // Documented DWM window attributes (dwmapi.h).
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;

    private const int DwmwcpRound = 2;

    /// <summary>Acrylic — the blur-behind backdrop meant for transient surfaces like dialogs.</summary>
    private const int DwmsbtTransientWindow = 3;

    /// <summary>Win11 22H2, the first build with DWMWA_SYSTEMBACKDROP_TYPE.</summary>
    private const int MinBackdropBuild = 22621;

    /// <summary>Win11 21H2, the first build with rounded-corner preference.</summary>
    private const int MinCornerBuild = 22000;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Requests acrylic + rounded corners for <paramref name="window"/>. Safe to
    /// call before the window has a handle: it defers to SourceInitialized and
    /// reports the result through <paramref name="onResult"/>.
    /// </summary>
    public static void Apply(Window window, Action<bool> onResult)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            onResult(TryApply(handle));
            return;
        }

        window.SourceInitialized += (_, _) =>
            onResult(TryApply(new WindowInteropHelper(window).Handle));
    }

    private static bool TryApply(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var build = Environment.OSVersion.Version.Build;

        // Tells DWM to draw the frame (border, and the title bar we don't use) for
        // a dark window, so the hairline around a dark card isn't a light one.
        var dark = ThemeManager.IsDark ? 1 : 0;
        TrySet(handle, DwmwaUseImmersiveDarkMode, ref dark);

        if (build >= MinCornerBuild)
        {
            var corner = DwmwcpRound;
            TrySet(handle, DwmwaWindowCornerPreference, ref corner);
        }

        if (build < MinBackdropBuild)
        {
            return false;
        }

        var backdrop = DwmsbtTransientWindow;
        return TrySet(handle, DwmwaSystemBackdropType, ref backdrop);
    }

    private static bool TrySet(IntPtr handle, int attribute, ref int value)
    {
        try
        {
            return DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int)) == 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }
}
