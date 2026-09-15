using CommunityToolkit.Mvvm.ComponentModel;
using Mandarin.Core.Settings;

namespace Mandarin.App.ViewModels;

public enum PanelVisualState
{
    Idle,
    DragHover,
    Converting,
    Success,
    Error,
}

/// <summary>
/// Backs the floating panel window. Persists position changes to settings and tracks the
/// bubble's current visual state (idle / drag-hover / converting / success / error).
/// </summary>
public partial class PanelViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly MandarinSettings _settings;

    [ObservableProperty]
    private double _left;

    [ObservableProperty]
    private double _top;

    [ObservableProperty]
    private PanelVisualState _visualState = PanelVisualState.Idle;

    public PanelViewModel(ISettingsService settingsService, MandarinSettings settings)
    {
        _settingsService = settingsService;
        _settings = settings;
        _left = settings.PanelX;
        _top = settings.PanelY;
    }

    public void SavePosition(double left, double top)
    {
        Left = left;
        Top = top;
        _settings.PanelX = left;
        _settings.PanelY = top;
        _settingsService.Save(_settings);
    }
}
