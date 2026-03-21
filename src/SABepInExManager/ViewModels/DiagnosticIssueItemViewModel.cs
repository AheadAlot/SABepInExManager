using SABepInExManager.Models;

namespace SABepInExManager.ViewModels;

public class DiagnosticIssueItemViewModel : ViewModelBase
{
    public DiagnosticIssueItemViewModel(BepInExDiagnosticIssue issue)
    {
        Key = issue.Key;
        Title = issue.Title;
        Description = issue.Description;
        SuggestedAction = issue.SuggestedAction;
        Severity = issue.Severity;
        CanFix = issue.CanFix;
    }

    public string Key { get; }
    public string Title { get; }
    public string Description { get; }
    public string SuggestedAction { get; }
    public BepInExDiagnosticIssueSeverity Severity { get; }
    public bool CanFix { get; }

    public bool IsDangerousFix => Key is "install_check_failed" or "version_unknown" or "reference_payload_missing";
    public bool ShowSafeFixButton => CanFix && !IsDangerousFix;
    public bool ShowDangerousFixButton => CanFix && IsDangerousFix;

    public string SeverityText => Severity switch
    {
        BepInExDiagnosticIssueSeverity.Error => "错误",
        BepInExDiagnosticIssueSeverity.Warning => "警告",
        _ => "信息",
    };
}

