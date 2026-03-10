using System;
using System.Collections.Generic;

namespace SABepInExManager.Models;

public class GameRootDetectionResult
{
    public bool Success { get; init; }
    public string? GameRootPath { get; init; }
    public string Message { get; init; } = string.Empty;
    public List<GameRootCandidate> Candidates { get; init; } = new();
}

public class GameRootCandidate
{
    public required string SteamRootPath { get; init; }
    public required string LibraryPath { get; init; }
    public required string ManifestPath { get; init; }
    public required string InstallDir { get; init; }
    public required string GameRootPath { get; init; }
    public DateTimeOffset ManifestLastWriteTime { get; init; }
}


