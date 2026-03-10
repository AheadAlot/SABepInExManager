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
}

