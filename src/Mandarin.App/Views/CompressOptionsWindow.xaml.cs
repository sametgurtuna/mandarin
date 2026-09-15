using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Mandarin.App.Resources;
using Mandarin.Core.AdvancedTools;
using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.VideoAudio;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace Mandarin.App.Views;

/// <summary>
/// Compression settings. The dialog shows only what the dropped file's format
/// can actually act on: pixel size and lossless for images, resolution for
/// video, audio bitrate for anything with sound, and nothing extra for PDFs.
/// </summary>
public partial class CompressOptionsWindow : Window
{
    private enum MediaKind
    {
        Image,
        Video,
        Audio,
        Document,
    }

    private static readonly HashSet<string> LosslessCapable = new(StringComparer.OrdinalIgnoreCase)
    {
        "webp", "png", "tiff", "tif",
    };

    private readonly MediaKind _kind;
    private readonly long _sourceSizeBytes;

    public CompressOptions? Result { get; private set; }

    public CompressOptionsWindow(string? sourcePath = null)
    {
        InitializeComponent();
        DialogChrome.Apply(this, HeaderBar, CardRoot);

        Title = Strings.CompressTitle;
        HeadingText.Text = Strings.CompressTitle;
        TaglineText.Text = Strings.CompressTagline;
        SmallerFileText.Text = Strings.CompressSmallerFile;
        HigherQualityText.Text = Strings.CompressHigherQuality;
        CancelButton.Content = Strings.CommonCancelButton;
        CompressButton.Content = Strings.CompressCompressButton;

        var extension = sourcePath is null ? string.Empty : FileExtensions.GetNormalizedExtension(sourcePath);
        _kind = KindOf(extension);
        _sourceSizeBytes = SizeOf(sourcePath);

        ConfigureForKind(extension);
        OutputPreview.Show(OutputPreviewText, sourcePath, AdvancedToolKind.Compress);

        if (_sourceSizeBytes > 0)
        {
            CurrentSizeText.Text = Strings.CompressCurrentSize(FormatMegabytes(_sourceSizeBytes));

            // Default the target to roughly half the current size: obviously an
            // ask rather than a guess at the answer.
            var suggestion = Math.Max(0.1, _sourceSizeBytes / 2.0 / (1024 * 1024));
            TargetSizeTextBox.Text = suggestion.ToString(suggestion < 10 ? "0.0" : "0", CultureInfo.CurrentCulture);
        }

        UpdateLabel();
    }

    private static MediaKind KindOf(string extension)
    {
        if (MediaExtensions.VideoExtensions.Contains(extension))
        {
            return MediaKind.Video;
        }

        if (MediaExtensions.AudioExtensions.Contains(extension))
        {
            return MediaKind.Audio;
        }

        return extension == "pdf" ? MediaKind.Document : MediaKind.Image;
    }

    private static long SizeOf(string? path)
    {
        try
        {
            return path is not null && File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private void ConfigureForKind(string extension)
    {
        switch (_kind)
        {
            case MediaKind.Image:
                ImageOptionsPanel.Visibility = Visibility.Visible;
                LosslessCheckBox.Visibility = LosslessCapable.Contains(extension)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                break;

            case MediaKind.Video:
                VideoOptionsPanel.Visibility = Visibility.Visible;
                AudioOptionsPanel.Visibility = Visibility.Visible;
                QualitySlider.Value = 50;
                break;

            case MediaKind.Audio:
                AudioOptionsPanel.Visibility = Visibility.Visible;
                QualitySlider.Value = 50;
                break;

            case MediaKind.Document:
                // A PDF's size comes from re-rendering its pages, which makes a
                // target-size search too slow to offer honestly.
                TargetModeRadio.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private static string FormatMegabytes(long bytes) =>
        (bytes / 1024.0 / 1024.0).ToString("0.0", CultureInfo.CurrentCulture);

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (QualityPanel is null || TargetPanel is null)
        {
            return;
        }

        var quality = QualityModeRadio.IsChecked == true;
        QualityPanel.Visibility = quality ? Visibility.Visible : Visibility.Collapsed;
        TargetPanel.Visibility = quality ? Visibility.Collapsed : Visibility.Visible;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateLabel();

    private void TargetSize_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ErrorText is not null)
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }

    private void Lossless_Changed(object sender, RoutedEventArgs e)
    {
        // Lossless ignores the quality scale, so stop pretending it applies.
        if (QualityPanel is not null)
        {
            QualityPanel.IsEnabled = LosslessCheckBox.IsChecked != true;
        }
    }

    private void UpdateLabel()
    {
        if (QualityLabel is null)
        {
            return;
        }

        QualityLabel.Text = Strings.CompressQualityFormat((int)QualitySlider.Value);
    }

    private static int? TagValue(ComboBox box)
    {
        if (box.SelectedItem is ComboBoxItem { Tag: string tag } &&
            int.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
            value > 0)
        {
            return value;
        }

        return null;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        long? targetBytes = null;

        if (TargetModeRadio.IsChecked == true)
        {
            // Accept both "1,5" and "1.5": people type whichever their keyboard gives.
            var text = TargetSizeTextBox.Text.Trim().Replace(',', '.');
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var megabytes) ||
                megabytes <= 0)
            {
                ErrorText.Text = Strings.CompressTargetInvalid;
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            targetBytes = (long)(megabytes * 1024 * 1024);

            if (_sourceSizeBytes > 0 && targetBytes >= _sourceSizeBytes)
            {
                ErrorText.Text = Strings.CompressTargetTooBig(FormatMegabytes(_sourceSizeBytes));
                ErrorText.Visibility = Visibility.Visible;
                return;
            }
        }

        Result = new CompressOptions(
            Quality: (int)QualitySlider.Value,
            TargetSizeBytes: targetBytes,
            MaxLongEdge: _kind == MediaKind.Image ? TagValue(MaxEdgeComboBox) : null,
            Lossless: _kind == MediaKind.Image && LosslessCheckBox.IsChecked == true,
            MaxVideoHeight: _kind == MediaKind.Video ? TagValue(ResolutionComboBox) : null,
            AudioBitrateKbps: _kind is MediaKind.Video or MediaKind.Audio ? TagValue(AudioBitrateComboBox) : null);

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
