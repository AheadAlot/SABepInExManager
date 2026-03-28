using System;

namespace SABepInExManager.Models;

public class AppUpdateCheckResult
{
    public bool Succeeded { get; set; }
    public AppUpdateStatus Status { get; set; } = AppUpdateStatus.Idle;
    public AppUpdateInfo? UpdateInfo { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public bool IsManualTrigger { get; set; }
}

