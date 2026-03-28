using System;

namespace SABepInExManager.Models;

public class AppConfig
{
    public string? GameRootPath { get; set; }
    public string? WorkshopContentPath { get; set; }
    public bool EnableDebugLogging { get; set; }

    public DateTimeOffset? LastUpdateCheckAt { get; set; }
    public bool LastUpdateCheckSucceeded { get; set; }
    public string? LastKnownLatestVersion { get; set; }
    public string? LastKnownReleaseTag { get; set; }
    public string? LastKnownReleaseName { get; set; }
    public string? LastKnownReleaseUrl { get; set; }
    public DateTimeOffset? LastKnownReleasePublishedAt { get; set; }
    public bool HasPendingAppUpdate { get; set; }
    public string? LastUpdateCheckError { get; set; }
}


