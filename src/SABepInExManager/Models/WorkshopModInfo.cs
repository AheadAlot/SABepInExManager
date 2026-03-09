using CommunityToolkit.Mvvm.ComponentModel;
using SABepInExManager.Core.Models;

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
    public ModStructureType StructureType { get; init; } = ModStructureType.Standard;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}
