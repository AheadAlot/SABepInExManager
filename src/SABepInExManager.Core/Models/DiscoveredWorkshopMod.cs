namespace SABepInExManager.Core.Models;

public class DiscoveredWorkshopMod
{
    public string ModId { get; set; } = string.Empty;
    public string ModRootPath { get; set; } = string.Empty;
    public string BepInExRootPath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? PreviewImagePath { get; set; }
    public ModStructureType StructureType { get; set; } = ModStructureType.Standard;
}

