namespace SABepInExManager.ViewModels;

public sealed class BackupSnapshotItemViewModel : ViewModelBase
{
    public required string FolderName { get; init; }
    public required string CreatedAtText { get; init; }
    public required int FileCount { get; init; }
    public required string SizeText { get; init; }
}

