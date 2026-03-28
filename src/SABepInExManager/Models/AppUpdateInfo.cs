using System;

namespace SABepInExManager.Models;

public class AppUpdateInfo
{
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public string ReleaseTag { get; set; } = string.Empty;
    public string ReleaseName { get; set; } = string.Empty;
    public string ReleaseUrl { get; set; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; set; }
    public bool IsUpdateAvailable { get; set; }
}

