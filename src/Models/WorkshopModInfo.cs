using CommunityToolkit.Mvvm.ComponentModel;

namespace SABepInExManager.Models;

public class WorkshopModInfo : ObservableObject
{
    private bool _isEnabled;

    public required string ModId { get; init; }
    public required string ModRootPath { get; init; }
    public required string BepInExRootPath { get; init; }
    public required string DisplayName { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? PreviewImagePath { get; init; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}


