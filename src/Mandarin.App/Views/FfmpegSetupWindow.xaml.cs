using System.IO;
using System.Windows;
using Mandarin.App.Resources;
using Mandarin.Core.Conversion.VideoAudio;
using Mandarin.Core.Settings;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Mandarin.App.Views;

/// <summary>
/// Shown when a video or audio operation is requested and ffmpeg.exe can't be
/// found. Lets the user point at their own copy, which is then remembered in
/// settings — Mandarin never downloads it (no network calls, per CLAUDE.md), so
/// this dialog is the whole recovery path.
/// </summary>
public partial class FfmpegSetupWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly MandarinSettings _settings;

    public FfmpegSetupWindow(ISettingsService settingsService, MandarinSettings settings)
    {
        InitializeComponent();
        DialogChrome.Apply(this, HeaderBar, CardRoot);

        _settingsService = settingsService;
        _settings = settings;

        SearchedPathsText.Text = string.Join(
            Environment.NewLine,
            Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
            Strings.FfmpegSearchedPathEnv);

        var current = _settings.FfmpegPath;
        StatusText.Text = string.IsNullOrWhiteSpace(current)
            ? Strings.FfmpegNotConfigured
            : Strings.FfmpegCurrentPath(current);
    }

    private void LocateButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.FfmpegLocateButton,
            Filter = "ffmpeg.exe|ffmpeg.exe|*.exe|*.exe",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        // Verify by running it, so picking the wrong exe fails here rather than
        // in the middle of the user's first conversion.
        if (!FfmpegLocator.IsWorkingFfmpeg(dialog.FileName))
        {
            ErrorText.Text = Strings.FfmpegNotValid;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        _settings.FfmpegPath = dialog.FileName;
        _settingsService.Save(_settings);
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogChrome.Dismiss(this);
}
