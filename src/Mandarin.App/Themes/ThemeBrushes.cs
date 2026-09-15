using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;

namespace Mandarin.App.Themes;

/// <summary>
/// Code-side access to the design tokens in Themes/Theme.xaml, for the parts of
/// the UI that are built in code (the radial wheel's petals, the toast's state
/// colours). Keeps one palette instead of a second one hidden in C# literals.
/// </summary>
internal static class ThemeBrushes
{
    public static Brush Accent => Lookup("AccentBrush", 0xEB, 0x5E, 0x14);
    public static Brush AccentLight => Lookup("AccentLightBrush", 0xFF, 0xA8, 0x34);
    public static Brush AccentDark => Lookup("AccentDarkBrush", 0xC6, 0x4A, 0x0C);
    public static Brush Surface => Lookup("SurfaceBrush", 0xFF, 0xFF, 0xFF);
    public static Brush SurfaceSunken => Lookup("SurfaceSunkenBrush", 0xF5, 0xF3, 0xF1);
    public static Brush Border => Lookup("BorderBrushToken", 0xE7, 0xE2, 0xDC);
    public static Brush TextPrimary => Lookup("TextPrimaryBrush", 0x1C, 0x19, 0x17);
    public static Brush TextSecondary => Lookup("TextSecondaryBrush", 0x57, 0x53, 0x4E);
    public static Brush TextMuted => Lookup("TextMutedBrush", 0x8C, 0x83, 0x79);

    /// <summary>Dark veil over the acrylic backdrop, for the toast.</summary>
    public static Brush AcrylicDarkTint => Lookup("AcrylicDarkTintBrush", 0x21, 0x1D, 0x1A);

    public static Brush WheelPetal => Lookup("WheelPetalBrush", 0xFF, 0xFF, 0xFF);
    public static Brush WheelPetalStroke => Lookup("WheelPetalStrokeBrush", 0xE7, 0xE2, 0xDC);

    public static Color AccentColor => ColorOf(Accent, Color.FromRgb(0xEB, 0x5E, 0x14));

    /// <summary>Warm off-white the toast card uses so it reads as one surface with the rest of the app.</summary>
    public static Color ToastSurfaceColor => Color.FromArgb(0xF7, 0x26, 0x22, 0x1E);

    /// <summary>Failure accent, lightened so it stays legible on the dark toast.</summary>
    public static Color ToastErrorColor => Color.FromRgb(0xF0, 0x7B, 0x66);

    private static Brush Lookup(string key, byte r, byte g, byte b)
    {
        // Falls back to the literal when there is no Application (unit tests,
        // designer) or the key has been renamed, so the UI never comes up blank.
        if (Application.Current?.TryFindResource(key) is Brush brush)
        {
            return brush;
        }

        var fallback = new SolidColorBrush(Color.FromRgb(r, g, b));
        fallback.Freeze();
        return fallback;
    }

    private static Color ColorOf(Brush brush, Color fallback) =>
        brush is SolidColorBrush solid ? solid.Color : fallback;
}
