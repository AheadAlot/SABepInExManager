namespace SABepInExManager.Core.Models;

public class ManagedFileEntry
{
    public string ModId { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string TargetRelativePath { get; set; } = string.Empty;
    public ModStructureType SourceStructureType { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

