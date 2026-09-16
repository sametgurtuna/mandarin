using System.Linq;
using System.Threading.Tasks;
using Mandarin.App.Services;
using Mandarin.App.ViewModels;
using Mandarin.App.Views;
using Mandarin.Core.AdvancedTools;
using Mandarin.Core.AdvancedTools.Images;
using Mandarin.Core.AdvancedTools.Pdf;
using Mandarin.Core.AdvancedTools.VideoAudio;
using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.Archives;
using Mandarin.Core.Conversion.Images;
using Mandarin.Core.Conversion.Pdf;
using Mandarin.Core.Conversion.Text;
using Mandarin.Core.Conversion.VideoAudio;
using Mandarin.Core.Merge;
using Mandarin.Core.Settings;
using WpfApplication = System.Windows.Application;
using StartupEventArgs = System.Windows.StartupEventArgs;
using WindowState = System.Windows.WindowState;
using ExitEventArgs = System.Windows.ExitEventArgs;
using Mandarin.App.Themes;

namespace Mandarin.App;

public partial class App : WpfApplication
{
    private ISettingsService _settingsService = null!;
    private MandarinSettings _settings = null!;
    private TrayIconService _trayIconService = null!;
    private PanelWindow _panelWindow = null!;
    private RadialWheelWindow _radialWheelWindow = null!;
    private GlobalDragHookService _globalDragHook = null!;
    private SettingsWindow? _settingsWindow;
    private SingleInstanceService _singleInstanceService = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        var isStartupLaunch = e.Args.Contains("--startup");

        _singleInstanceService = new SingleInstanceService();
        if (!_singleInstanceService.TryAcquire())
        {
            var filePathArg = e.Args.FirstOrDefault(a => a != "--startup");
            if (filePathArg is not null)
            {
                SingleInstanceService.TrySendToRunningInstance(filePathArg);
            }

            _singleInstanceService.Dispose();
            Shutdown();
            return;
        }

        _settingsService = new JsonSettingsService();
        _settings = _settingsService.Load();
        ApplyLanguage(_settings.Language);
        ThemeManager.Apply(_settings.Theme);

        // One source of truth for "where is ffmpeg": the setting the user can
        // point at a binary, read live so locating it needs no restart.
        MediaProbe.ConfiguredFfmpegPath = () => _settings.FfmpegPath;
        var ffmpegRunner = new ProcessFfmpegRunner(() => _settings.FfmpegPath);

        var registry = new ConverterRegistry(new IConverter[]
        {
            new MagickImageConverter(),
            new ImageToDocxConverter(),
            new VideoAudioConverter(ffmpegRunner),
            new PdfConverter(),
            new TextExportConverter(),
            new SubtitleConverter(),
            new ArchiveConverter(),
        });
        var conversionService = new ConversionService(registry, _settingsService, _settings);

        var advancedToolRegistry = new AdvancedToolRegistry(new IAdvancedTool[]
        {
            new ImageCompressTool(),
            new ImageCropTool(),
            new ImageEditTool(),
            new ImageMetadataTool(),
            new ImageRedactTool(),
            new ImageStripMetadataTool(),
            new VideoAudioCompressTool(ffmpegRunner),
            new VideoCropTool(ffmpegRunner),
            new VideoAudioTrimTool(ffmpegRunner),
            new VideoRedactTool(ffmpegRunner),
            new VideoAudioSplitTool(ffmpegRunner),
            new VideoAudioStripMetadataTool(ffmpegRunner),
            new PdfCompressTool(),
            new PdfSplitTool(),
            new PdfStripMetadataTool(),
        });
        var advancedToolService = new AdvancedToolService(advancedToolRegistry);

        var mergeService = new MergeService(new IMergeTool[]
        {
            new PdfMergeTool(),
            new MediaMergeTool(ffmpegRunner),
        });

        var panelViewModel = new PanelViewModel(_settingsService, _settings);
        _panelWindow = new PanelWindow(panelViewModel, conversionService, advancedToolService, mergeService, ffmpegRunner, _settingsService, _settings);

        _radialWheelWindow = new RadialWheelWindow(conversionService, advancedToolService, mergeService, ffmpegRunner);
        _radialWheelWindow.ActionTriggered += (item, files, dropPt) => _panelWindow.ExecuteActionFromExternal(item, files, dropPt);

        _panelWindow.RadialWheelWindowAccessor = () => _radialWheelWindow;

        // Global Shift-Drag detection
        _globalDragHook = new GlobalDragHookService();
        _globalDragHook.DragShiftDetected += (pt, isAlt) =>
            Dispatcher.Invoke(() => _radialWheelWindow.ShowAtCursor(pt, isAlt));
        _globalDragHook.DragCancelled += () =>
            Dispatcher.Invoke(() => _radialWheelWindow.HideWheel());
        _globalDragHook.DragEnded += () =>
        {
            Dispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(250);
                _radialWheelWindow.HideWheel();
            });
        };
        _globalDragHook.Start();

        _singleInstanceService.StartListening(filePath =>
            Dispatcher.Invoke(() => _panelWindow.TriggerExternalFileRequest(filePath)));

        var startupFilePathArg = e.Args.FirstOrDefault(a => a != "--startup");
        if (startupFilePathArg is not null)
        {
            _panelWindow.TriggerExternalFileRequest(startupFilePathArg);
        }
        else
        {
            // Launched with no file: either a normal manual launch, or Windows
            // starting it at sign-in (--startup). Either way the panel is the
            // whole point of the app, so show it — quietly, without stealing
            // focus, when Windows did the launching.
            _panelWindow.ShowPanel(activate: !isStartupLaunch);
        }

        _trayIconService = new TrayIconService();
        _trayIconService.OpenRequested += OnOpenRequested;
        _trayIconService.SettingsRequested += OnSettingsRequested;
        _trayIconService.QuitRequested += OnQuitRequested;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _globalDragHook?.Dispose();
        _trayIconService?.Dispose();
        _singleInstanceService?.Dispose();
        base.OnExit(e);
    }

    private void OnOpenRequested()
    {
        _panelWindow.ShowPanel();
    }

    private void OnSettingsRequested()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(_settingsService, _settings);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnQuitRequested()
    {
        _globalDragHook.Dispose();
        _trayIconService.Dispose();
        _singleInstanceService.Dispose();
        Shutdown();
    }

    private static void ApplyLanguage(string languageCode)
    {
        try
        {
            var culture = new System.Globalization.CultureInfo(languageCode);
            System.Globalization.CultureInfo.CurrentUICulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
        }
        catch (System.Globalization.CultureNotFoundException)
        {
        }
    }
}
