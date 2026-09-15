using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Mandarin.Core.Conversion.VideoAudio;
using Mandarin.Core.AdvancedTools;
using Image = System.Windows.Controls.Image;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace Mandarin.App.Views;

public partial class TrimWindow : Window
{
    /// <summary>How many frames the filmstrip samples across the clip.</summary>
    private const int FilmstripFrameCount = 8;

    /// <summary>Matches the timeline track's height, so frames need no scaling.</summary>
    private const int FilmstripFrameHeight = 52;

    private readonly TimeSpan _totalDuration;
    private readonly DispatcherTimer _playbackTimer;
    private readonly List<string> _filmstripTempFiles = new();
    private readonly CancellationTokenSource _filmstripCts = new();

    private double _startFraction = 0.0;
    private double _endFraction = 1.0;
    private bool _isDraggingLeft;
    private bool _isDraggingRight;
    private bool _isPlaying;

    public TrimOptions? Result { get; private set; }

    public TrimWindow(TimeSpan? duration, string? mediaPath = null, IFfmpegProcessRunner? ffmpegRunner = null)
    {
        InitializeComponent();
        DialogChrome.Apply(this, HeaderBar, CardRoot);
        OutputPreview.Show(OutputPreviewText, mediaPath, AdvancedToolKind.Trim);

        _totalDuration = duration ?? TimeSpan.FromSeconds(10);
        if (_totalDuration.TotalSeconds < 0.1) _totalDuration = TimeSpan.FromSeconds(10);

        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _playbackTimer.Tick += PlaybackTimer_Tick;

        if (!string.IsNullOrEmpty(mediaPath) && System.IO.File.Exists(mediaPath))
        {
            try
            {
                MediaPreview.Source = new Uri(mediaPath);
            }
            catch { }
        }

        UpdateLabels();

        if (ffmpegRunner is not null && !string.IsNullOrEmpty(mediaPath) && File.Exists(mediaPath))
        {
            // Fire and forget: the placeholder strip is already on screen, and real
            // frames replace it whenever ffmpeg gets around to producing them.
            _ = LoadFilmstripAsync(ffmpegRunner, mediaPath);
        }

        Closed += (_, _) => CleanUpFilmstrip();
    }

    private async Task LoadFilmstripAsync(IFfmpegProcessRunner ffmpegRunner, string mediaPath)
    {
        try
        {
            // Extraction is process-bound work; keep it off the UI thread and come
            // back explicitly, rather than relying on the captured context.
            var frames = await VideoFrameExtractor.ExtractStripAsync(
                    ffmpegRunner, mediaPath, _totalDuration, FilmstripFrameCount, FilmstripFrameHeight, _filmstripCts.Token)
                .ConfigureAwait(false);

            if (frames.Count == 0 || _filmstripCts.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() => ApplyFilmstrip(frames));
        }
        catch (OperationCanceledException)
        {
            // The dialog closed while frames were still being extracted.
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UriFormatException)
        {
            // A missing or unreadable frame just means the placeholder strip stays.
        }
    }

    private void ApplyFilmstrip(IReadOnlyList<string> frames)
    {
        _filmstripTempFiles.AddRange(frames);

        FilmstripFrames.Columns = frames.Count;
        foreach (var frame in frames)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(frame);
            bitmap.EndInit();
            bitmap.Freeze();

            FilmstripFrames.Children.Add(new Image
            {
                Source = bitmap,
                Stretch = Stretch.UniformToFill,
                Opacity = 0.75,
            });
        }

        FilmstripFrames.Visibility = Visibility.Visible;
        FilmstripPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void CleanUpFilmstrip()
    {
        _filmstripCts.Cancel();

        foreach (var file in _filmstripTempFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A leftover temp frame is harmless; never fail closing over it.
            }
        }

        _filmstripTempFiles.Clear();
    }

    private void TimelineGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTimelineLayout();
    }

    private void UpdateTimelineLayout()
    {
        double width = TimelineGrid.ActualWidth;
        if (width <= 0) return;

        double leftX = _startFraction * width;
        double rightX = _endFraction * width;
        double rangeW = Math.Max(20, rightX - leftX);

        Canvas.SetLeft(TrimRangeBorder, leftX);
        TrimRangeBorder.Width = rangeW;
    }

    private void UpdateLabels()
    {
        var start = TimeSpan.FromSeconds(_totalDuration.TotalSeconds * _startFraction);
        var end = TimeSpan.FromSeconds(_totalDuration.TotalSeconds * _endFraction);
        var selected = end - start;

        StartTimeText.Text = $"Start {start:mm\\:ss\\.ff}";
        EndTimeText.Text = $"End {end:mm\\:ss\\.ff}";
        SelectedDurationText.Text = $"{selected:mm\\:ss\\.ff} selected";
        CurrentPositionText.Text = $"{start:mm\\:ss\\.ff} / {_totalDuration:mm\\:ss\\.ff}";
    }

    private void LeftHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingLeft = true;
        ((UIElement)sender).CaptureMouse();
    }

    private void LeftHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingLeft) return;

        double width = TimelineGrid.ActualWidth;
        if (width <= 0) return;

        var pos = e.GetPosition(TimelineCanvas);
        _startFraction = Math.Clamp(pos.X / width, 0.0, _endFraction - 0.05);
        UpdateTimelineLayout();
        UpdateLabels();
    }

    private void RightHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingRight = true;
        ((UIElement)sender).CaptureMouse();
    }

    private void RightHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingRight) return;

        double width = TimelineGrid.ActualWidth;
        if (width <= 0) return;

        var pos = e.GetPosition(TimelineCanvas);
        _endFraction = Math.Clamp(pos.X / width, _startFraction + 0.05, 1.0);
        UpdateTimelineLayout();
        UpdateLabels();
    }

    private void Handle_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingLeft = false;
        _isDraggingRight = false;
        ((UIElement)sender).ReleaseMouseCapture();
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            MediaPreview.Pause();
            _playbackTimer.Stop();
            PlayPauseButton.Content = "▶";
            _isPlaying = false;
        }
        else
        {
            MediaPreview.Play();
            _playbackTimer.Start();
            PlayPauseButton.Content = "⏸";
            _isPlaying = true;
        }
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        var pos = MediaPreview.Position;
        var end = TimeSpan.FromSeconds(_totalDuration.TotalSeconds * _endFraction);
        if (pos >= end)
        {
            MediaPreview.Position = TimeSpan.FromSeconds(_totalDuration.TotalSeconds * _startFraction);
        }
        CurrentPositionText.Text = $"{pos:mm\\:ss\\.ff} / {_totalDuration:mm\\:ss\\.ff}";
    }

    private void StepBack_Click(object sender, RoutedEventArgs e)
    {
        var pos = MediaPreview.Position - TimeSpan.FromSeconds(1);
        if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
        MediaPreview.Position = pos;
        CurrentPositionText.Text = $"{pos:mm\\:ss\\.ff} / {_totalDuration:mm\\:ss\\.ff}";
    }

    private void StepForward_Click(object sender, RoutedEventArgs e)
    {
        var pos = MediaPreview.Position + TimeSpan.FromSeconds(1);
        if (pos > _totalDuration) pos = _totalDuration;
        MediaPreview.Position = pos;
        CurrentPositionText.Text = $"{pos:mm\\:ss\\.ff} / {_totalDuration:mm\\:ss\\.ff}";
    }

    private void MediaPreview_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (MediaPreview.NaturalDuration.HasTimeSpan)
        {
            MediaPreview.Position = TimeSpan.FromSeconds(_totalDuration.TotalSeconds * _startFraction);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _playbackTimer.Stop();
        DialogResult = false;
        Close();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        _playbackTimer.Stop();
        var start = TimeSpan.FromSeconds(_totalDuration.TotalSeconds * _startFraction);
        var end = TimeSpan.FromSeconds(_totalDuration.TotalSeconds * _endFraction);

        Result = new TrimOptions(start, end);
        DialogResult = true;
        Close();
    }
}
