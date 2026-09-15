using Mandarin.App.Resources;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;

namespace Mandarin.App.Services;

/// <summary>
/// Wraps the Windows notification-area icon and its right-click menu
/// (Open, Settings, Quit).
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;

    public event Action? OpenRequested;

    public event Action? SettingsRequested;

    public event Action? QuitRequested;

    public TrayIconService()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Visible = true,
            Text = Strings.TrayTooltip,
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Strings.TrayOpen, null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add(Strings.TraySettings, null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Strings.TrayQuit, null, (_, _) => QuitRequested?.Invoke());

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    private static Drawing.Icon LoadIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/mandarin.ico", UriKind.Absolute);
        var streamInfo = WpfApplication.GetResourceStream(uri)
            ?? throw new InvalidOperationException("Tray icon resource not found.");
        using var stream = streamInfo.Stream;
        return new Drawing.Icon(stream);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
