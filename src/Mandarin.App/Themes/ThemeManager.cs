using System.Windows;
using Microsoft.Win32;
using Application = System.Windows.Application;

namespace Mandarin.App.Themes;

/// <summary>
/// Light/dark switching. The app's colour tokens live in two interchangeable
/// dictionaries (Themes/Tokens.Light.xaml and Tokens.Dark.xaml) merged at index
/// 0 of the application resources; switching theme swaps that one dictionary, so
/// every surface that references a colour with DynamicResource updates live.
/// </summary>
internal static class ThemeManager
{
    /// <summary>Settings values, stored as-is in settings.json.</summary>
    public const string ModeSystem = "system";
    public const string ModeLight = "light";
    public const string ModeDark = "dark";

    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static string _mode = ModeSystem;
    private static bool _subscribed;

    /// <summary>True when dark tokens are currently loaded.</summary>
    public static bool IsDark { get; private set; }

    /// <summary>Raised after the token dictionary has been swapped.</summary>
    public static event Action? ThemeChanged;

    /// <summary>
    /// Applies a settings value ("system", "light" or "dark"). Following the
    /// system means reacting to the user changing it in Windows later, so the
    /// first "system" call also subscribes to that notification.
    /// </summary>
    public static void Apply(string? mode)
    {
        _mode = Normalize(mode);

        if (_mode == ModeSystem && !_subscribed)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _subscribed = true;
        }

        Load(_mode == ModeSystem ? SystemPrefersDark() : _mode == ModeDark);
    }

    private static string Normalize(string? mode) => mode switch
    {
        ModeLight => ModeLight,
        ModeDark => ModeDark,
        _ => ModeSystem,
    };

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General || _mode != ModeSystem)
        {
            return;
        }

        // The notification arrives on a system thread.
        Application.Current?.Dispatcher.Invoke(() => Load(SystemPrefersDark()));
    }

    /// <summary>Reads the Windows "app mode" setting; defaults to light if absent.</summary>
    public static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Load(bool dark)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var merged = app.Resources.MergedDictionaries;
        if (merged.Count == 0)
        {
            return;
        }

        var source = new Uri(
            dark ? "Themes/Tokens.Dark.xaml" : "Themes/Tokens.Light.xaml",
            UriKind.Relative);

        // Replacing index 0 in place keeps the styles dictionary (index 1) and
        // its DynamicResource lookups pointed at the new colours.
        merged[0] = new ResourceDictionary { Source = source };
        IsDark = dark;
        ThemeChanged?.Invoke();
    }
}
