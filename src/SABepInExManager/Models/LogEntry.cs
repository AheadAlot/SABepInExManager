using System;

namespace SABepInExManager.Models;

public class LogEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Message { get; init; }
    public AppLogLevel Level { get; init; } = AppLogLevel.Info;

    public string TimestampText => Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
}


