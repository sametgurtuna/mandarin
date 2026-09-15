using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Mandarin.App.Themes;
using System.Windows.Threading;
using Mandarin.App.Resources;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace Mandarin.App.Views;

/// <summary>
/// A small auto-dismissing notification shown after a conversion completes or fails.
/// Hovering pauses the auto-dismiss timer.
/// </summary>
public partial class ToastWindow : Window
{
    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(4.5);

    private readonly DispatcherTimer _dismissTimer;
    private readonly string? _folderToOpen;

    public ToastWindow(string title, string message, bool isError, string? folderToOpen)
    {
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;
        _folderToOpen = folderToOpen;

        var accent = isError ? ThemeBrushes.ToastErrorColor : ThemeBrushes.AccentColor;
        RootBorder.Background = ThemeBrushes.AcrylicDarkTint;

        // The toast is frosted like the dialogs; if the backdrop isn't available
        // it falls back to a solid dark card so the white text stays readable.
        WindowBackdrop.Apply(this, acrylic =>
        {
            if (!acrylic)
            {
                RootBorder.Background = new SolidColorBrush(ThemeBrushes.ToastSurfaceColor);
            }
        });
        RootBorder.BorderBrush = new SolidColorBrush(accent);
        TitleText.Foreground = new SolidColorBrush(accent);
        MessageText.Foreground = Brushes.White;

        // "!" for a failure, a tick for success: the shape carries the state even
        // when the colour doesn't.
        StatusBadge.Background = new SolidColorBrush(accent);
        StatusGlyph.Text = isError ? "!" : "✓";

        if (folderToOpen is not null)
        {
            OpenFolderButton.Content = Strings.ToastOpenFolderButton;
            OpenFolderButton.Visibility = Visibility.Visible;
        }

        _dismissTimer = new DispatcherTimer { Interval = DisplayDuration };
        _dismissTimer.Tick += (_, _) => Close();
        _dismissTimer.Start();

        PositionBottomRight();

        Loaded += (_, _) =>
        {
            if (TryFindResource("ToastEnterStoryboard") is Storyboard enter)
            {
                enter.Begin(this);
            }
        };
    }

    private void PositionBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Loaded += (_, _) =>
        {
            // DWM draws the shadow now, so the window rect is the visible card.
            const double Gap = 12;
            Left = workArea.Right - ActualWidth - Gap;
            Top = workArea.Bottom - ActualHeight - Gap;
        };
    }

    private void ToastWindow_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) =>
        _dismissTimer.Stop();

    private void ToastWindow_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) =>
        _dismissTimer.Start();

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_folderToOpen is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_folderToOpen}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Best-effort convenience action; failing to open Explorer shouldn't crash the app.
        }

        Close();
    }
}
