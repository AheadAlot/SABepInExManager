using System;
using System.Collections.Generic;

namespace SABepInExManager.Core.Models;

public class AutoUpdaterSyncState
{
    public string AppId { get; set; } = string.Empty;
    public DateTimeOffset LastRunAt { get; set; }
    public Dictionary<string, AutoUpdaterModState> Mods { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class AutoUpdaterModState
{
    public string Signature { get; set; } = string.Empty;
    public List<string> Files { get; set; } = new();
    public DateTimeOffset LastSyncedAt { get; set; }
    public Dictionary<string, AutoUpdaterCachedFileState> CachedFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class AutoUpdaterCachedFileState
{
    public string TargetRelativePath { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Length { get; set; }
    public DateTimeOffset LastWriteTimeUtc { get; set; }
    public string ContentHash { get; set; } = string.Empty;
}

