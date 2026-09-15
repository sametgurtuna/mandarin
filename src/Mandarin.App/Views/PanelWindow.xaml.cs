using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Mandarin.App.Resources;
using Mandarin.App.Services;
using Mandarin.App.ViewModels;
using Mandarin.Core.AdvancedTools;
using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.Images;
using Mandarin.Core.Conversion.Pdf;
using Mandarin.Core.Conversion.VideoAudio;
using Mandarin.Core.Merge;
using Mandarin.Core.Settings;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using Keyboard = System.Windows.Input.Keyboard;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using Point = System.Windows.Point;

namespace Mandarin.App.Views;

/// <summary>
/// The compact floating bubble panel that sits on screen, handles bubble dragging,
/// and executes all background conversion operations with progress and notifications.
/// </summary>
public partial class PanelWindow : Window
{
    private readonly PanelViewModel _viewModel;
    private readonly ConversionService _conversionService;
    private readonly AdvancedToolService _advancedToolService;
    private readonly MergeService _mergeService;
    private readonly IFfmpegProcessRunner _ffmpegRunner;
    private readonly ISettingsService _settingsService;
    private readonly MandarinSettings _settings;
    private readonly ProgressHudWindow _progressHud = new();
    private Point? _lastDropPoint;

    public Func<RadialWheelWindow>? RadialWheelWindowAccessor { get; set; }

    public PanelWindow(
        PanelViewModel viewModel,
        ConversionService conversionService,
        AdvancedToolService advancedToolService,
        MergeService mergeService,
        IFfmpegProcessRunner ffmpegRunner,
        ISettingsService settingsService,
        MandarinSettings settings)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _settings = settings;
        _viewModel = viewModel;
        _conversionService = conversionService;
        _advancedToolService = advancedToolService;
        _mergeService = mergeService;
        _ffmpegRunner = ffmpegRunner;
        DataContext = viewModel;

        Left = viewModel.Left;
        Top = viewModel.Top;

        SoundEffectService.Initialize();
    }

    private void Panel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
            _viewModel.SavePosition(Left, Top);
        }
    }

    private void Panel_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_viewModel.VisualState == PanelVisualState.Idle)
        {
            HoverRing.Visibility = Visibility.Visible;
        }
    }

    private void Panel_MouseLeave(object sender, MouseEventArgs e)
    {
        HoverRing.Visibility = Visibility.Collapsed;
    }

    public void TriggerExternalFileRequest(string filePath)
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();

        if (!File.Exists(filePath)) return;

        if (!_conversionService.IsSupported(filePath))
        {
            ShowToast(
                Strings.ToastUnsupportedFileTitle,
                $"Mandarin doesn't know how to convert '{Path.GetFileName(filePath)}' yet.",
                isError: true,
                folderToOpen: null);
            return;
        }

        var wheel = RadialWheelWindowAccessor?.Invoke();
        if (wheel != null)
        {
            var centerPt = new GlobalDragHookService.POINT
            {
                X = (int)(Left + 32),
                Y = (int)(Top + 32)
            };
            wheel.ShowAtCursor(centerPt, isAlt: false);
        }
    }

    private void Panel_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            HoverRing.Visibility = Visibility.Visible;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void Panel_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void Panel_DragLeave(object sender, DragEventArgs e)
    {
        HoverRing.Visibility = Visibility.Collapsed;
    }

    private async void Panel_Drop(object sender, DragEventArgs e)
    {
        HoverRing.Visibility = Visibility.Collapsed;

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] { Length: > 0 } files)
        {
            return;
        }

        var fileList = files.ToList();
        var modifiers = Keyboard.Modifiers;

        if (modifiers == (ModifierKeys.Shift | ModifierKeys.Alt) || modifiers == ModifierKeys.Alt)
        {
            if (fileList.Count > 1 && _mergeService.CanMerge(fileList))
            {
                await RunMergeAsync(fileList);
                return;
            }

            if (fileList.Count == 1)
            {
                var tools = _advancedToolService.GetAvailableTools(fileList[0]);
                if (tools.Count > 0)
                {
                    await RunAdvancedToolAsync(fileList[0], tools[0]);
                    return;
                }
            }
        }

        // Shift or default drop: run suggested conversion
        var supported = fileList.Where(_conversionService.IsSupported).ToList();
        if (supported.Count > 0)
        {
            var items = new List<(string SourcePath, string Target)>();
            foreach (var file in supported)
            {
                var target = _conversionService.GetSuggestedTarget(file);
                if (target != null) items.Add((file, target));
            }

            if (items.Count > 0)
            {
                await RunBatchAsync(items);
                return;
            }
        }

        // Otherwise open the radial wheel centered on the bubble
        var wheel = RadialWheelWindowAccessor?.Invoke();
        if (wheel != null)
        {
            var centerPt = new GlobalDragHookService.POINT
            {
                X = (int)(Left + 32),
                Y = (int)(Top + 32)
            };
            wheel.ShowAtCursor(centerPt, isAlt: false);
        }
    }

    public async void ExecuteActionFromExternal(RadialMenuItem item, List<string> files, Point? dropPoint = null)
    {
        _lastDropPoint = dropPoint;

        if (item.IsMerge)
        {
            await RunMergeAsync(files);
            return;
        }

        if (item.IsAdvancedTool && item.ToolKind.HasValue)
        {
            if (files.Count == 1)
            {
                await RunAdvancedToolAsync(files[0], item.ToolKind.Value);
            }
            else
            {
                foreach (var file in files)
                {
                    await RunAdvancedToolAsync(file, item.ToolKind.Value);
                }
            }
            return;
        }

        // Format conversion
        var batch = files.Select(f => (SourcePath: f, Target: item.ActionId)).ToList();
        await RunBatchAsync(batch);
    }

    /// <summary>
    /// True when the operation touches video or audio, which is the only thing
    /// that needs ffmpeg.
    /// </summary>
    private static bool NeedsFfmpeg(IEnumerable<string> sourcePaths, string? targetExtension = null)
    {
        if (targetExtension is not null &&
            MediaExtensions.AllExtensions.Contains(targetExtension.TrimStart('.').ToLowerInvariant()))
        {
            return true;
        }

        return sourcePaths.Any(p =>
            MediaExtensions.AllExtensions.Contains(Path.GetExtension(p).TrimStart('.').ToLowerInvariant()));
    }

    /// <summary>
    /// Makes sure ffmpeg is available before starting work that needs it, giving
    /// the user a way to point at their copy instead of failing mid-conversion.
    /// Returns false if the user backed out.
    /// </summary>
    private bool EnsureFfmpeg(IEnumerable<string> sourcePaths, string? targetExtension = null)
    {
        if (!NeedsFfmpeg(sourcePaths, targetExtension) ||
            FfmpegLocator.FindFfmpegPath(_settings.FfmpegPath) is not null)
        {
            return true;
        }

        var dialog = new FfmpegSetupWindow(_settingsService, _settings) { Owner = this };
        return dialog.ShowDialog() == true;
    }

    private async Task RunBatchAsync(IReadOnlyList<(string SourcePath, string Target)> items)
    {
        if (items.Count == 0) return;

        if (!EnsureFfmpeg(items.Select(i => i.SourcePath), items[0].Target))
        {
            return;
        }

        var cts = new CancellationTokenSource();
        var targetLabel = items[0].Target.ToUpperInvariant();
        var title = items.Count == 1 ? $"Converting to {targetLabel}" : $"Converting {items.Count} files to {targetLabel}";
        var firstFileName = Path.GetFileName(items[0].SourcePath);

        _progressHud.ShowOperation(title, firstFileName, _lastDropPoint, cts);

        var succeeded = 0;
        string? firstOutputFolder = null;
        string? firstError = null;
        string? lastSuccessMessage = null;
        string? lastOutputPath = null;

        for (var i = 0; i < items.Count; i++)
        {
            if (cts.IsCancellationRequested) break;

            var (sourcePath, target) = items[i];
            var index = i;
            var currentFileName = Path.GetFileName(sourcePath);
            _progressHud.SubtitleText.Text = currentFileName;

            var itemProgress = new Progress<double>(fraction =>
            {
                var overall = (index + fraction) / items.Count;
                _progressHud.UpdateProgress(overall);
            });

            var result = await _conversionService.ConvertAsync(sourcePath, target, itemProgress, cts.Token);

            if (result.Success)
            {
                succeeded++;
                lastOutputPath = result.OutputPath;
                var outputIsFolder = result.OutputPath is not null && Directory.Exists(result.OutputPath);
                firstOutputFolder ??= outputIsFolder ? result.OutputPath : Path.GetDirectoryName(result.OutputPath);
                lastSuccessMessage = target == "extract"
                    ? $"{currentFileName} extracted"
                    : $"{currentFileName} → .{target}";
            }
            else
            {
                firstError ??= result.ErrorMessage;
            }
        }

        if (cts.IsCancellationRequested)
        {
            return;
        }

        if (succeeded == items.Count)
        {
            var message = items.Count == 1 ? lastSuccessMessage! : $"{succeeded} file(s) converted.";
            _progressHud.SetCompleted(items.Count == 1 ? $"Converted to {targetLabel}" : "Converted!", message, firstOutputFolder, lastOutputPath);
        }
        else if (succeeded == 0)
        {
            _progressHud.SetFailed("Conversion Failed", firstError ?? "Unknown error.");
        }
        else
        {
            _progressHud.SetCompleted("Partially Converted", $"{succeeded}/{items.Count} succeeded.", firstOutputFolder, lastOutputPath);
        }
    }

    private async Task RunMergeAsync(List<string> sourcePaths)
    {
        if (!EnsureFfmpeg(sourcePaths))
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _progressHud.ShowOperation("Merging files", $"{sourcePaths.Count} files", _lastDropPoint, cts);

        var progress = new Progress<double>(fraction =>
        {
            _progressHud.UpdateProgress(fraction);
        });

        var result = await _mergeService.MergeAsync(sourcePaths, progress, cts.Token);

        if (cts.IsCancellationRequested) return;

        if (result.Success)
        {
            _progressHud.SetCompleted("Files Merged!", $"{sourcePaths.Count} files merged", Path.GetDirectoryName(result.OutputPath), result.OutputPath);
        }
        else
        {
            _progressHud.SetFailed("Merge Failed", result.ErrorMessage ?? "Unknown error.");
        }
    }

    private async Task RunAdvancedToolAsync(string sourcePath, AdvancedToolKind kind)
    {
        if (!EnsureFfmpeg(new[] { sourcePath }))
        {
            return;
        }

        IToolOptions? options;
        string? tempPreviewPath = null;

        try
        {
            switch (kind)
            {
                case AdvancedToolKind.EditPhoto:
                {
                    var dialog = new EditPhotoWindow(sourcePath);
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    options = dialog.Result;
                    break;
                }

                case AdvancedToolKind.EditMetadata:
                {
                    var dialog = new EditMetadataWindow(sourcePath);
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    options = dialog.Result;
                    break;
                }

                case AdvancedToolKind.RedactPhoto:
                {
                    tempPreviewPath = ImagePreviewGenerator.GeneratePreviewPng(sourcePath);
                    var previewPathToUse = tempPreviewPath ?? sourcePath;
                    var dialog = new RedactWindow(previewPathToUse, isVideo: false, sourcePath: sourcePath);
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    options = dialog.Result;
                    break;
                }

                case AdvancedToolKind.RedactVideo:
                {
                    tempPreviewPath = await VideoFrameExtractor.ExtractFirstFrameAsync(_ffmpegRunner, sourcePath, CancellationToken.None);
                    if (tempPreviewPath is null)
                    {
                        ShowToast("Couldn't load preview", "Mandarin couldn't generate a preview frame to redact.", isError: true, folderToOpen: null);
                        return;
                    }

                    var dialog = new RedactWindow(tempPreviewPath, isVideo: true, sourcePath: sourcePath);
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    options = dialog.Result;
                    break;
                }

                case AdvancedToolKind.Compress:
                {
                    var dialog = new CompressOptionsWindow(sourcePath);
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    options = dialog.Result;
                    break;
                }

                case AdvancedToolKind.Crop:
                {
                    var sourceExtension = FileExtensions.GetNormalizedExtension(sourcePath);
                    tempPreviewPath = MediaExtensions.VideoExtensions.Contains(sourceExtension)
                        ? await VideoFrameExtractor.ExtractFirstFrameAsync(_ffmpegRunner, sourcePath, CancellationToken.None)
                        : ImagePreviewGenerator.GeneratePreviewPng(sourcePath);

                    if (tempPreviewPath is null)
                    {
                        ShowToast("Couldn't load preview", "Mandarin couldn't generate a preview to crop.", isError: true, folderToOpen: null);
                        return;
                    }

                    var dialog = new CropWindow(tempPreviewPath, sourcePath);
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    options = dialog.Result;
                    break;
                }

                case AdvancedToolKind.Trim:
                {
                    var info = await MediaProbe.ProbeAsync(sourcePath, CancellationToken.None);
                    var dialog = new TrimWindow(info?.Duration, sourcePath, _ffmpegRunner);
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    options = dialog.Result;
                    break;
                }

                case AdvancedToolKind.Split:
                {
                    var sourceExtension = FileExtensions.GetNormalizedExtension(sourcePath);
                    if (sourceExtension == "pdf")
                    {
                        var pageCount = PdfPageCounter.TryGetPageCount(sourcePath);
                        var dialog = new PdfSplitWindow(pageCount, sourcePath);
                        if (dialog.ShowDialog() != true)
                        {
                            return;
                        }

                        options = dialog.Result;
                    }
                    else
                    {
                        var info = await MediaProbe.ProbeAsync(sourcePath, CancellationToken.None);
                        var dialog = new MediaSplitWindow(info?.Duration, sourcePath);
                        if (dialog.ShowDialog() != true)
                        {
                            return;
                        }

                        options = dialog.Result;
                    }

                    break;
                }

                case AdvancedToolKind.StripMetadata:
                    options = null;
                    break;

                default:
                    return;
            }
        }
        finally
        {
            if (tempPreviewPath is not null)
            {
                try
                {
                    File.Delete(tempPreviewPath);
                }
                catch (IOException)
                {
                }
            }
        }

        await RunAdvancedToolExecutionAsync(sourcePath, kind, options);
    }

    private async Task RunAdvancedToolExecutionAsync(string sourcePath, AdvancedToolKind kind, IToolOptions? options)
    {
        var cts = new CancellationTokenSource();
        var actionName = kind switch
        {
            AdvancedToolKind.EditPhoto => "Editing Photo",
            AdvancedToolKind.EditMetadata => "Updating Metadata",
            AdvancedToolKind.RedactPhoto => "Redacting Photo",
            AdvancedToolKind.RedactVideo => "Redacting Video",
            AdvancedToolKind.Crop => "Cropping Media",
            AdvancedToolKind.Trim => "Trimming Media",
            AdvancedToolKind.Compress => "Compressing File",
            AdvancedToolKind.Split => "Splitting File",
            AdvancedToolKind.StripMetadata => "Stripping Metadata",
            _ => kind.ToString()
        };

        var fileName = Path.GetFileName(sourcePath);
        _progressHud.ShowOperation(actionName, fileName, _lastDropPoint, cts);

        var progress = new Progress<double>(fraction =>
        {
            _progressHud.UpdateProgress(fraction);
        });

        var result = await _advancedToolService.ExecuteAsync(sourcePath, kind, options, progress, cts.Token);

        if (cts.IsCancellationRequested) return;

        if (result.Success)
        {
            var outputIsFolder = result.OutputPath is not null && Directory.Exists(result.OutputPath);
            var folderToOpen = outputIsFolder
                ? result.OutputPath
                : Path.GetDirectoryName(result.OutputPath);

            // A warning means it worked but not exactly as asked (e.g. a target
            // size the encoder couldn't reach) — say so instead of just "Done!".
            var subtitle = result.Warning ?? Path.GetFileName(result.OutputPath);
            _progressHud.SetCompleted($"{ToLabel(kind)} Done!", subtitle, folderToOpen, result.OutputPath);
        }
        else
        {
            _progressHud.SetFailed($"{ToLabel(kind)} Failed", result.ErrorMessage ?? "Unknown error.");
        }
    }

    private static string ToLabel(AdvancedToolKind kind) => kind switch
    {
        AdvancedToolKind.EditPhoto => "Edit Photo",
        AdvancedToolKind.EditMetadata => "Edit Metadata",
        AdvancedToolKind.RedactPhoto => "Redact Photo",
        AdvancedToolKind.RedactVideo => "Redact Video",
        AdvancedToolKind.Compress => Strings.AdvancedToolCompress,
        AdvancedToolKind.Crop => Strings.AdvancedToolCrop,
        AdvancedToolKind.Trim => Strings.AdvancedToolTrim,
        AdvancedToolKind.Split => Strings.AdvancedToolSplit,
        AdvancedToolKind.StripMetadata => Strings.AdvancedToolStripMetadata,
        _ => kind.ToString(),
    };

    private void ShowStatusGlyph(string glyph)
    {
        StatusGlyph.Text = glyph;
        StatusGlyph.Visibility = Visibility.Visible;
    }

    private static void ShowToast(string title, string message, bool isError, string? folderToOpen)
    {
        var toast = new ToastWindow(title, message, isError, folderToOpen);
        toast.Show();
    }
}
