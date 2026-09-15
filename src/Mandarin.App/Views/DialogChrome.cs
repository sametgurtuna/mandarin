using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Mandarin.App.Themes;
using Brush = System.Windows.Media.Brush;

namespace Mandarin.App.Views;

/// <summary>
/// Shared behaviour for Mandarin's dialog cards: the Windows 11 acrylic
/// backdrop, drag by the header strip, and Esc to dismiss.
///
/// Dialogs draw their own frame instead of using the OS title bar, so these
/// affordances have to be wired up by hand.
/// </summary>
internal static class DialogChrome
{
    /// <summary>
    /// Makes <paramref name="header"/> drag the window, Esc close it, and — when
    /// <paramref name="card"/> is given — the window frosted.
    /// </summary>
    /// <param name="card">
    /// The dialog's root <see cref="Border"/>. Its XAML background is the tint
    /// that sits over the acrylic; if the backdrop is unavailable (Windows 10, or
    /// DWM refuses) it is swapped for an opaque surface, because the glass frame
    /// would otherwise render as black.
    /// </param>
    public static void Apply(Window window, UIElement? header = null, Border? card = null)
    {
        if (card is not null)
        {
            WindowBackdrop.Apply(window, acrylic =>
            {
                if (!acrylic)
                {
                    card.Background = Opaque(card.Background);
                }
            });
        }

        if (header is not null)
        {
            header.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                {
                    // DragMove throws if the button was already released by the
                    // time it runs; a failed drag must never take the app down.
                    try
                    {
                        window.DragMove();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            };
        }

        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Dismiss(window);
            }
        };
    }

    /// <summary>
    /// Closes a dialog as a cancellation. Modal dialogs need DialogResult set;
    /// modeless ones only accept Close().
    /// </summary>
    public static void Dismiss(Window window)
    {
        try
        {
            window.DialogResult = false;
        }
        catch (InvalidOperationException)
        {
            window.Close();
        }
    }

    /// <summary>
    /// Drops the alpha from a tint brush, keeping its hue, so the fallback
    /// surface still looks like the same colour the design asked for.
    /// </summary>
    private static Brush Opaque(Brush? tint)
    {
        if (tint is SolidColorBrush solid)
        {
            var c = solid.Color;
            var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(c.R, c.G, c.B));
            brush.Freeze();
            return brush;
        }

        return ThemeBrushes.Surface;
    }
}
