using System.Collections.Generic;

namespace SABepInExManager.Models;

public class BepInExDiagnosticResult
{
    public bool IsInstalled { get; init; }
    public int ExpectedFileCount { get; init; }
    public int MatchedFileCount { get; init; }
    public string BepInExVersionText { get; init; } = "未知";
    public string ConfigurationManagerVersionText { get; init; } = "未安装";
    public string DoorstopVersionText { get; init; } = "未知";
    public string LocalVersionText { get; init; } = "未知";
    public IReadOnlyList<BepInExDiagnosticIssue> Issues { get; init; } = [];
    public bool HasIssues => Issues.Count > 0;
}

