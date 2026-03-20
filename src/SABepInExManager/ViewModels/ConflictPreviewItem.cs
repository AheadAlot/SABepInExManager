namespace SABepInExManager.ViewModels;

public class ConflictPreviewItem
{
    public required string RelativeToGameRootPath { get; init; }
    public required string ExistsIn { get; init; }
    public required string Winner { get; init; }
}

