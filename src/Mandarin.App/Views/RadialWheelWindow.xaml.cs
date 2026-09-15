using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using Mandarin.App.Resources;
using Mandarin.App.Services;
using Mandarin.Core.AdvancedTools;
using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.Images;
using Mandarin.Core.Conversion.Pdf;
using Mandarin.Core.Conversion.VideoAudio;
using Mandarin.Core.Merge;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Keyboard = System.Windows.Input.Keyboard;
using ModifierKeys = System.Windows.Input.ModifierKeys;

namespace Mandarin.App.Views;

/// <summary>
/// Topmost standalone radial wheel window that appears at the cursor position
/// whenever a file or folder is dragged anywhere on the system while holding Shift (or Shift+Alt).
/// </summary>
public partial class RadialWheelWindow : Window
{
    private readonly ConversionService _conversionService;
    private readonly AdvancedToolService _advancedToolService;
    private readonly MergeService _mergeService;
    private readonly IFfmpegProcessRunner _ffmpegRunner;

    private List<string>? _activeFiles;
    private bool _isAdvancedMode;
    private bool _isOpen;

    public event Action<RadialMenuItem, List<string>, Point?>? ActionTriggered;

    public RadialWheelWindow(
        ConversionService conversionService,
        AdvancedToolService advancedToolService,
        MergeService mergeService,
        IFfmpegProcessRunner ffmpegRunner)
    {
        InitializeComponent();
        _conversionService = conversionService;
        _advancedToolService = advancedToolService;
        _mergeService = mergeService;
        _ffmpegRunner = ffmpegRunner;

        RadialMenu.ItemActivated += item =>
        {
            if (_activeFiles != null && _activeFiles.Count > 0)
            {
                var files = _activeFiles;
                var dropPt = new Point(Left + 180, Top + 180);
                HideWheel();
                ActionTriggered?.Invoke(item, files, dropPt);
            }
        };

        RadialMenu.Cancelled += HideWheel;
    }

    public void ShowAtCursor(GlobalDragHookService.POINT screenPoint, bool isAlt)
    {
        _isAdvancedMode = isAlt;

        // Center on cursor (size is 360x360)
        double targetLeft = screenPoint.X - 180;
        double targetTop = screenPoint.Y - 180;

        // Keep within virtual screen bounds
        targetLeft = Math.Max(SystemParameters.VirtualScreenLeft, Math.Min(SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 360, targetLeft));
        targetTop = Math.Max(SystemParameters.VirtualScreenTop, Math.Min(SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 360, targetTop));

        Left = targetLeft;
        Top = targetTop;

        _isOpen = true;
        Show();
        Activate();

        var sb = (Storyboard)Resources["FadeInStoryboard"];
        sb.Begin();
    }

    public void HideWheel()
    {
        if (!_isOpen) return;
        _isOpen = false;
        _activeFiles = null;
        Hide();
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;

            if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
            {
                _activeFiles = files.ToList();
                bool isAlt = GlobalDragHookService.IsAltDown() || (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
                _isAdvancedMode = isAlt;
                LoadMenuItems(_activeFiles, _isAdvancedMode);
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Copy;

        bool isAlt = GlobalDragHookService.IsAltDown() || (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        if (isAlt != _isAdvancedMode && _activeFiles != null)
        {
            _isAdvancedMode = isAlt;
            LoadMenuItems(_activeFiles, _isAdvancedMode);
        }

        var pos = e.GetPosition(this);
        var centerOffset = new Point(pos.X - 180.0, pos.Y - 180.0);
        RadialMenu.UpdateDragHoverPosition(centerOffset);

        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        // Prevent false flicker: only hide if cursor is genuinely outside the window rect
        if (GlobalDragHookService.GetCursorPos(out var pt))
        {
            var winRect = new Rect(Left, Top, Width, Height);
            if (!winRect.Contains(new Point(pt.X, pt.Y)))
            {
                HideWheel();
            }
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            HideWheel();
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files)
        {
            HideWheel();
            return;
        }

        var selected = RadialMenu.SelectedItem;
        var fileList = files.ToList();
        var dropPt = new Point(Left + 180, Top + 180);
        HideWheel();

        if (selected != null)
        {
            ActionTriggered?.Invoke(selected, fileList, dropPt);
        }
        else
        {
            // If dropped in center without selecting a specific petal:
            var supported = fileList.Where(_conversionService.IsSupported).ToList();
            if (supported.Count > 0)
            {
                var target = _conversionService.GetSuggestedTarget(supported[0]);
                if (target != null)
                {
                    var item = new RadialMenuItem(target, target.ToUpperInvariant(), $"Convert to {target.ToUpperInvariant()}", false);
                    ActionTriggered?.Invoke(item, supported, dropPt);
                }
            }
        }
    }

    private void LoadMenuItems(List<string> files, bool isAdvanced)
    {
        var items = new List<RadialMenuItem>();

        if (isAdvanced)
        {
            if (files.Count > 1)
            {
                if (_mergeService.CanMerge(files))
                {
                    items.Add(new RadialMenuItem("Merge", "MERGE", "Merge all files", true, null, IsMerge: true));
                }
                items.Add(new RadialMenuItem("Compress", "COMPRESS", "Compress all files", true, AdvancedToolKind.Compress));
            }
            else if (files.Count == 1)
            {
                var tools = _advancedToolService.GetAvailableTools(files[0]);
                foreach (var tool in tools)
                {
                    var title = tool switch
                    {
                        AdvancedToolKind.EditPhoto => "EDIT",
                        AdvancedToolKind.EditMetadata => "METADATA",
                        AdvancedToolKind.RedactPhoto => "REDACT",
                        AdvancedToolKind.RedactVideo => "REDACT",
                        AdvancedToolKind.Compress => "COMPRESS",
                        AdvancedToolKind.Crop => "CROP",
                        AdvancedToolKind.Trim => "TRIM",
                        AdvancedToolKind.Split => "SPLIT",
                        AdvancedToolKind.StripMetadata => "STRIP EXIF",
                        _ => tool.ToString().ToUpperInvariant()
                    };

                    var sub = tool switch
                    {
                        AdvancedToolKind.EditPhoto => "Adjust light & color",
                        AdvancedToolKind.EditMetadata => "View & remove metadata",
                        AdvancedToolKind.RedactPhoto => "Redact faces & areas",
                        AdvancedToolKind.RedactVideo => "Redact video areas",
                        AdvancedToolKind.Compress => "Compress file",
                        AdvancedToolKind.Crop => "Crop media",
                        AdvancedToolKind.Trim => "Trim video/audio",
                        AdvancedToolKind.Split => "Split pages/media",
                        AdvancedToolKind.StripMetadata => "Remove metadata",
                        _ => tool.ToString()
                    };

                    items.Add(new RadialMenuItem(tool.ToString(), title, sub, true, tool));
                }
            }
        }
        else
        {
            var targetSets = files
                .Select(p => _conversionService.GetAvailableTargets(p).ToHashSet(StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (targetSets.Count > 0)
            {
                var common = targetSets.Aggregate((a, b) =>
                {
                    a.IntersectWith(b);
                    return a;
                });

                foreach (var target in common.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
                {
                    items.Add(new RadialMenuItem(
                        target,
                        target.ToUpperInvariant(),
                        $"Convert to {target.ToUpperInvariant()}",
                        false));
                }
            }
        }

        RadialMenu.SetItems(items, isAdvanced);
    }
}
