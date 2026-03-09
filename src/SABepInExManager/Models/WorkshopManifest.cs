namespace SABepInExManager.Models;

public class WorkshopManifest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public WorkshopManifestMetadata? Metadata { get; set; }
}

public class WorkshopManifestMetadata
{
    public long? Id { get; set; }
    public string? Version { get; set; }
    public string? PackageId { get; set; }
}


