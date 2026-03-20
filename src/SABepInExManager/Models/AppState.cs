using System.Collections.Generic;

namespace SABepInExManager.Models;

public class AppState
{
    public List<string> EnabledModIds { get; set; } = new();
    public string? WorkshopContentPath { get; set; }
    public bool? EnableDebugLogging { get; set; }
}

public class ConflictItem
{
    public required string RelativePath { get; init; }
    public required List<string> ModIds { get; init; }
    public required string WinnerModId { get; init; }
}

public class InstallResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}


