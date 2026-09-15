using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Mandarin.App.Services;
using Mandarin.Core.AdvancedTools;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using UserControl = System.Windows.Controls.UserControl;
using TextBlock = System.Windows.Controls.TextBlock;
using StackPanel = System.Windows.Controls.StackPanel;
using Canvas = System.Windows.Controls.Canvas;
using Orientation = System.Windows.Controls.Orientation;
using Cursors = System.Windows.Input.Cursors;
using Key = System.Windows.Input.Key;
using Mandarin.App.Resources;
using Mandarin.App.Themes;

namespace Mandarin.App.Views;

public sealed record RadialMenuItem(
    string ActionId,
    string Title,
    string Subtitle,
    bool IsAdvancedTool,
    AdvancedToolKind? ToolKind = null,
    bool IsMerge = false);

public partial class RadialMenuControl : UserControl
{
    private static readonly Geometry CompressIconGeometry = Geometry.Parse("M4,12l1.41,1.41L11,7.83V20h2V7.83l5.58,5.59L20,12l-8,-8 -8,8z");
    private static readonly Geometry CropIconGeometry = Geometry.Parse("M17,15h2V7c0,-1.1 -0.9,-2 -2,-2H9v2h8v8zM7,17V1H5v4H1v2h4v10c0,1.1 0.9,2 2,2h10v4h2v-4h4v-2H7z");
    private static readonly Geometry TrimIconGeometry = Geometry.Parse("M9.64,7.64c.23,-.5 0.36,-1.05 0.36,-1.64 0,-2.21 -1.79,-4 -4,-4S2,3.79 2,6s1.79,4 4,4c.59,0 1.14,-0.13 1.64,-0.36L10,12l-2.36,2.36C7.14,14.13 6.59,14 6,14c-2.21,0 -4,1.79 -4,4s1.79,4 4,4 4,-1.79 4,-4c0,-0.59 -0.13,-1.14 -0.36,-1.64L12,14l7,7h3v-1L9.64,7.64zM6,8c-1.1,0 -2,-0.9 -2,-2s0.9,-2 2,-2 2,0.9 2,2 -0.9,2 -2,2zm0,12c-1.1,0 -2,-0.9 -2,-2s0.9,-2 2,-2 2,0.9 2,2 -0.9,2 -2,2zm6,-7.5c-.28,0 -.5,-0.22 -.5,-0.5s0.22,-0.5 0.5,-0.5 0.5,0.22 0.5,0.5 -0.22,0.5 -0.5,0.5zM19,3l-6,6 2,2 7,-7V3h-3z");
    private static readonly Geometry SplitIconGeometry = Geometry.Parse("M4,6c-1.1,0 -2,0.9 -2,2v8c0,1.1 0.9,2 2,2h5V6H4zm16,-2h-5v16h5c1.1,0 2,-0.9 2,-2V6c0,-1.1 -0.9,-2 -2,-2z");
    private static readonly Geometry MetadataIconGeometry = Geometry.Parse("M21.41,11.58l-9-9C12.05,2.22 11.55,2 11,2H4c-1.1,0 -2,0.9 -2,2v7c0,0.55 0.22,1.05 0.59,1.42l9,9c0.36,0.36 0.86,0.58 1.41,0.58s1.05,-0.22 1.41,-0.59l7,-7c0.37,-0.36 0.59,-0.86 0.59,-1.41s-0.23,-1.06 -0.59,-1.42zM5.5,7C4.67,7 4,6.33 4,5.5S4.67,4 5.5,4 7,4.67 7,5.5 6.33,7 5.5,7z");
    private static readonly Geometry MergeIconGeometry = Geometry.Parse("M4,6H2v14c0,1.1 0.9,2 2,2h14v-2H4V6zm16,-4H8c-1.1,0 -2,0.9 -2,2v12c0,1.1 0.9,2 2,2h12c1.1,0 2,-0.9 2,-2V4c0,-1.1 -0.9,-2 -2,-2zm0,14H8V4h12v12z");
    private static readonly Geometry EditPhotoIconGeometry = Geometry.Parse("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z");
    private static readonly Geometry RedactIconGeometry = Geometry.Parse("M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z");

    // Read per rebuild (which happens on every drag) so a theme switch is picked up.
    private static Brush NormalPetalFill => ThemeBrushes.WheelPetal;
    private static Brush NormalPetalStroke => ThemeBrushes.WheelPetalStroke;

    private static Brush HighlightPetalStroke => ThemeBrushes.AccentDark;

    private static readonly LinearGradientBrush HighlightPetalFill = new()
    {
        StartPoint = new Point(0, 0),
        EndPoint = new Point(1, 1),
        GradientStops = new GradientStopCollection
        {
            new GradientStop(Color.FromRgb(0xFF, 0xA8, 0x34), 0.0),
            new GradientStop(Color.FromRgb(0xEB, 0x5E, 0x14), 1.0)
        }
    };

    private IReadOnlyList<RadialMenuItem> _items = Array.Empty<RadialMenuItem>();
    private readonly List<Path> _petalPaths = new();
    private readonly List<UIElement> _petalLabels = new();
    private int? _selectedIndex;

    public event Action<RadialMenuItem>? ItemActivated;
    public event Action? Cancelled;

    public IReadOnlyList<RadialMenuItem> Items => _items;
    public int? SelectedIndex => _selectedIndex;
    public RadialMenuItem? SelectedItem => (_selectedIndex >= 0 && _selectedIndex < _items.Count) ? _items[_selectedIndex.Value] : null;

    static RadialMenuControl()
    {
        NormalPetalFill.Freeze();
        NormalPetalStroke.Freeze();
        HighlightPetalStroke.Freeze();
        HighlightPetalFill.Freeze();
    }

    public RadialMenuControl()
    {
        InitializeComponent();
        Loaded += (_, _) => Focus();
    }

    public void SetItems(IReadOnlyList<RadialMenuItem> items, bool isAdvancedMode)
    {
        _items = items;
        _selectedIndex = null;

        // Update mode badges
        if (isAdvancedMode)
        {
            ShiftKeyBadge.Background = ThemeBrushes.SurfaceSunken;
            ((TextBlock)ShiftKeyBadge.Child).Foreground = ThemeBrushes.TextMuted;
            AltKeyBadge.Background = ThemeBrushes.Accent;
            AltKeyBadgeText.Foreground = Brushes.White;
            ModeHintText.Text = Strings.RadialModeTools;
        }
        else
        {
            ShiftKeyBadge.Background = ThemeBrushes.Accent;
            ((TextBlock)ShiftKeyBadge.Child).Foreground = Brushes.White;
            AltKeyBadge.Background = ThemeBrushes.SurfaceSunken;
            AltKeyBadgeText.Foreground = ThemeBrushes.TextMuted;
            ModeHintText.Text = Strings.RadialModeConvert;
        }

        RebuildPetals();
        SelectIndex(items.Count > 0 ? 0 : null, playSound: false);
    }

    private void RebuildPetals()
    {
        PetalsCanvas.Children.Clear();
        _petalPaths.Clear();
        _petalLabels.Clear();

        int count = _items.Count;
        if (count == 0)
        {
            CenterMainText.Text = "—";
            CenterSubText.Text = "No actions";
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var item = _items[i];
            var geometry = RadialPetalGeometry.CreatePetalGeometry(i, count);

            var path = new Path
            {
                Data = geometry,
                Fill = NormalPetalFill,
                Stroke = NormalPetalStroke,
                StrokeThickness = 1.0,
                Cursor = Cursors.Hand,
                Tag = i
            };

            int capturedIndex = i;
            path.MouseEnter += (_, _) => SelectIndex(capturedIndex, playSound: true);
            path.MouseLeftButtonUp += (_, _) => ActivateSelected();

            PetalsCanvas.Children.Add(path);
            _petalPaths.Add(path);

            var labelPos = RadialPetalGeometry.GetLabelCenter(i, count);

            if (item.IsAdvancedTool)
            {
                var panel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false
                };

                var iconGeom = GetToolIconGeometry(item);
                if (iconGeom != null)
                {
                    var iconPath = new Path
                    {
                        Data = iconGeom,
                        Fill = ThemeBrushes.TextSecondary,
                        Width = 16,
                        Height = 16,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    panel.Children.Add(iconPath);
                }

                var tb = new TextBlock
                {
                    Text = item.Title,
                    FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                    FontSize = 10.0,
                    FontWeight = FontWeights.Bold,
                    Foreground = ThemeBrushes.TextPrimary,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 0)
                };
                panel.Children.Add(tb);

                panel.Measure(new Size(80, 80));
                Canvas.SetLeft(panel, labelPos.X - panel.DesiredSize.Width / 2.0);
                Canvas.SetTop(panel, labelPos.Y - panel.DesiredSize.Height / 2.0);

                PetalsCanvas.Children.Add(panel);
                _petalLabels.Add(panel);
            }
            else
            {
                var tb = new TextBlock
                {
                    Text = item.Title,
                    FontFamily = new FontFamily("Segoe UI Variable Display, Segoe UI"),
                    FontSize = count > 8 ? 11.0 : (count > 6 ? 12.0 : 13.0),
                    FontWeight = FontWeights.Bold,
                    Foreground = ThemeBrushes.TextPrimary,
                    IsHitTestVisible = false,
                    TextAlignment = TextAlignment.Center
                };

                tb.Measure(new Size(80, 80));
                Canvas.SetLeft(tb, labelPos.X - tb.DesiredSize.Width / 2.0);
                Canvas.SetTop(tb, labelPos.Y - tb.DesiredSize.Height / 2.0);

                PetalsCanvas.Children.Add(tb);
                _petalLabels.Add(tb);
            }
        }
    }

    private static Geometry? GetToolIconGeometry(RadialMenuItem item)
    {
        if (item.IsMerge) return MergeIconGeometry;
        return item.ToolKind switch
        {
            AdvancedToolKind.Compress => CompressIconGeometry,
            AdvancedToolKind.Crop => CropIconGeometry,
            AdvancedToolKind.Trim => TrimIconGeometry,
            AdvancedToolKind.Split => SplitIconGeometry,
            AdvancedToolKind.StripMetadata => MetadataIconGeometry,
            AdvancedToolKind.EditMetadata => MetadataIconGeometry,
            AdvancedToolKind.EditPhoto => EditPhotoIconGeometry,
            AdvancedToolKind.RedactPhoto => RedactIconGeometry,
            AdvancedToolKind.RedactVideo => RedactIconGeometry,
            _ => null
        };
    }

    public void SelectIndex(int? index, bool playSound = true)
    {
        if (_selectedIndex == index) return;

        int? oldIndex = _selectedIndex;
        _selectedIndex = index;

        if (oldIndex.HasValue && oldIndex.Value >= 0 && oldIndex.Value < _petalPaths.Count)
        {
            _petalPaths[oldIndex.Value].Fill = NormalPetalFill;
            _petalPaths[oldIndex.Value].Stroke = NormalPetalStroke;
            _petalPaths[oldIndex.Value].StrokeThickness = 1.0;
            _petalPaths[oldIndex.Value].Effect = null;

            UpdateLabelAppearance(oldIndex.Value, isSelected: false);
        }

        if (index.HasValue && index.Value >= 0 && index.Value < _petalPaths.Count)
        {
            _petalPaths[index.Value].Fill = HighlightPetalFill;
            _petalPaths[index.Value].Stroke = HighlightPetalStroke;
            _petalPaths[index.Value].StrokeThickness = 1.3;
            _petalPaths[index.Value].Effect = new DropShadowEffect
            {
                BlurRadius = 16,
                ShadowDepth = 0,
                Opacity = 0.45,
                Color = ThemeBrushes.AccentColor
            };

            UpdateLabelAppearance(index.Value, isSelected: true);

            var item = _items[index.Value];
            CenterMainText.Text = item.Title;
            CenterSubText.Text = item.Subtitle;

            if (playSound)
            {
                SoundEffectService.PlayTick();
            }
        }
        else
        {
            CenterMainText.Text = Strings.RadialCenterTitle;
            CenterSubText.Text = Strings.RadialCenterHint;
        }
    }

    private void UpdateLabelAppearance(int index, bool isSelected)
    {
        if (index < 0 || index >= _petalLabels.Count) return;

        var element = _petalLabels[index];
        var textColor = isSelected ? Brushes.White : ThemeBrushes.TextPrimary;

        if (element is TextBlock tb)
        {
            tb.Foreground = textColor;
        }
        else if (element is StackPanel sp)
        {
            foreach (var child in sp.Children)
            {
                if (child is TextBlock childTb)
                {
                    childTb.Foreground = textColor;
                }
                else if (child is Path childPath)
                {
                    childPath.Fill = isSelected ? Brushes.White : ThemeBrushes.TextSecondary;
                }
            }
        }
    }

    public void UpdateDragHoverPosition(Point offsetFromCenter)
    {
        int? hit = RadialPetalGeometry.HitTestAngle(offsetFromCenter, _items.Count);
        if (hit != _selectedIndex)
        {
            SelectIndex(hit, playSound: true);
        }
    }

    public void ActivateSelected()
    {
        if (_selectedIndex.HasValue && _selectedIndex.Value >= 0 && _selectedIndex.Value < _items.Count)
        {
            var item = _items[_selectedIndex.Value];
            SoundEffectService.PlaySuccess();
            ItemActivated?.Invoke(item);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_items.Count == 0) return;

        int count = _items.Count;
        int current = _selectedIndex ?? 0;

        switch (e.Key)
        {
            case Key.Right:
            case Key.Down:
                SelectIndex((current + 1) % count, playSound: true);
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Up:
                SelectIndex((current - 1 + count) % count, playSound: true);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                ActivateSelected();
                e.Handled = true;
                break;
            case Key.Escape:
                Cancelled?.Invoke();
                e.Handled = true;
                break;
        }
    }
}
