using System.Collections.Generic;

namespace SABepInExManager.Patcher.Models;

public class AppStateLite
{
    public List<string> EnabledModIds { get; set; } = new();
    public string? WorkshopContentPath { get; set; }
}

