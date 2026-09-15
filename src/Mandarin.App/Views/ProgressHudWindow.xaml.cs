using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Mandarin.App.Services;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Point = System.Windows.Point;
using Mandarin.App.Themes;

namespace Mandarin.App.Views;

public partial class ProgressHudWindow : Window
{
    private CancellationTokenSource? _cts;
    private string? _outputFolder;
    private string? _outputFilePath;
    private bool _isCompleted;

    public ProgressHudWindow()
    {
        InitializeComponent();

        // Frosted like the dialogs; falls back to a solid card if DWM says no.
        WindowBackdrop.Apply(this, acrylic =>
        {
            if (!acrylic)
            {
                HudCard.Background = ThemeBrushes.Surface;
            }
        });
    }

    public void ShowOperation(
        string title,
        string subtitle,
        Point? nearPoint,
        CancellationTokenSource? cts)
    {
        _cts = cts;
        _outputFolder = null;
        _outputFilePath = null;
        _isCompleted = false;

        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        CancelButton.Visibility = Visibility.Visible;
        ProgressBarFill.Width = 0;

        // Position window: either near cursor/wheel position or bottom-right of primary screen
        double targetLeft;
        double targetTop;

        if (nearPoint.HasValue)
        {
            targetLeft = nearPoint.Value.X - Width / 2.0;
            targetTop = nearPoint.Value.Y - Height / 2.0;
        }
        else
        {
            var workArea = SystemParameters.WorkArea;
            targetLeft = workArea.Right - Width - 24;
            targetTop = workArea.Bottom - Height - 24;
        }

        // Clamp inside virtual screen
        targetLeft = Math.Max(SystemParameters.VirtualScreenLeft + 10,
            Math.Min(SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width - 10, targetLeft));
        targetTop = Math.Max(SystemParameters.VirtualScreenTop + 10,
            Math.Min(SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height - 10, targetTop));

        Left = targetLeft;
        Top = targetTop;

        Show();

        var sb = (Storyboard)Resources["FadeInStoryboard"];
        sb.Begin(this);
    }

    public void UpdateProgress(double fraction)
    {
        Dispatcher.Invoke(() =>
        {
            fraction = Math.Clamp(fraction, 0.0, 1.0);
            double totalWidth = ProgressBarContainer.ActualWidth;
            if (totalWidth <= 0) totalWidth = 300;

            ProgressBarFill.Width = fraction * totalWidth;
        });
    }

    public void SetCompleted(string title, string? subtitle, string? folderToOpen, string? filePath = null)
    {
        Dispatcher.Invoke(async () =>
        {
            _isCompleted = true;
            _outputFolder = folderToOpen;
            _outputFilePath = filePath;

            TitleText.Text = "✓ " + title;
            SubtitleText.Text = string.IsNullOrEmpty(subtitle) ? "Click to view in Explorer" : subtitle;
            CancelButton.Visibility = Visibility.Collapsed;

            double totalWidth = ProgressBarContainer.ActualWidth;
            if (totalWidth <= 0) totalWidth = 300;
            ProgressBarFill.Width = totalWidth;

            SoundEffectService.PlaySuccess();

            // Auto close after 2.8 seconds unless user clicked
            await Task.Delay(2800);
            Dismiss();
        });
    }

    public void SetFailed(string title, string error)
    {
        Dispatcher.Invoke(async () =>
        {
            _isCompleted = false;
            TitleText.Text = "✕ " + title;
            SubtitleText.Text = error;
            CancelButton.Visibility = Visibility.Collapsed;

            await Task.Delay(3500);
            Dismiss();
        });
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            TitleText.Text = "Cancelled";
            SubtitleText.Text = "Operation was cancelled";
            DismissDelayed(600);
        }
    }

    private void HudCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isCompleted)
        {
            if (!string.IsNullOrEmpty(_outputFilePath) && File.Exists(_outputFilePath))
            {
                Process.Start("explorer.exe", $"/select,\"{_outputFilePath}\"");
            }
            else if (!string.IsNullOrEmpty(_outputFolder) && Directory.Exists(_outputFolder))
            {
                Process.Start("explorer.exe", _outputFolder);
            }

            Dismiss();
        }
    }

    private void DismissDelayed(int delayMs)
    {
        Task.Delay(delayMs).ContinueWith(_ => Dispatcher.Invoke(Dismiss));
    }

    public void Dismiss()
    {
        try
        {
            var sb = (Storyboard)Resources["FadeOutStoryboard"];
            sb.Completed += (_, _) => Hide();
            sb.Begin(this);
        }
        catch
        {
            Hide();
        }
    }
}
