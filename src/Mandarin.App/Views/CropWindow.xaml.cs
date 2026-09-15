using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ImageMagick;
using Mandarin.App.Resources;
using Mandarin.App.Themes;
using Mandarin.Core.AdvancedTools;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using ComboBox = System.Windows.Controls.ComboBox;
using Cursor = System.Windows.Input.Cursor;
using Cursors = System.Windows.Input.Cursors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace Mandarin.App.Views;

public partial class CropWindow : Window
{
    /// <summary>Which part of the selection the current drag is moving.</summary>
    private enum DragMode
    {
        None,
        New,
        Move,
        TopLeft,
        Top,
        TopRight,
        Right,
        BottomRight,
        Bottom,
        BottomLeft,
        Left,
    }

    private const double HandleSize = 10;
    private const double MinSelection = 8;

    private readonly int _originalWidth;
    private readonly int _originalHeight;
    private readonly Rectangle[] _handles = new Rectangle[8];

    private DragMode _dragMode = DragMode.None;
    private Point _dragStart;
    private Rect _dragOriginRect;

    /// <summary>Width / height the selection is locked to, or null when free.</summary>
    private double? _aspect;

    public CropOptions? Result { get; private set; }

    public CropWindow(string previewImagePath, string? sourcePath = null)
    {
        InitializeComponent();
        DialogChrome.Apply(this, HeaderBar, CardRoot);
        OutputPreview.Show(OutputPreviewText, sourcePath, AdvancedToolKind.Crop);

        try
        {
            using var magickImage = new MagickImage(previewImagePath);
            using var ms = new MemoryStream();
            magickImage.Write(ms, MagickFormat.Bmp);
            ms.Seek(0, SeekOrigin.Begin);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            _originalWidth = bitmap.PixelWidth;
            _originalHeight = bitmap.PixelHeight;
            PreviewImage.Source = bitmap;
        }
        catch (Exception ex) when (ex is MagickException or IOException or NotSupportedException or ArgumentException)
        {
            // Never crash on bad input: fall back to a nominal frame so the dialog
            // still opens and can be cancelled.
            _originalWidth = 800;
            _originalHeight = 600;
        }

        CreateHandles();
        UpdateSizeText();

        // Arrow keys nudge and resize, so a selection can be placed exactly.
        PreviewKeyDown += CropWindow_PreviewKeyDown;
    }

    // ---------- selection geometry ----------

    private Rect Selection => new(
        Canvas.GetLeft(SelectionRectangle),
        Canvas.GetTop(SelectionRectangle),
        SelectionRectangle.Width,
        SelectionRectangle.Height);

    private bool HasSelection => SelectionRectangle.Visibility == Visibility.Visible &&
                                 SelectionRectangle.Width >= MinSelection &&
                                 SelectionRectangle.Height >= MinSelection;

    private void SetSelection(Rect rect)
    {
        if (SelectionCanvas.Width <= 0 || SelectionCanvas.Height <= 0)
        {
            return;
        }

        // Keep it inside the image, and never smaller than a grabbable size.
        var width = Math.Clamp(rect.Width, MinSelection, SelectionCanvas.Width);
        var height = Math.Clamp(rect.Height, MinSelection, SelectionCanvas.Height);
        var x = Math.Clamp(rect.X, 0, SelectionCanvas.Width - width);
        var y = Math.Clamp(rect.Y, 0, SelectionCanvas.Height - height);

        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
        SelectionRectangle.Visibility = Visibility.Visible;

        UpdateOverlay();
        UpdateSizeText();
        ErrorText.Visibility = Visibility.Collapsed;
    }

    /// <summary>Applies the locked aspect, keeping the dragged corner's anchor fixed.</summary>
    private Rect ApplyAspect(Rect rect, DragMode mode)
    {
        if (_aspect is not { } aspect || aspect <= 0)
        {
            return rect;
        }

        var width = rect.Width;
        var height = rect.Height;

        // Horizontal-only handles drive height; vertical-only drive width; corners
        // follow whichever dimension the user stretched further.
        if (mode is DragMode.Left or DragMode.Right)
        {
            height = width / aspect;
        }
        else if (mode is DragMode.Top or DragMode.Bottom)
        {
            width = height * aspect;
        }
        else if (width / Math.Max(height, 0.001) > aspect)
        {
            width = height * aspect;
        }
        else
        {
            height = width / aspect;
        }

        var x = mode is DragMode.TopLeft or DragMode.Left or DragMode.BottomLeft
            ? rect.Right - width
            : rect.X;
        var y = mode is DragMode.TopLeft or DragMode.Top or DragMode.TopRight
            ? rect.Bottom - height
            : rect.Y;

        return new Rect(x, y, width, height);
    }

    // ---------- handles ----------

    private void CreateHandles()
    {
        for (var i = 0; i < _handles.Length; i++)
        {
            var mode = (DragMode)((int)DragMode.TopLeft + i);
            var handle = new Rectangle
            {
                Width = HandleSize,
                Height = HandleSize,
                Fill = ThemeBrushes.Surface,
                Stroke = ThemeBrushes.Accent,
                StrokeThickness = 1.5,
                RadiusX = 2,
                RadiusY = 2,
                Visibility = Visibility.Collapsed,
                Cursor = CursorFor(mode),
                Tag = mode,
            };

            handle.MouseLeftButtonDown += Handle_MouseLeftButtonDown;
            _handles[i] = handle;
            SelectionCanvas.Children.Add(handle);
        }
    }

    private static Cursor CursorFor(DragMode mode) => mode switch
    {
        DragMode.TopLeft or DragMode.BottomRight => Cursors.SizeNWSE,
        DragMode.TopRight or DragMode.BottomLeft => Cursors.SizeNESW,
        DragMode.Top or DragMode.Bottom => Cursors.SizeNS,
        DragMode.Left or DragMode.Right => Cursors.SizeWE,
        _ => Cursors.SizeAll,
    };

    private void UpdateOverlay()
    {
        var r = Selection;
        var w = SelectionCanvas.Width;
        var h = SelectionCanvas.Height;

        void Place(Rectangle shade, double x, double y, double width, double height)
        {
            Canvas.SetLeft(shade, x);
            Canvas.SetTop(shade, y);
            shade.Width = Math.Max(0, width);
            shade.Height = Math.Max(0, height);
            shade.Visibility = Visibility.Visible;
        }

        Place(ShadeTop, 0, 0, w, r.Y);
        Place(ShadeBottom, 0, r.Bottom, w, h - r.Bottom);
        Place(ShadeLeft, 0, r.Y, r.X, r.Height);
        Place(ShadeRight, r.Right, r.Y, w - r.Right, r.Height);

        void PlaceGuide(Line line, double x1, double y1, double x2, double y2)
        {
            line.X1 = x1;
            line.Y1 = y1;
            line.X2 = x2;
            line.Y2 = y2;
            line.Visibility = Visibility.Visible;
        }

        PlaceGuide(GuideV1, r.X + (r.Width / 3), r.Y, r.X + (r.Width / 3), r.Bottom);
        PlaceGuide(GuideV2, r.X + (2 * r.Width / 3), r.Y, r.X + (2 * r.Width / 3), r.Bottom);
        PlaceGuide(GuideH1, r.X, r.Y + (r.Height / 3), r.Right, r.Y + (r.Height / 3));
        PlaceGuide(GuideH2, r.X, r.Y + (2 * r.Height / 3), r.Right, r.Y + (2 * r.Height / 3));

        var positions = new (double X, double Y)[]
        {
            (r.X, r.Y),
            (r.X + (r.Width / 2), r.Y),
            (r.Right, r.Y),
            (r.Right, r.Y + (r.Height / 2)),
            (r.Right, r.Bottom),
            (r.X + (r.Width / 2), r.Bottom),
            (r.X, r.Bottom),
            (r.X, r.Y + (r.Height / 2)),
        };

        for (var i = 0; i < _handles.Length; i++)
        {
            Canvas.SetLeft(_handles[i], positions[i].X - (HandleSize / 2));
            Canvas.SetTop(_handles[i], positions[i].Y - (HandleSize / 2));
            _handles[i].Visibility = Visibility.Visible;
        }
    }

    private void HideOverlay()
    {
        foreach (var shade in new[] { ShadeTop, ShadeBottom, ShadeLeft, ShadeRight })
        {
            shade.Visibility = Visibility.Collapsed;
        }

        foreach (var guide in new[] { GuideV1, GuideV2, GuideH1, GuideH2 })
        {
            guide.Visibility = Visibility.Collapsed;
        }

        foreach (var handle in _handles)
        {
            handle.Visibility = Visibility.Collapsed;
        }
    }

    // ---------- pixel readout ----------

    private (int X, int Y, int Width, int Height)? SelectionInImagePixels()
    {
        if (!HasSelection || SelectionCanvas.Width <= 0 || SelectionCanvas.Height <= 0)
        {
            return null;
        }

        var r = Selection;
        var x = Math.Clamp((int)Math.Round(r.X / SelectionCanvas.Width * _originalWidth), 0, _originalWidth - 1);
        var y = Math.Clamp((int)Math.Round(r.Y / SelectionCanvas.Height * _originalHeight), 0, _originalHeight - 1);
        var w = Math.Clamp((int)Math.Round(r.Width / SelectionCanvas.Width * _originalWidth), 1, _originalWidth - x);
        var h = Math.Clamp((int)Math.Round(r.Height / SelectionCanvas.Height * _originalHeight), 1, _originalHeight - y);
        return (x, y, w, h);
    }

    private void UpdateSizeText()
    {
        var pixels = SelectionInImagePixels();
        SizeText.Text = pixels is { } p
            ? Strings.CropSize(p.Width, p.Height)
            : Strings.CropSize(_originalWidth, _originalHeight);
    }

    // ---------- layout ----------

    private void ViewportGrid_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateCanvasBounds();

    private void UpdateCanvasBounds()
    {
        if (ViewportGrid.ActualWidth <= 0 || ViewportGrid.ActualHeight <= 0 || _originalWidth <= 0 || _originalHeight <= 0)
        {
            return;
        }

        var gridW = ViewportGrid.ActualWidth;
        var gridH = ViewportGrid.ActualHeight;

        var scale = Math.Min(gridW / _originalWidth, gridH / _originalHeight);
        var renderedW = _originalWidth * scale;
        var renderedH = _originalHeight * scale;

        SelectionCanvas.Width = renderedW;
        SelectionCanvas.Height = renderedH;
        SelectionCanvas.Margin = new Thickness((gridW - renderedW) / 2.0, (gridH - renderedH) / 2.0, 0, 0);

        if (HasSelection)
        {
            SetSelection(Selection);
        }
    }

    // ---------- mouse ----------

    private void Handle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Rectangle { Tag: DragMode mode })
        {
            return;
        }

        _dragMode = mode;
        _dragStart = e.GetPosition(SelectionCanvas);
        _dragOriginRect = Selection;
        SelectionCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(SelectionCanvas);
        _dragStart = position;
        _dragOriginRect = Selection;

        // Clicking inside an existing selection moves it; anywhere else starts a new one.
        _dragMode = HasSelection && Selection.Contains(position) ? DragMode.Move : DragMode.New;

        if (_dragMode == DragMode.New)
        {
            SetSelection(new Rect(position.X, position.Y, MinSelection, MinSelection));
        }

        SelectionCanvas.CaptureMouse();
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragMode == DragMode.None)
        {
            return;
        }

        var current = e.GetPosition(SelectionCanvas);
        var dx = current.X - _dragStart.X;
        var dy = current.Y - _dragStart.Y;
        var origin = _dragOriginRect;

        if (_dragMode == DragMode.New)
        {
            var rect = new Rect(
                Math.Min(current.X, _dragStart.X),
                Math.Min(current.Y, _dragStart.Y),
                Math.Abs(current.X - _dragStart.X),
                Math.Abs(current.Y - _dragStart.Y));

            // The corner under the cursor is the one that moves, so the opposite
            // one is the anchor an aspect lock has to respect.
            var anchored = current.X < _dragStart.X
                ? (current.Y < _dragStart.Y ? DragMode.TopLeft : DragMode.BottomLeft)
                : (current.Y < _dragStart.Y ? DragMode.TopRight : DragMode.BottomRight);

            SetSelection(ApplyAspect(rect, anchored));
            return;
        }

        if (_dragMode == DragMode.Move)
        {
            SetSelection(new Rect(origin.X + dx, origin.Y + dy, origin.Width, origin.Height));
            return;
        }

        var left = origin.X;
        var top = origin.Y;
        var right = origin.Right;
        var bottom = origin.Bottom;

        if (_dragMode is DragMode.TopLeft or DragMode.Left or DragMode.BottomLeft)
        {
            left = Math.Min(origin.X + dx, right - MinSelection);
        }

        if (_dragMode is DragMode.TopRight or DragMode.Right or DragMode.BottomRight)
        {
            right = Math.Max(origin.Right + dx, left + MinSelection);
        }

        if (_dragMode is DragMode.TopLeft or DragMode.Top or DragMode.TopRight)
        {
            top = Math.Min(origin.Y + dy, bottom - MinSelection);
        }

        if (_dragMode is DragMode.BottomLeft or DragMode.Bottom or DragMode.BottomRight)
        {
            bottom = Math.Max(origin.Bottom + dy, top + MinSelection);
        }

        SetSelection(ApplyAspect(new Rect(left, top, right - left, bottom - top), _dragMode));
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragMode = DragMode.None;
        SelectionCanvas.ReleaseMouseCapture();
    }

    // ---------- keyboard ----------

    private void CropWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Leave the aspect dropdown's own arrow handling alone.
        if (Keyboard.FocusedElement is ComboBox or ComboBoxItem || !HasSelection)
        {
            return;
        }

        var step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10.0 : 1.0;
        var resize = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var r = Selection;

        Rect? moved = e.Key switch
        {
            Key.Left => resize ? new Rect(r.X, r.Y, r.Width - step, r.Height) : new Rect(r.X - step, r.Y, r.Width, r.Height),
            Key.Right => resize ? new Rect(r.X, r.Y, r.Width + step, r.Height) : new Rect(r.X + step, r.Y, r.Width, r.Height),
            Key.Up => resize ? new Rect(r.X, r.Y, r.Width, r.Height - step) : new Rect(r.X, r.Y - step, r.Width, r.Height),
            Key.Down => resize ? new Rect(r.X, r.Y, r.Width, r.Height + step) : new Rect(r.X, r.Y + step, r.Width, r.Height),
            _ => null,
        };

        if (moved is not { } rect)
        {
            return;
        }

        SetSelection(resize ? ApplyAspect(rect, DragMode.BottomRight) : rect);
        e.Handled = true;
    }

    // ---------- commands ----------

    private void AspectComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AspectComboBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var tag = (string)item.Tag;
        _aspect = tag switch
        {
            "source" => _originalHeight > 0 ? (double)_originalWidth / _originalHeight : null,
            "free" => null,
            _ => ParseRatio(tag),
        };

        // SelectionRectangle is null while the XAML is still loading.
        if (_aspect is not null && SelectionRectangle is not null && HasSelection)
        {
            SetSelection(ApplyAspect(Selection, DragMode.BottomRight));
        }
    }

    private static double? ParseRatio(string tag)
    {
        var parts = tag.Split(':');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var w) &&
            double.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var h) &&
            h > 0)
        {
            return w / h;
        }

        return null;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        SelectionRectangle.Visibility = Visibility.Collapsed;
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
        HideOverlay();
        UpdateSizeText();
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectionInImagePixels() is not { } pixels)
        {
            ErrorText.Text = Strings.CropNoSelection;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        Result = new CropOptions(pixels.X, pixels.Y, pixels.Width, pixels.Height);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
