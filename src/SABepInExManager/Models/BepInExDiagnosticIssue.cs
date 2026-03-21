namespace SABepInExManager.Models;

public class BepInExDiagnosticIssue
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required BepInExDiagnosticIssueSeverity Severity { get; init; }
    public string SuggestedAction { get; init; } = "建议执行诊断修复或重装 BepInEx。";
    public bool CanFix { get; init; } = true;
}

