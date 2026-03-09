using System;
using System.Collections.Generic;

namespace SABepInExManager.Core.Models;

public class PatcherSyncState
{
    public string AppId { get; set; } = string.Empty;
    public DateTimeOffset LastRunAt { get; set; }
    public Dictionary<string, PatcherModState> Mods { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class PatcherModState
{
    public string Signature { get; set; } = string.Empty;
    public List<string> Files { get; set; } = new();
    public DateTimeOffset LastSyncedAt { get; set; }
}

