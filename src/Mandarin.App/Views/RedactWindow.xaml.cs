using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ImageMagick;
using Mandarin.Core.AdvancedTools;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;

namespace Mandarin.App.Views;

public partial class RedactWindow : Window
{
    private readonly int _originalWidth;
    private readonly int _originalHeight;
    private readonly List<RedactionRegion> _regions = new();

    private Point _dragStart;
    private bool _isDragging;
    private Rectangle? _currentRect;
    private RedactStyle _currentStyle = RedactStyle.Solid;

    public RedactOptions? Result { get; private set; }

    // isVideo is kept for call-site clarity: the dialog itself is identical for
    // stills and for a video's first frame, and the title stays the tool's name
    // the way Crop's and Split's do.
    public RedactWindow(string previewImagePath, bool isVideo = false, string? sourcePath = null)
    {
        InitializeComponent();
        DialogChrome.Apply(this, HeaderBar, CardRoot);
        OutputPreview.Show(OutputPreviewText, sourcePath, AdvancedToolKind.RedactPhoto);

        try
        {
            using var magickImage = new MagickImage(previewImagePath);
            _originalWidth = (int)magickImage.Width;
            _originalHeight = (int)magickImage.Height;

            using var ms = new MemoryStream();
            magickImage.Write(ms, MagickFormat.Bmp);
            ms.Seek(0, SeekOrigin.Begin);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            PreviewImage.Source = bitmap;
        }
        catch
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(previewImagePath);
                bitmap.EndInit();

                _originalWidth = bitmap.PixelWidth;
                _originalHeight = bitmap.PixelHeight;
                PreviewImage.Source = bitmap;
            }
            catch
            {
                _originalWidth = 800;
                _originalHeight = 600;
            }
        }
    }

    private void ViewportGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCanvasBounds();
        RedrawRegions();
    }

    private void UpdateCanvasBounds()
    {
        if (ViewportGrid.ActualWidth <= 0 || ViewportGrid.ActualHeight <= 0 || _originalWidth <= 0 || _originalHeight <= 0)
            return;

        double gridW = ViewportGrid.ActualWidth;
        double gridH = ViewportGrid.ActualHeight;

        double scale = Math.Min(gridW / _originalWidth, gridH / _originalHeight);
        double renderedW = _originalWidth * scale;
        double renderedH = _originalHeight * scale;

        double left = (gridW - renderedW) / 2.0;
        double top = (gridH - renderedH) / 2.0;

        SelectionCanvas.Width = renderedW;
        SelectionCanvas.Height = renderedH;
        SelectionCanvas.Margin = new Thickness(left, top, 0, 0);
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(SelectionCanvas);
        _isDragging = true;

        _currentRect = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(235, 94, 20)),
            StrokeThickness = 2,
            Fill = _currentStyle == RedactStyle.Solid ? Brushes.Black : new SolidColorBrush(Color.FromArgb(120, 100, 100, 100))
        };

        Canvas.SetLeft(_currentRect, _dragStart.X);
        Canvas.SetTop(_currentRect, _dragStart.Y);
        _currentRect.Width = 0;
        _currentRect.Height = 0;

        SelectionCanvas.Children.Add(_currentRect);
        SelectionCanvas.CaptureMouse();
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _currentRect == null) return;

        var current = e.GetPosition(SelectionCanvas);
        var x = Math.Clamp(Math.Min(current.X, _dragStart.X), 0, SelectionCanvas.Width);
        var y = Math.Clamp(Math.Min(current.Y, _dragStart.Y), 0, SelectionCanvas.Height);
        var width = Math.Min(Math.Abs(current.X - _dragStart.X), SelectionCanvas.Width - x);
        var height = Math.Min(Math.Abs(current.Y - _dragStart.Y), SelectionCanvas.Height - y);

        Canvas.SetLeft(_currentRect, x);
        Canvas.SetTop(_currentRect, y);
        _currentRect.Width = width;
        _currentRect.Height = height;
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging || _currentRect == null) return;

        _isDragging = false;
        SelectionCanvas.ReleaseMouseCapture();

        if (_currentRect.Width > 5 && _currentRect.Height > 5 && SelectionCanvas.Width > 0 && SelectionCanvas.Height > 0)
        {
            var normX = Canvas.GetLeft(_currentRect) / SelectionCanvas.Width;
            var normY = Canvas.GetTop(_currentRect) / SelectionCanvas.Height;
            var normW = _currentRect.Width / SelectionCanvas.Width;
            var normH = _currentRect.Height / SelectionCanvas.Height;

            _regions.Add(new RedactionRegion(normX, normY, normW, normH, _currentStyle));
        }
        else
        {
            SelectionCanvas.Children.Remove(_currentRect);
        }

        _currentRect = null;
        RedrawRegions();
    }

    private void RedrawRegions()
    {
        SelectionCanvas.Children.Clear();
        if (SelectionCanvas.Width <= 0 || SelectionCanvas.Height <= 0) return;

        foreach (var r in _regions)
        {
            var rect = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(235, 94, 20)),
                StrokeThickness = 2,
                Fill = r.Style == RedactStyle.Solid ? Brushes.Black : new SolidColorBrush(Color.FromArgb(140, 60, 60, 60)),
                Width = r.NormalizedWidth * SelectionCanvas.Width,
                Height = r.NormalizedHeight * SelectionCanvas.Height
            };

            Canvas.SetLeft(rect, r.NormalizedX * SelectionCanvas.Width);
            Canvas.SetTop(rect, r.NormalizedY * SelectionCanvas.Height);
            SelectionCanvas.Children.Add(rect);
        }
    }

    private void Style_Checked(object sender, RoutedEventArgs e)
    {
        if (BlurStyleRadio?.IsChecked == true) _currentStyle = RedactStyle.Blur;
        else if (PixelateStyleRadio?.IsChecked == true) _currentStyle = RedactStyle.Pixelate;
        else _currentStyle = RedactStyle.Solid;
    }

    private void DetectFaces_Click(object sender, RoutedEventArgs e)
    {
        // Add sample center-area face detection box
        _regions.Add(new RedactionRegion(0.25, 0.2, 0.25, 0.25, _currentStyle));
        RedrawRegions();
    }

    private void DetectText_Click(object sender, RoutedEventArgs e)
    {
        // Add sample bottom text detection box
        _regions.Add(new RedactionRegion(0.1, 0.75, 0.8, 0.15, _currentStyle));
        RedrawRegions();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _regions.Clear();
        SelectionCanvas.Children.Clear();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Result = new RedactOptions(_regions);
        DialogResult = true;
        Close();
    }
}
